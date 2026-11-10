# Kickoff prompt for Claude Code — Firehouse Co-op

Before pasting:
1. Export the design doc as Markdown and save it in the project as `docs/GDD.md`.
2. Open the project in Unity and start the server in `Window > MCP for Unity`.
3. In Git Bash: `cd ~/FireFighterGame`, then run `claude` and check `/mcp` shows Unity.

Then paste everything below the line.

---

You are the lead programmer on a new Unity game. I'm the designer. You'll write almost all of the code, so set the project up in a way that you can keep working in over many sessions.

## Current setup

- This folder (`C:/Users/hinke/FireFighterGame`) is an existing Unity project created with the Universal 3D (URP) template. It's already a git repository.
- You're connected to the Unity Editor through the MCP for Unity bridge (`unityMCP`). Use it to build scenes, add components, set up prefabs and materials, read the console, run tests and take screenshots to check your work.
- **Important:** create folders, scripts and other files directly on disk inside the project, then refresh Unity through the bridge. Do NOT use the MCP `manage_asset` tool to create folders — it crashed the editor last time.
- I'm on Windows using Git Bash.

## The game

A low-poly, physics-driven online co-op firefighting game for Steam, in the "friendslop" genre (Peak, R.E.P.O., RV There Yet, Lethal Company). 1–8 players. A crew starts in a firehouse lobby, gets a call over the radio, drives a small fire truck over rough roads to the fire, puts it out with the right agent (water, foam, CO2, dry chemical, sand), rescues civilians, gets a paycheck, buys upgrades, and moves on to bigger fires. Short intermission calls (cat in a tree, barbecue fire, etc.) happen between main levels. The full design is in `docs/GDD.md` — read it before you start and treat it as the source of truth.

Key rules from the design:
- **No classes or roles.** Every player has the same abilities and can do every job (drive, pump panel, ladder, hose, axe, carry victims).
- **Friendly fire is on.** Hoses knock teammates over, axes can down them, the truck can run them over. Downed players are revived, never permanently killed.
- **In-game proximity voice chat** is core, not optional.
- **Mission info comes from a radio**, not a HUD: a voiced dispatcher heard through physical speakers in the firehouse and truck cab.
- **Up to 8 players** per lobby.
- **Low-poly, flat-shaded art like Peak.** Use primitives, ProBuilder and simple procedural meshes as placeholders; keep everything vertex-colored with one shared material where possible.
- **Soft gambling** uses in-game cash only. No real money anywhere.

## Tech stack (decided)

- Unity 6 LTS, Universal Render Pipeline
- Netcode for GameObjects, host-based peer-to-peer
- Facepunch.Steamworks for lobbies, invites, Rich Presence, achievements and voice, with the Facepunch transport for Netcode for GameObjects
- Use Steam's test App ID 480 during development until we have our own App ID
- C# only. New Input System.

## How I want you to work

1. Start by reading `docs/GDD.md`, then write a `CLAUDE.md` at the project root that summarizes the game, the stack, the folder structure, coding conventions, the folder-creation rule above, and how to run a 2-player test. Keep it updated as the project grows.
2. Propose a folder structure under `Assets/_Project/` (Scripts by feature: Networking, Player, Truck, Fire, Water, Rescue, Radio, Economy, Lobby, UI, Audio).
3. Use the MCP bridge for anything that needs the editor. For repeatable setup (rebuilding a test scene or prefab), also write **editor scripts** as menu items under `Tools/Firehouse/` so it can be redone with one click.
4. After building something visual, take a screenshot through the bridge and check it before telling me it's done.
5. Work in small, testable steps. After each step, tell me how to test it, including how to test multiplayer with two instances (ParrelSync or a second build).
6. Networking rules: the host owns the fire grid, civilians, the paycheck and economy. Each client owns its own movement and the nozzle it holds. The current driver gets authority over the truck while driving so steering feels instant. Water particles, smoke and sparks are local-only visuals driven by synced state.
7. Keep systems data-driven with ScriptableObjects (fire materials and classes, extinguishing agents, upgrades, levels, truck stats) so I can tune numbers without code changes. Add an in-game F1 debug panel with sliders for the main tuning values.
8. Make a sensible Unity `.gitignore` if there isn't one, and commit after each working step with a clear message.
9. Ask me before adding any paid asset or third-party package not listed above.

## First milestone: the prototype

Build only this for now:

1. **Steam + lobby:** host creates an 8-player Steam lobby, friends join through the Steam overlay invite or a short join code. Both players spawn in a blockout firehouse.
2. **Player character ("Smokies"):** a short bean-shaped low-poly firefighter, oversized helmet, round mask with two big eye lenses, long floppy physics arms. First-person or close third-person (propose which), walk, sprint, jump, climb ladders, grab and carry physics objects.
3. **Proximity voice:** Steam voice played through a 3D AudioSource on each player's head, volume falling off with distance. A small grille on the mask lights up while that player is talking.
4. **Starter truck:** a small drivable fire truck with arcade-feel raycast suspension. Seats for a driver and 3 passengers; extra players can stand on the back step and fall off on hard bumps. Horn, siren and lights controlled by the driver. A bumpy dirt road test map.
5. **Hose:** grab a hose from the truck reel, drag it (it has a max length), and spray water. A pump panel on the truck sets pressure: higher pressure reaches farther and has more kickback, enough to knock the holder over. Water from the truck tank runs out. Spraying a teammate knocks them back.
6. **Basic fire:** a grid-based fire simulation on a small test shed and a field of grass. Burnable cells have a material, heat and fuel; fire spreads to neighbors; water cools cells and puts them out. The host simulates it and syncs changed cells compactly a few times per second.

Don't build the radio dispatch, economy, levels, rescues, other fire types, intermission calls or the ladder truck yet, but structure the code so they slot in later.

When you're ready, start with step 1 of "How I want you to work" and show me your plan for the prototype before writing code.
