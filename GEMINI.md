# Gemini Developer & Repository Guide: Bukeperry Mod & LLM Bot

## 🌲 Repository Overview

This repository contains **Bukeperry**, an interactive AI companion for Valheim and Discord, powered by Google Gemma on AWS Bedrock.

The project is structured into three main components:
1. **Bukeperry Mod & Distribution (`mod/`)**: In-game C# companion mod for Valheim (BepInEx 5 & Jötunn), Linux dedicated server bundle packager (`scripts/package-server-bundle.sh`), headless server installer (`scripts/install-server.sh`), and Thunderstore publishing tooling (`scripts/thunderstore/publish.sh`).
2. **LLM Lambda (`llm-lambda/`)**: Serverless Discord slash command integration (`/bukeperry`, `/bukeperry-reset`) and in-game chat endpoint (`POST /game/chat`). Implements an API Gateway handler + background worker Lambda architecture using AWS Bedrock (`google.gemma-4-31b`), local RAG knowledge retrieval, and DynamoDB-backed conversation state.
3. **AWS CDK Infrastructure (`cdk/`)**: AWS CDK v2 infrastructure definitions deploying stack `ValheimLLM`.

---

## 🏗️ Architecture & Component Details

### 1. `llm-lambda/`
- **Runtime**: Node.js 24.x (`Runtime.NODEJS_24_X`), TypeScript, tested with Vitest.
- **Directory Structure**:
  - `src/`:
    - `core.ts`: Shared Bedrock LLM core (`generateBukeperryReply`), local RAG knowledge retrieval, caveman persona sanitization, and DynamoDB state management.
    - `handler.ts`: Discord interaction webhook handler (Ed25519 signature verification & deferred response).
    - `worker.ts`: Background worker invoking shared core and patching Discord interaction message.
    - `gameHandler.ts`: Synchronous HTTP handler for in-game chat (`POST /game/chat`), secured by API Gateway API Key.
    - `retriever.ts`: Local RAG retrieval engine querying `data/valheim_knowledge.json`.
    - `data/valheim_knowledge.json`: Structured Valheim lore & troll knowledge base.
  - `test/`:
    - Unit tests for handler, gameHandler, and retriever (`handler.test.ts`, `gameHandler.test.ts`, `retriever.test.ts`).
- **Flow**:
  1. **Discord Path**:
     - API Gateway receives Discord Interaction webhook at `/interactions`.
     - `handler.ts` verifies Ed25519 signature, returns deferred response (Type 5), invokes `worker.ts` asynchronously.
     - `worker.ts` calls `generateBukeperryReply` in `core.ts` and patches Discord interaction original message.
  2. **In-Game Chat Path**:
     - Game server / client sends `POST /game/chat` with `x-api-key` header and JSON body `{ prompt, channelId? }`.
     - API Gateway validates API Key against `ValheimModUsagePlan` at the edge (rejects unauthorized with 403).
     - `gameHandler.ts` validates payload, calls `generateBukeperryReply(prompt, channelId)` synchronously, and returns `{ reply }` with status 200.

### 2. `cdk/`
- **Entrypoint**: `bin/cdk.ts` instantiates:
  - `ValheimLLMStack` (`lib/llm-stack.ts`): DynamoDB state table `ValheimLLMStateTable`, `ValheimLLMWorkerLambda`, `ValheimLLMLambda`, `ValheimLLMGameLambda`, REST API Gateway (`ValheimLLMGateway`) with routes `/interactions` and `/game/chat`, `ValheimModApiKey`, `ValheimModUsagePlan`, Secrets Manager read policy (`valheim-discord-secrets`), Bedrock IAM permissions.

### 3. `mod/` (Bukeperry Mod & Distribution Tooling)
- **Mod Architecture**:
  - `BukeperryMod/`: C# .NET Standard 2.1 project utilizing BepInEx 5 and Jötunn.
  - Custom Troll Prefab & Merchant: Registers custom `bukeperry` NPC trader with custom dialog lines, inventory items, and logs purchase anti-greed check.
  - Conversational AI: Sniffs `/s` shouts and proximity speech, dispatches async HTTP POSTs to API Gateway off-thread, and delivers responses via `ZRoutedRpc` `BukeperrySpeechRPC` (overhead bubble `Chat.SetNpcText` + chat log).
  - Single Source of Truth: Mod version is maintained solely in `BukeperryPlugin.cs` (`PluginVersion = "0.2.0"`).
