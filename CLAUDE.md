# Firehouse Co-op — project guide for Claude Code

The design doc `docs/GDD.md` is the source of truth. Read it before you start any new feature. The designer owns the design, and Claude writes almost all of the code.

## The game in one paragraph

This is a low-poly, physics-driven online co-op firefighting game for Steam, for 1–8 players, in the "friendslop" genre (Peak, R.E.P.O., RV There Yet, Lethal Company). The crew starts in a firehouse lobby and gets a call over a physical radio. They drive a small fire truck over rough roads, put out the fire with the right agent (water, foam, CO2, dry chemical, sand) and rescue civilians. Then they get paid, buy upgrades and take bigger calls. Short silly intermission calls happen between the main levels.

**Rules that must never be broken**
- No classes or roles. Every player can do every job.
- Friendly fire is on: hoses knock teammates over, axes down them, the truck runs them over. Downed players are always revived and never killed for good.
- Proximity voice chat is a core feature.
- Mission info comes from a radio (physical speakers in the firehouse and truck cab), not a HUD.
- Up to 8 players.
- Art is low-poly and flat-shaded with vertex colors, using one shared material wherever possible.
- Gambling uses in-game cash only. No real money anywhere, and cosmetics are never sold.

## Tech stack

- Unity 6 (see `ProjectSettings/ProjectVersion.txt`) with URP and the new Input System. C# only.
- Netcode for GameObjects (NGO), host-based peer-to-peer.
- Facepunch.Steamworks for lobbies, invites, Rich Presence, achievements and voice, plus the Facepunch transport for NGO.
- Steam App ID **480** (Spacewar test app) until we have our own. `steam_appid.txt` lives at the project root.
- Unity Transport (UTP) is used as a **local/direct-IP fallback transport** for same-machine testing, because two instances can't both be logged into Steam on one PC.
- Ask the designer before adding any paid asset or any third-party package not listed here.

## Folder structure

All of our content lives under `Assets/_Project/`. Leave the template's folders (`Settings`, `TutorialInfo`, etc.) alone.

```
Assets/_Project/
  Scripts/
    Core/         bootstrap, service locator, tuning/debug panel, shared utils
    Networking/   NetworkManager setup, transport switch, custom messages, ownership helpers
    Lobby/        Steam lobby create/join, join codes, Rich Presence, scene flow
    Player/       Smokies controller, camera, grab/carry, ladders, knockback, downed/revive
    Truck/        raycast suspension, driving, seats, back step, siren/horn/lights, pump panel, tank
    Water/        hose rope, nozzle, spray, water hits, kickback
    Fire/         fire grid sim, materials/classes, network sync, local visuals
    Rescue/       (later) civilians
    Radio/        (later) dispatcher, speakers, call tickets
    Economy/      (later) paycheck, crew fund, shop, upgrades
    Audio/        proximity voice, 3D audio helpers
    UI/           menus, join-code entry, debug panel UI
  Editor/         editor-only scripts: Tools/Firehouse/* menu items, scene/prefab builders
  Tests/
    EditMode/
    PlayMode/
  Data/           ScriptableObject assets (FireMaterials, Agents, TruckStats, Upgrades, Levels, Tuning)
  Prefabs/
  Scenes/         Bootstrap, Firehouse, TestRoad, TestFire
  Art/
    Materials/    the shared vertex-color material lives here
    Shaders/
    Meshes/       generated/procedural meshes
  Audio/
Assets/Plugins/   vendored third-party DLLs (Facepunch.Steamworks)
```

Assemblies: `Firehouse.Runtime` (Scripts), `Firehouse.Editor` (Editor), and `Firehouse.Tests.EditMode` / `Firehouse.Tests.PlayMode`.

## IMPORTANT: creating folders and files

