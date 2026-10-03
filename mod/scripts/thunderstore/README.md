# Bukeperry Mod 🌲🧌

**Bukeperry** is a legendary cave troll merchant, forest guardian, and conversational companion for Valheim.

Tired of silent NPCs? Bukeperry watches over the Black Forest with pride, swagger, and a massive log club. He sells forest supplies, shares ancient troll lore, and speaks directly to players in real-time.

---

## 🌟 Features

### 🛒 The Forest Merchant
- **Store Inventory**: Sells forest essentials like Stone and Wood for Gold Coins.
- **Merchant Interaction**: Walk up to Bukeperry and press `[E]` to browse his wares.
- **Where to Find Him**: Bukeperry makes his home in the Black Forest nearest to your world spawn. Venture into the woods to find his grove!

### 💬 Live In-Game Conversations
- **Talk Naturally**: Walk up to Bukeperry and talk in local chat or shout across the woods with `/s`.
- **Overhead Speech**: Bukeperry responds with dynamic overhead speech bubbles and chat messages in his authentic caveman troll voice.
- **Lore & Creature Knowledge**: Ask him about the forest, other biomes, bosses, skeletons, or his opinions on Odin and the gods.
- **Zero Lag**: All AI processing runs completely asynchronous off the game thread. Zero frame drops or server stutter.

### 🌐 Seamless Multiplayer
- **No Client Setup Required**: In multiplayer, only the dedicated server connects to the AI backend. Players simply install the mod via Thunderstore and start talking!

---

## 📥 Installation

### Option 1: Thunderstore / r2modman (Recommended)
1. Install [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) or [r2modman](https://thunderstore.io/package/ebkr/r2modman/).
2. Click **Install with Mod Manager** on this page.
3. Launch game via the mod manager. All dependencies (BepInEx and Jötunn) will be handled automatically!

### Option 2: Manual Installation
1. Install [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/).
2. Install [Jötunn, the Valheim Library](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/).
3. Download this mod and place `BukeperryMod.dll` into your `Valheim/BepInEx/plugins/` directory.

---

## ⚙️ Configuration (Server / Host)

When hosting a dedicated server (or playing local/singleplayer) with AI conversation enabled:
- Config file: `BepInEx/config/com.jimbrighter.bukeperrymod.cfg`
- Fields:
  - `ApiEndpoint`: The AWS API Gateway endpoint URL (e.g. `https://xxxx.execute-api.us-east-1.amazonaws.com/prod/game/chat`)
  - `ApiKey`: The API Key for authorization
  - `ProximityRadius`: How close players must be to talk to Bukeperry (default `20` meters; shouts reach up to `70` meters)
  - `ChannelId`: Optional channel ID (such as a Discord channel ID) for persistent multi-turn conversation memory

*Note: For regular players connecting to a dedicated server, no configuration is needed!*

---

## 🔗 Links & Source
- Source code: [GitHub: jim-brighter/bukeperry](https://github.com/jim-brighter/bukeperry)
- Created by **Jim Brighter**