- **Distribution Targets**:
  - **Thunderstore / r2modman (Client Players)**:
    - Namespace: `jimbrighter`, Package: `BukeperryMod`.
    - Auto-dependencies: `denikson-BepInExPack_Valheim-5.4.2351` and `ValheimModding-Jotunn-2.30.2`.
    - Automated packaging & publishing via `mod/scripts/thunderstore/publish.sh` using Thunderstore CLI (`tcli`).
    - Zero client config required (all AI calls are handled server-side).
  - **Linux Dedicated Server (`/home/vhserver`)**:
    - Packager: `mod/scripts/package-server-bundle.sh` packages `bukeperry-server-bundle-v*.zip` (BepInEx Unix + Doorstop + Jotunn + BukeperryMod.dll) into `mod/dist/`.
    - Installer: `mod/scripts/install-server.sh` pulls the latest release from GitHub, extracts into server directory, configures `com.jimbrighter.bukeperrymod.cfg`, and injects Doorstop environment variables into `valheim.service`.

---

## 🛠️ Common Commands & Workflows

### Building & Deploying the Mod
```bash
# Build and copy DLL to local Valheim and mod/dist/
./mod/scripts/macos-deploy.sh

# Package dedicated server zip bundle
./mod/scripts/package-server-bundle.sh
```

### Running Tests
```bash
cd llm-lambda
npm test
```

### Deploying Infrastructure
```bash
cd cdk
npm run deploy
```

---

## 🔐 Configuration & Secrets Manager

The Lambda functions read runtime credentials from AWS Secrets Manager secret **`valheim-discord-secrets`**.

Required secret structure:
```json
{
  "token": "YOUR_DISCORD_BOT_TOKEN",
  "channel_id": "YOUR_DISCORD_CHANNEL_ID",
  "public_key": "YOUR_DISCORD_APPLICATION_PUBLIC_KEY",
  "user_agent": "BukeperryBot",
  "port": "2456"
}
```

---

## 💡 Code Conventions & Persona Guardrails

1. **Node.js Runtime**: All AWS Lambdas use Node.js 24 (`Runtime.NODEJS_24_X`).
2. **Bundling**: TypeScript / Node.js bundling is handled automatically during CDK synth/deploy via `NodejsFunction` and `esbuild`.
3. **Discord Webhook Timeout**: Discord interaction endpoints MUST respond within 3000ms. Do not run heavy processing or Bedrock API calls inside `handler.ts`; pass workloads asynchronously to `worker.ts`.
4. **Bukeperry Persona & Lore Guardrails**:
   - **Troll-First Perspective**: Any changes to LLM prompts, persona rules, or knowledge entries MUST strictly reflect Bukeperry's in-world troll perspective, NOT a human player's or Valheim strategy wiki perspective. Trolls care about logs, caves, moss, smashing, greydwarves, and their view of other creatures.
   - **Speech & Formatting**: Strict caveman troll formatting: all lowercase text, primarily third-person ("bukeperry" / "troll", with sparing dumb caveman "i" like "i bukeperry"), short punchy sentences (at most 5 words on average, 1–4 words encouraged), no asterisks or stage directions.
   - **Biome & World Attitudes**: Proud, swaggering brute troll. King of Black Forest, deep reverence/respect for The Elder as forest master, treats Meadows creatures like pets/snacks, treats Swamps as a muddy nuisance, refuses Mountain due to bare feet freezing in snow. Skeletons and bone creatures are treated with deep disdain as fragile rattling pests that easily snap and crush to dust under a log.
   - **Canonical 7-Boss World Order**: 1: Eikthyr (Meadows), 2: The Elder (Black Forest), 3: Bonemass (Swamp), 4: Moder (Mountain), 5: Yagluth (Plains), 6: The Queen (Mistlands), 7: Fader (Ashlands / Final Boss).
5. **Mod Changes & Versioning Workflow**: Any time files in `mod/` are touched or modified, bump the version in `mod/BukeperryMod/BukeperryPlugin.cs` (`PluginVersion = "x.y.z"`), and immediately run `./mod/scripts/macos-deploy.sh` to compile, deploy to the local Valheim client, and update `mod/dist/BukeperryMod.dll`.
