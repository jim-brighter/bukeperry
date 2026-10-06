using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BukeperryMod
{
  public class BukeperryController : MonoBehaviour
  {
    public static readonly List<BukeperryController> Instances = [];

    public Humanoid Humanoid { get; private set; }
    public MonsterAI MonsterAI { get; private set; }

    public const string GrudgeZdoKey = "Bukeperry_Grudges";
    public const string LastAttackTimeZdoKey = "Bukeperry_LastAttackTime";
    public const float DeaggroDistance = 50.0f;
    public const float OutOfRangeTimeout = 10.0f;
    public const float DisengageTimeout = 30.0f;

    private ZNetView m_nview;
    private readonly HashSet<long> m_hostilePlayerIDs = [];
    private string m_lastSyncedGrudgeString = null;
    private readonly Dictionary<long, float> m_outOfRangeTimers = [];

    private void Awake()
    {
      if (Instances.Count > 0 && ZNet.instance != null && ZNet.instance.IsServer())
      {
        BukeperryPlugin.Log.LogWarning("Duplicate Bukeperry detected! Destroying extra instance.");
        if (TryGetComponent<ZNetView>(out var nview) && nview.GetZDO() != null)
        {
          nview.ClaimOwnership();
          nview.Destroy();
        }
        else
        {
          Destroy(gameObject);
        }
        return;
      }

      Instances.Add(this);
      Humanoid = GetComponent<Humanoid>();
      MonsterAI = GetComponent<MonsterAI>();
      m_nview = GetComponent<ZNetView>();

      // Self-heal: If loaded from a glitched save where Y position was falling, snap to terrain
      if (ZoneSystem.instance != null && ZoneSystem.instance.FindFloor(transform.position, out float floorY))
      {
        if (transform.position.y < floorY - 1f || transform.position.y > floorY + 50f)
        {
          transform.position = new Vector3(transform.position.x, floorY, transform.position.z);
        }
      }
    }

    private void Start()
    {
      if (m_nview == null)
      {
        m_nview = GetComponent<ZNetView>();
      }

      // Dormant sector wake-up check: if more than DisengageTimeout seconds passed since last attack, clear lingering grudge
      if (m_nview != null && m_nview.IsValid() && m_nview.IsOwner() && ZNet.instance != null)
      {
        float lastAttackTime = m_nview.GetZDO().GetFloat(LastAttackTimeZdoKey, 0f);
        if (lastAttackTime > 0f)
        {
          double now = ZNet.instance.GetTimeSeconds();
          if (now - lastAttackTime > DisengageTimeout)
          {
            m_nview.GetZDO().Set(GrudgeZdoKey, "");
            m_nview.GetZDO().Set(LastAttackTimeZdoKey, 0f);
            m_hostilePlayerIDs.Clear();
            m_lastSyncedGrudgeString = "";
            BukeperryPlugin.Log.LogInfo("Bukeperry woke up after attackers fled while dormant. Chill again.");
          }
        }
      }
    }

    private void OnDestroy()
    {
      Instances.Remove(this);
    }

    public HashSet<long> GetHostilePlayerIDs()
    {
      if (m_nview == null || !m_nview.IsValid() || m_nview.GetZDO() == null)
      {
        return m_hostilePlayerIDs;
      }

      string zdoString = m_nview.GetZDO().GetString(GrudgeZdoKey, "");
      if (zdoString != m_lastSyncedGrudgeString)
      {
        m_lastSyncedGrudgeString = zdoString;
        m_hostilePlayerIDs.Clear();
        if (!string.IsNullOrEmpty(zdoString))
        {
          string[] parts = zdoString.Split(',');
          foreach (string part in parts)
          {
            if (long.TryParse(part.Trim(), out long id))
            {
              m_hostilePlayerIDs.Add(id);
            }
          }
        }
      }
      return m_hostilePlayerIDs;
    }

    private void Update()
    {
      // Grudge and de-aggro logic is strictly authoritative on the owner
      if (m_nview == null || !m_nview.IsOwner()) return;

      var hostileIDs = GetHostilePlayerIDs();
      if (hostileIDs.Count == 0) return;

      List<long> toRemoveDied = null;
      List<long> toRemoveFled = null;

      foreach (long playerID in hostileIDs)
      {
        Player p = Player.GetPlayer(playerID);
        if (p != null && p.IsDead())
        {
          toRemoveDied ??= [];
          toRemoveDied.Add(playerID);
          continue;
        }

        // Check if player left sector or ran beyond DeaggroDistance
        bool outOfRange = (p == null) || (Vector3.Distance(transform.position, p.transform.position) > DeaggroDistance);

        if (outOfRange)
        {
          m_outOfRangeTimers.TryGetValue(playerID, out float elapsed);
          elapsed += Time.deltaTime;
          m_outOfRangeTimers[playerID] = elapsed;

          if (elapsed >= OutOfRangeTimeout)
          {
            toRemoveFled ??= [];
            toRemoveFled.Add(playerID);
          }
        }
        else
        {
          m_outOfRangeTimers[playerID] = 0f;
        }
      }

      if (toRemoveDied != null)
      {
        foreach (long id in toRemoveDied)
        {
          RemoveGrudge(id, "died");
        }
      }

      if (toRemoveFled != null)
      {
        foreach (long id in toRemoveFled)
        {
          RemoveGrudge(id, "fled");
        }
      }
    }

    public void AddGrudge(long playerID)
    {
      if (playerID == 0) return;

      // Grudge state is strictly authoritative on the owner
      if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner()) return;

      GetHostilePlayerIDs();
      m_outOfRangeTimers.Remove(playerID);

      if (ZNet.instance != null && m_nview != null && m_nview.IsValid())
      {
        m_nview.GetZDO().Set(LastAttackTimeZdoKey, (float)ZNet.instance.GetTimeSeconds());
      }

      if (m_hostilePlayerIDs.Add(playerID))
      {
        string newString = string.Join(",", m_hostilePlayerIDs);
        m_lastSyncedGrudgeString = newString;
        if (m_nview != null && m_nview.IsValid())
        {
          m_nview.GetZDO().Set(GrudgeZdoKey, newString);
        }

        Player player = Player.GetPlayer(playerID);
        string name = player != null ? player.GetPlayerName() : $"Player {playerID}";
        BukeperryPlugin.Log.LogInfo($"Bukeperry attacked by {name}! Entering rage mode.");
      }

      if (MonsterAI != null)
      {
        Player player = Player.GetPlayer(playerID);
        if (player != null)
        {
          Traverse.Create(MonsterAI).Method("SetTarget", player).GetValue();
        }
        MonsterAI.Alert();
      }
    }

    public void RemoveGrudge(long playerID, string reason = "died")
    {
      if (playerID == 0) return;

      // Grudge state is strictly authoritative on the owner
      if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner()) return;

      GetHostilePlayerIDs();
      m_outOfRangeTimers.Remove(playerID);

      if (m_hostilePlayerIDs.Remove(playerID))
      {
        string newString = string.Join(",", m_hostilePlayerIDs);
        m_lastSyncedGrudgeString = newString;
        if (m_nview != null && m_nview.IsValid())
        {
          m_nview.GetZDO().Set(GrudgeZdoKey, newString);
        }
        BukeperryPlugin.Log.LogInfo($"Attacking viking {playerID} {reason}. Grudge resolved.");

        if (m_hostilePlayerIDs.Count == 0)
        {
          if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
          {
            m_nview.GetZDO().Set(LastAttackTimeZdoKey, 0f);
          }

          if (MonsterAI != null)
          {
            Traverse.Create(MonsterAI).Field<Character>("m_targetCreature").Value = null;
            Traverse.Create(MonsterAI).Field<bool>("m_alerted").Value = false;
          }
          BukeperryPlugin.Log.LogInfo("All attackers resolved. Bukeperry is chill again.");
        }
      }
    }

    public void OnAttackedBy(Player player)
    {
      if (player == null || player.IsDead()) return;
      AddGrudge(player.GetPlayerID());
    }

    public bool IsHostileTo(Player player)
    {
      if (player == null) return false;
      return GetHostilePlayerIDs().Contains(player.GetPlayerID());
    }

    private static ZDOID s_cachedBukeperryZDOID = ZDOID.None;

    public static bool TryGetBukeperryLocation(out Vector3 position, out ZDOID zdoid)
    {
      zdoid = ZDOID.None;
      position = Vector3.zero;

      // 1. Live GameObject instance (singleplayer, local client, or host)
      if (Instances.Count > 0 && Instances[0] != null)
      {
        var inst = Instances[0];
        position = inst.transform.position;
        if (inst.TryGetComponent<ZNetView>(out var nv) && nv.GetZDO() != null)
        {
          zdoid = nv.GetZDO().m_uid;
          s_cachedBukeperryZDOID = zdoid;
        }
        return true;
      }

      // 2. Cached ZDO in ZDOMan on dedicated server
      if (s_cachedBukeperryZDOID != ZDOID.None && ZDOMan.instance != null)
      {
        ZDO cachedZdo = ZDOMan.instance.GetZDO(s_cachedBukeperryZDOID);
        if (cachedZdo != null)
        {
          position = cachedZdo.GetPosition();
          zdoid = s_cachedBukeperryZDOID;
          return true;
        }
      }

      // 3. Search ZDOMan for Bukeperry prefab
      if (ZDOMan.instance != null)
      {
        int prefabHash = BukeperryPrefab.PrefabName.GetStableHashCode();
        var dict = Traverse.Create(ZDOMan.instance).Field<Dictionary<ZDOID, ZDO>>("m_objectsByID")?.Value;
        if (dict != null)
        {
          foreach (var kvp in dict)
          {
            if (kvp.Value != null && kvp.Value.GetPrefab() == prefabHash)
            {
              s_cachedBukeperryZDOID = kvp.Key;
              zdoid = kvp.Key;
              position = kvp.Value.GetPosition();
              return true;
            }
          }
        }
      }

      // 4. Fallback to marked spawn position from world seed
      Vector3? spawnPos = BukeperrySpawner.GetSpawnPosition();
      if (spawnPos.HasValue)
      {
        position = spawnPos.Value;
        return true;
      }

      return false;
    }

    public void Speak(string text)
    {
      ZDOID zdoid = ZDOID.None;
      if (TryGetComponent<ZNetView>(out var nview) && nview.GetZDO() != null)
      {
        zdoid = nview.GetZDO().m_uid;
      }
      Speak(zdoid, text);
    }

    public static void Speak(ZDOID zdoid, string text)
    {
      if (string.IsNullOrWhiteSpace(text)) return;

      if (ZRoutedRpc.instance != null)
      {
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "BukeperrySpeechRPC", zdoid, text);
      }
      else if (Chat.instance != null && Instances.Count > 0)
      {
        Chat.instance.SetNpcText(Instances[0].gameObject, Vector3.up * 2.5f, 25f, 7f, "", text, large: false);
      }
    }

    public static void OnBukeperrySpeechRPC(long sender, ZDOID zdoid, string text)
    {
      GameObject bukeperryGo = null;
      if (ZNetScene.instance != null && zdoid != ZDOID.None)
      {
        bukeperryGo = ZNetScene.instance.FindInstance(zdoid);
      }

      if (bukeperryGo == null && Instances.Count > 0)
      {
        bukeperryGo = Instances[0].gameObject;
      }

      if (bukeperryGo != null && Chat.instance != null)
      {
        Chat.instance.SetNpcText(bukeperryGo, Vector3.up * 2.5f, 25f, 7f, "", text, large: false);
        Chat.instance.AddString($"<color=#5599ff>Bukeperry</color>: {text}");
      }
    }
  }
}
