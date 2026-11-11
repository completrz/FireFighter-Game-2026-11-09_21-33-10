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
    Audio/        proximity voice, procedural placeholder sounds
    World/        procedural terrain/road generation, level helpers
    UI/           menus, join-code entry, debug panel UI
  Editor/         editor-only scripts: Tools/Firehouse/* menu items, scene/prefab builders
  Tests/
    EditMode/
    PlayMode/
  Data/           ScriptableObject assets (FireMaterials, Agents, TruckStats, Upgrades, Levels, Tuning)
  Prefabs/        P_Player, …
  Resources/      NetworkRoot.prefab only (see "Startup" below)
  Scenes/         Firehouse (build index 0), later TestRoad, TestFire
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

## Startup and sessions

- `NetworkRootBoot` (runs before the first scene) instantiates `Resources/NetworkRoot.prefab`. That prefab holds the NetworkManager, both transports (UnityTransport for Local, `FacepunchTransport` for Steam), `SteamService`, `SteamLobbyService`, `SessionManager` and the prototype `MainMenu`. It is DontDestroyOnLoad, so **you can press Play in any scene**. Never put a NetworkManager in a scene.
- `SessionManager` is the only API for starting and stopping: `HostLocal`, `JoinLocal(ip)`, `HostSteam`, `JoinByCode`, `JoinSteamLobby`, `Leave`. The host then loads the "gameplay scene", which is the scene that was open at startup unless `_gameplayScene` is set.
- Players are **not** auto-spawned by NGO. Each scene has a `PlayerSpawner` (host only) that spawns `SessionManager.PlayerPrefab` at that scene's `SpawnPoint`s, with `destroyWithScene: true`.
- `SteamService` owns Steam's Init, RunCallbacks and Shutdown; the transport never shuts Steam down. Steam is skipped in Multiplayer Play Mode clones.
- Steam lobbies on App 480 are tagged with `firehouse_coop=proto1`. Bump `SteamLobbyService.ProtocolVersion` when builds stop being network-compatible.
- Input focus: menus and panels call `UIFocus.SetOpen`. Gameplay checks `UIFocus.IsBlocked`, and the local player sets `UIFocus.GameplayWantsCursorLock`.

## Generated content (Tools/Firehouse menu)

| Menu item | Makes |
| --- | --- |
| Build/Rebuild Everything | All of the items below, in order |
| Setup/Create Shared Material | `Art/Materials/M_VertexColor.mat` (shader `Firehouse/FlatVertexColor`) |
| Setup/Physics Layers | Layer names and the collision matrix from `Firehouse.Core.Layers` |
| Build/Player Prefab (Smokie) | Smokie meshes, `Prefabs/P_Player.prefab` and `Data/PlayerStats.asset`, and registers it in `Data/NetworkPrefabs.asset` |
| Build/Props | `Prefabs/Props/*` carryables (crate, big crate, ball, cone, rescue dummy, mailbox) |
| Build/Truck (Starter) | `Prefabs/P_Truck_Starter.prefab` and `Data/TruckStats_Starter.asset` |
| Build/Test Road Scene | `Scenes/TestRoad.unity`: procedural terrain and road, bridge, mailboxes, fences, the truck and spawns |
| Build/Network Root | `Resources/NetworkRoot.prefab` |
| Build/Firehouse Blockout Scene | `Scenes/Firehouse.unity` and sets it as build index 0 |
| Art/Setup Firehouse Import | Import settings for `Assets/Art/Firehouse/Firehouse.fbx` (exported from `ArtSource/Firehouse.blend`): colliders, Mikk tangents, lightmap UVs; extracts URP Lit materials to `Art/Firehouse/Materials` with albedo + normal maps from `Art/Firehouse/Textures` |
| Build/Firehouse Test Scene | Runs the import setup, then `Scenes/Firehouse_Test.unity`: the textured firehouse (static except doors, garage doors, spawn empties), warm sun with soft shadows, offline `SimpleFirstPersonController` at PlayerSpawn_01 |
| Render Concept Art | Stages a hand-posed key-art shot in a scratch scene and writes `docs/concept-art.png` (used by README) |
| Build/Art Test Scene | An unsaved scratch scene with shader test shapes |

Shader vertex colors: RGB is the albedo, and **alpha < 1 means emissive** (0 = full glow). `_GlowColor` (set by MaterialPropertyBlock) adds script-driven glow, and `_BaseColor` (set by MaterialPropertyBlock) tints, e.g. coat color.

## Player (Smokies)

- `SmokieController`: a rigidbody capsule (70 kg, frictionless, rotation frozen) simulated by its **owner**. States: Walking, Climbing, KnockedDown, GettingUp. Everyone else sees it through an owner-authority NetworkTransform plus NetworkRigidbody, so on non-owners it's kinematic.
- **Pushing players:** call `smokie.Knockback(velocityChange, forceKnockdown)` from anywhere. It forwards to the owner by RPC, and a velocity change ≥ `KnockdownImpulse` knocks them over. Hard landings knock players down too. This is the hook for hoses, axes and trucks.
- **Ladders and poles:** the `Ladder` component plus a trigger box on the Trigger layer. Its `transform.forward` points from the climber toward the ladder.
- **Grabbing:** `Grabbable` (host keeps the holder list, ownership goes to the first holder, max holders per object) and `PlayerGrabber` (hold LMB, RMB throws). Carrying is a spring with a strength cap, so heavy things need two people.
- **Cosmetics:** `SmokieArms` (local-only jointed floppy arms, detached into world space at runtime) and `SmokieFace` (swaps the eye mesh: normal, wide or X).
- Everything tunable lives in `Data/PlayerStats.asset` and shows up in the F1 panel.
- Controls: WASD, mouse, Shift sprint, Space jump, LMB hold to grab, RMB throw, E interact (reserved). Gamepad is bound too.

## Trucks

- `TruckController`: arcade raycast suspension. Each wheel does a spring/damper, sideways grip capped by load × friction, and drive/brake/rolling resistance. **Only the owner simulates it:** the host while parked, the driver while driving. Others see it through an owner-authority NetworkTransform plus NetworkRigidbody.
- The driver's controls (throttle, steer, horn, siren, lights) are owner-write NetworkVariables. `TruckEffects` (every client, local only) handles wheel placement and spin, procedural horn/siren/engine sounds (`Audio/ProceduralAudio`), and the lightbar flash.
- `TruckSeats`: the host owns the occupancy list (`NetworkList<ulong>`). Seat 0 is the driver, and taking it calls `HandOwnership(driver)`, which keeps the truck's motion. Leaving gives it back to the host. Seat kinds: Driver, Cab, BackStep (exposed: back step and hose bed).
- **Seated players:** `SmokieState.Seated`. Every client places a seated player on their seat in `LateUpdate` (looked up from `TruckSeats`, so it needs no extra sync). The collider is off while seated, and you can look ±150° around while seated.
- **Comedy** (on the simulating client): hard jolts or crashes throw back-step riders off (`TruckSeats.Eject`, then the host frees the seat and knocks the player back). Driving into players knocks them over, using an overlap check, because the truck ignores collisions with remote (kinematic) players. The truck also claims nearby loose `Grabbable`s so it bashes them aside.
- `TruckController.AutoDrive` (throttle, steer) is a test hook used by tools and tests instead of player input.
- Driving controls: WASD, Space handbrake (also an automatic parking brake when idle), H or LMB horn, F siren, L lights, R flip upright (when slow and upside down), E get in/out.
- `PlayerInteractor` and the `Interactable` base class: E uses whatever you're looking at (seats now, the pump panel next).

## World

- `ProceduralTerrain` (`[ExecuteAlways]`) generates hills, a Catmull-Rom dirt road with potholes, washboard, jump humps and a cattle grate, and a creek. **It is deterministic, and every client generates it locally** (the mesh is never saved). Builders use `SampleHeight`, `RoadPointAt` and `CreekCrossingDistance` to place things.

## Proximity voice

- `SmokieVoice` (on the player): the owner captures audio and sends packets with `[Rpc(SendTo.NotMe, Delivery = Unreliable)]`. Receivers decode them into that Smokie's head `VoicePlayback`, a 3D AudioSource with a streaming clip, a jitter buffer, linear falloff, and a low-pass plus volume drop when a wall (Default layer) blocks the line to the listener.
- **Codecs** (first byte of each packet): `Steam` (SteamUser record/compress) in Steam sessions, and `MuLaw12k` (Unity Microphone at 12 kHz μ-law, 50 ms packets) in Local sessions, so the voice works in MPPM clones without Steam. Everything plays back at 24 kHz.
- The talk grille glows from the speaking level (`_GlowColor` set by MaterialPropertyBlock).
- `Data/VoiceSettings.asset` (F1): open mic or push-to-talk (V / LB), mic gain, voice activation level, min/max distance, occlusion, jitter buffer, and **Loopback** (hear yourself; single-PC test).
- Later: a radio upgrade (whole-crew channel) can reuse `VoiceRpc` with a 2D playback source.

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

**Same machine (Local transport, Multiplayer Play Mode, the fastest loop):**
1. Open `Window > Multiplayer > Multiplayer Play Mode` and tick **Player 2**. The first time, wait for the clone to finish importing.
2. Open `Assets/_Project/Scenes/Firehouse.unity` and press Play.
3. Main editor: **Host local**. Player 2 window: **Join IP** with `127.0.0.1`.
4. Esc opens the in-game menu (Leave), and F1 opens the debug panel (network status).

**Steam (real lobbies, invites and voice):** this needs two PCs with **two different Steam accounts**, both with Steam running.
1. File > Build Profiles > Windows > Build into `Builds/` (git-ignored). Copy `steam_appid.txt` (contains `480`) next to the exe.
2. PC A: run the build (or press Play in the editor) → **Host (Steam lobby)** → Esc → **Invite friends**, or read out the 6-letter code.
3. PC B: accept the invite in Steam, or type the code → **Join with code**.
Steam shows the game as "Spacewar" while we're on App ID 480. That's expected.

## Workflow

- Work in small, testable steps. After each step, explain how to test it, including in 2-player mode.
- After building anything visual, take a screenshot through the bridge and check it before calling it done.
- Commit after each working step with a clear message.
- Keep this file updated when structure, conventions or test steps change.

## Status

Milestone 1 (prototype) is in progress: Steam lobby, Smokies controller, proximity voice, starter truck, hose, basic fire grid.
- Done: step 0 (setup), step 1 (Steam/local session, lobby + join codes, firehouse blockout), step 2 (Smokies controller, arms, ladders/pole, grab and carry, props).
- Done: step 3 (proximity voice, Steam + local codecs), step 4 (starter truck, seats, back step, horn/siren/lights, TestRoad).
- Done: step 6 (grid fire: `Fire/` FireGridSim (host, unit-tested), FireGrid (voxelizes BurnableVolumes, syncs 1-byte cell states as deltas + snapshots), FireGridView (voxels, flames/smoke/steam, light, crackle), `FireSystem.ApplyAgentAt/IgniteAt` = the hose's entry point, data in `Data/Fire/`, `Scenes/TestFire.unity`, F1 → Fire ignite/douse buttons).
- Gotchas: scene builders must save with `ProjectAssets.SaveSceneWithNetworkIds` (else in-scene NetworkObjects have hash 0 and never spawn); mesh particles use per-effect materials with `_UseVertexColor = 0` (particle vertex colors arrived garbled).
- Next: step 5, the hose (reel on the truck's rear, max length, pressure from a pump panel `Interactable`, tank drain, kickback, water knockback on players).
- The host can switch scenes for everyone from F1 → Network.
- Branch: `prototype`. These are **not** built yet, but the code should be structured so they slot in later: radio dispatch, economy, levels, rescues, other fire types, intermission calls, the ladder truck.