**Create folders, scripts and other files directly on disk** (Write tool or shell), then refresh Unity through the MCP bridge (`refresh_unity`). **Never use the MCP `manage_asset` tool to create folders**, because it has crashed the editor before. Use MCP tools for editor work: building scenes, components, prefabs, materials, reading the console, running tests and taking screenshots.

After you write or change scripts: refresh, wait for compilation (`mcpforunity://editor/state` → `data.advice.ready_for_tools`), then `read_console` for errors before you go on.

## Coding conventions

- Namespaces follow the folders: `Firehouse.Core`, `Firehouse.Player`, `Firehouse.Fire`, and so on.
- Naming: `PascalCase` for types, methods and properties, `_camelCase` for private fields, `[SerializeField] private` rather than public fields. One main type per file, and the file name matches the type.
- **Data-driven:** tunable numbers live in ScriptableObjects (`FireMaterialDef`, `FireClassDef`, `AgentDef`, `TruckStats`, `HoseStats`, `PlayerStats`, later `UpgradeDef` and `LevelDef`). Don't hard-code gameplay numbers in MonoBehaviours.
- **F1 debug panel:** main tuning values register with the debug panel (`Firehouse.Core.Tuning`) so they can be adjusted live in play.
- Keep the simulation separate from presentation. Fire, water and truck logic go in plain C# classes where practical, so they can be unit-tested in EditMode tests. MonoBehaviours and NetworkBehaviours are thin wrappers.
- Every repeatable setup (test scenes, prefabs, the Smokies model, the shared material) has an editor builder under the **`Tools/Firehouse/`** menu, so it can be rebuilt in one click. Generated assets can be regenerated, so don't hand-edit them.
- Art: primitives, ProBuilder and procedural meshes, colored with vertex colors through the one shared material `Art/Materials/M_VertexColor`.

## Networking rules (authority)

| Thing | Owner |
| --- | --- |
| Fire grid, civilians, paycheck/economy, water tank level, pump pressure | **Host** |
| Each player's movement, the nozzle they hold, objects they're carrying | **Owning client** (via ownership transfer) |
| Truck physics | **Current driver** gets ownership while driving, and it returns to the host when they leave the seat |
| Water particles, smoke, sparks, flames, hose rope shape | **Local only**, driven from synced state |

- Fire cells sync as compact deltas (changed cells only) a few times per second, with a full snapshot for late joiners.
- Hits between players (water knockback, axe, truck) are worked out by whoever owns the cause and sent to the victim's owner as an RPC, and the owner applies the impulse.
- Prefer `NetworkVariable`s for slow state and custom/named messages for bulk data (fire deltas, voice).

## How to run a 2-player test

**Same machine (local transport, the fastest loop):**
1. Open `Window > Multiplayer > Multiplayer Play Mode` and enable Player 2 (if that package is installed; otherwise use a second build).
2. Enter Play Mode. In the main editor's menu, choose **Transport: Local** → **Host**.
3. In the Player 2 window, choose **Transport: Local** → **Join** `127.0.0.1`.

**Steam (real lobbies, invites and voice):** this needs two PCs with **two different Steam accounts**, both with Steam running.
1. `Tools/Firehouse/Build/Windows Dev Build` (or File > Build). `steam_appid.txt` (480) must sit next to the exe.
2. PC A: run the build → **Host** → **Invite Friends** (Steam overlay, Shift+Tab), or read out the 6-letter code.
3. PC B: accept the invite, or type the code into **Join with code**.

## Workflow

- Work in small, testable steps. After each step, explain how to test it, including in 2-player mode.
- After building anything visual, take a screenshot through the bridge and check it before calling it done.
- Commit after each working step with a clear message.
- Keep this file updated when structure, conventions or test steps change.

## Status

Milestone 1 (prototype) is in progress: Steam lobby, Smokies controller, proximity voice, starter truck, hose, basic fire grid. These are **not** built yet, but the code should be structured so they slot in later: radio dispatch, economy, levels, rescues, other fire types, intermission calls, the ladder truck.
