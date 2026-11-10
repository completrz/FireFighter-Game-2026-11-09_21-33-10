# Firehouse Co-op

![Concept art: a Smokie crew fighting a suburban house fire at dusk](docs/concept-art.png)

*Concept render of the finished game: a Level 2 house fire. One Smokie hoses the downstairs window (and has just flattened a teammate), one climbs the ladder to the upstairs window, one carries a civilian out the front door, one runs in with an extinguisher, and the cat is still on the roof. Staged in Unity with the game's own models (`Tools/Firehouse/Render Concept Art`).*

**A 1–8 player online co-op firefighting game for Steam.** A scrappy volunteer fire crew drives a beat-up engine to fires, argues over who holds the hose, and gets paid just enough to buy a better truck. It's a friendslop game: cheap, chaotic, physics-driven, and funnier the worse you play.

The full design lives in [`docs/GDD.md`](docs/GDD.md). This README is the overview.

---

## Pillars

1. **One truck, many jobs.** Driving, hoses, the ladder, rescue and tool choice are separate jobs anyone can pick up at any time. Proximity chat is how the crew sorts out who does what.
2. **Readable chaos.** Fire, smoke and water are exaggerated and physical. Failure should be loud and funny, never confusing.
3. **Pick the right tool.** Each fire type rewards a specific agent. Water on a grease or electrical fire makes things worse, spectacularly.
4. **People first.** Civilians are worth more than the building. Splitting the crew between rescue and suppression is the core tension.
5. **Upgrade the station.** Every paycheck visibly changes the firehouse and the fleet.

**Borrowing from:** *Peak* (a lobby full of toys, physics failure, proximity voice), *R.E.P.O.* (clumsy team carrying, paycheck-and-shop loop), *RV There Yet* (one shared vehicle, rough terrain as the comedy engine) and *Lethal Company* (short runs, quota pressure, a hub between missions).

## Rules that never change

- **No classes or roles.** Everyone can drive, run the pump, work the ladder, carry victims and swing the axe.
- **Friendly fire is on.** Hoses knock teammates over, axes down them, the truck runs them over. Downed players are always revived, never killed for good.
- **Proximity voice chat is core.** You hear people near you, muffled by walls.
- **Mission info comes from a radio**, not a HUD.
- **Gambling uses in-game cash only.** No real money anywhere, and cosmetics are never sold.

## The Smokies

The crew are **Smokies**: short bean-shaped firefighters with oversized helmets and long floppy ragdoll arms, so carrying victims and falling off ladders look clumsy.

- The face is a round breathing mask with two big eye lenses that show expressions: happy, squinting, wide panic, X's when downed, swirls when dizzy.
- A small grille on the mask lights up when that player talks.
- The air tank on the back visibly empties.
- Each player gets a coat color and a helmet number from 1 to 8, and reflective stripes glow in smoke.
- Smokies get sootier over a run. Getting hosed by a teammate washes it off, so friendly fire has a use.

## Core loop

```
 Firehouse lobby ──► radio call ──► drive to the fire ──► fight fire + rescue ──► paycheck ──► upgrades ──┐
        ▲                                                                                                  │
        └──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

The drive is part of every round, not a loading screen. Arriving fast but wrecked costs money, so the driver matters as much as the hose crew.

## The firehouse (lobby and hub)

The firehouse is a two-story building you run around in while friends join, and it's the hub between missions. Everything in it is physical.

| Area | What's there |
| --- | --- |
| Apparatus bay | The trucks. Climb in, honk, run the siren, practise the ladder. The bay door opens to start a mission. Basketball hoop and alarm bell/PA. |
| Kitchen and day room | Couch, a TV replaying the last run's bloopers, a fridge of throwable food, a chili stove that starts a safe grease-fire tutorial, poker table and slot machine. |
| Bunk room | Cosmetics locker, outfit mirror, and the **fire pole** down to the bay. |
| Captain's office | Mission board and upgrade shop terminal. |
| Back lot | Training tower climb, hose target practice, rescue-dummy carry race. |
| Everywhere | The station dog, which follows players and steals hats. |

Most toys double as tutorials. **Inviting friends:** Host creates an 8-player Steam lobby instantly. Friends join through the Steam overlay or by right-clicking you in Steam, or with a 6-letter code for Discord friends. Late joiners walk in through the front door.

## Radio dispatch

A voiced dispatcher with a personality reads out the address, what's burning, how many people are reported and the hazards. The radio is a **physical speaker** in the firehouse and the truck cab, so whoever hears it has to relay it. A printer spits out a paper call ticket you can carry, and lose. Callers miscount people and misreport fire types, and mid-mission updates come in ("neighbor says there's a dog in the garage"). At first there's no GPS: the dispatcher gives turn-by-turn directions and there's a paper map. A GPS screen and handheld radios are upgrades.

## Trucks and driving

The truck is the heart of the game: everyone rides it, it carries every tool, and losing control of it should be the funniest part of any run.

| Truck | Unlock | Crew | Adds |
| --- | --- | --- | --- |
| Starter mini-pumper | Start | Driver + 3, extras on the back step | Small tank, 1 hose reel, extinguisher, axe, short ladder |
| Standard engine | Upgrade | Driver + 3 seated | Bigger tank, 2 lines, hydrant hookup, compartments |
| **Tiller ladder truck** | Late upgrade | Front driver, rear tiller steerer, ladder operator | Rear steering from the back and an aerial ladder. The drivers must talk through every turn or it jackknifes. |
| Foam / hazmat unit | Late upgrade | Driver + 3 | Foam, CO2, dry chemical, sand, radiation gear |

**What you do with the truck:**
- Drag hose lines that have real length.
- Set pressure at the pump panel. High pressure reaches further but kicks the holder over.
- Choose between the tank, which runs dry fast, and a hydrant, which gives unlimited water but pins you in place.
- Work the ladder controls.
- Siren, horn and lights.
- Tools in physical compartments that must be returned or are lost.

Riders on the back step and hose bed fall off on bumps.

**Roads by tier:**
- **Rural:** potholes, cattle grates, a narrow bridge.
- **Suburban:** speed bumps, cul-de-sacs, a car that won't pull over.
- **City:** traffic, one-way streets.
- **Industrial:** rail crossings, forklifts.

Pedestrians always dive out of the way, and close calls cost reputation.

## Fire

Fire is simulated on a grid of burnable cells. Each cell has a material, heat, fuel and wetness. Fire spreads to neighbors based on heat, material and wind, climbs upward, and burns through walls. The right agent knocks it down fast, and the wrong one does nothing or makes it worse.

| Fire type | Example | Best agent | Wrong agent |
| --- | --- | --- | --- |
| Ordinary | Brush, sheds, furniture | Water | — |
| Flammable liquid | Gas station, cars | Foam, dry chemical | Water spreads the burning liquid |
| Electrical | Breaker box, transformer | CO2, dry chemical, or cut the power | Water shocks the hose holder |
| Grease | Kitchens | Wet chemical, lid, fire blanket | Water causes a fireball |
| Metal / chemical | Factory, warehouse | Dry powder, sand | Water causes small explosions |
| Reactor core | The finale | Boron and sand drops, cooling | Water raises radiation |

**Tools:** hose lines (water, later foam), extinguishers (water, CO2, dry chemical, wet chemical), fire blanket, sand buckets, axe and halligan bar, power and gas shutoffs.

**Hazards:**
- Smoke that blinds you indoors. Venting it feeds the fire.
- Backdrafts: feel the door first.
- Floors that sag and collapse.
- Explosive propane tanks and cars.
- Heat and air-tank meters.
- Downed players who must be dragged out and revived.

## Rescue

Civilians are the highest-value objective: each one saved pays more than the property, and losing one cuts the paycheck hard.

| Type | Behavior |
| --- | --- |
| Walker | Panicked but follows you out |
| Hider | Under beds, in closets. Listen for them. |
| Unconscious | Carry or drag. Heavy, so two players carry faster. |
| Trapped | Upper floors or behind debris. Use the ladder, an axe or a rope. |
| Pet | Small bonus, pure comedy |
| Stubborn | Won't leave without their TV, cat or trophy |

Victims are clumsy physics bodies with a health timer that drains faster in smoke. Hand them to the ambulance zone for full pay. A thermal camera upgrade sees through smoke and walls.

## Levels

Ten levels at launch, each 10–20 minutes including the drive, each teaching one new idea.

| # | Level | New idea |
| --- | --- | --- |
| 1 | Brush fire: rural field and barn | Driving, hoses, the tank running dry |
| 2 | House fire: suburban two-story | Going inside, smoke, rescue, a kitchen grease fire |
| 3 | Strip mall | Grease, electrical and liquid fires, power and gas shutoffs |
| 4 | City apartment block | Ladder rescues, spread to neighbors, hydrants, traffic |
| 5 | Reactor meltdown (fictional plant) | No-water fire, radiation shifts, a multi-phase objective |
| 6 | Hillside wildfire | Firebreaks, evacuating a street, fire that moves |
| 7 | Airport fuel fire | A huge liquid fire, foam, passengers on a plane |
| 8 | High-rise | Stairwells, standpipes, a ladder that only reaches the lower floors |
| 9 | Chemical plant | Metal and chemical fires, toxic clouds, valves |
| 10 | Refinery blaze | Chain explosions, splitting the crew across the map |

**Intermission calls** are short, silly, skippable 3–5 minute calls between levels that reuse existing maps:
- cat in a tree
- backyard barbecue fire
- kids opening a hydrant
- a stuck elevator
- a car fire on the highway
- a false alarm (burnt toast)
- a raccoon in the chimney
- the kite guy on the roof

After launch: endless mode with random fires, a harder "Captain" difficulty, and new levels in free updates.

## Economy and upgrades

Every run pays an itemized pay stub:

| Item | Amount |
| --- | --- |
| Base pay | $500 × level |
| Each civilian saved | +$300 |
| Each civilian lost | −$500 |
| Property saved | up to +$1,000 |
| Under par time | +$200 |
| Damages | − mailboxes, fences and cars hit on the way |
| Lost tools | − cost of tools left behind |

Most of the pay goes into a **shared crew fund** spent together, and arguing over it is part of the fun. Each player keeps a small personal cut.

**Upgrades:**
- **Trucks:** the standard engine, tiller ladder truck and foam/hazmat unit.
- **Truck parts:** bigger tank, stronger pump, better suspension, longer ladder, louder siren.
- **Hoses and agents:** longer hose, a second line, foam concentrate, more extinguisher types.
- **Personal gear:** bigger air tank, heat and lead suits, thermal camera, radio range.
- **Tools:** halligan, chainsaw, rope kit, fire blanket, jaws of life.
- **Station:** decorations, new lobby toys, more garage bays.
- **Cosmetics:** earned in game only, never sold.

**Soft gambling (in-game cash only):**
- a slot machine
- poker and blackjack against each other
- scratch-off tickets
- mission bets the crew votes on
- mystery supply crates
- side bets on training-yard races

Nothing is bought with real money, and the crew fund can only be gambled through a crew vote.

## Multiplayer and tech

| | |
| --- | --- |
| Engine | Unity 6, Universal Render Pipeline, low-poly flat-shaded vertex-color art with one shared material |
| Networking | Netcode for GameObjects, host-based peer-to-peer over the Steam relay. No dedicated servers. |
| Steam | Facepunch.Steamworks: lobbies, invites, Rich Presence, achievements, Steam Cloud, proximity voice. Remote Play Together for free. |
| Input | New Input System, keyboard/mouse and gamepad |

**Who owns what:**
- **The host:** the fire grid, civilians, the paycheck, water tanks and pump pressure.
- **Each player:** their own movement and the nozzle they hold.
- **The current driver:** the truck, while driving, so steering feels instant.
- **Each machine on its own:** water particles, smoke and sparks, all drawn from the shared state.
- **Fire sync:** only changed cells, a few times a second.

## Roadmap

1. **Prototype:** Steam lobby, the Smokies controller, proximity voice, a drivable starter truck on a bumpy road, one hose with pressure, a basic fire grid. Two players over Steam.
2. **Vertical slice:** the firehouse lobby with 3 toys and the invite flow, Level 2 (house fire) with smoke and rescue, the paycheck screen, 8-player lobbies. *Steam store page and wishlist push right after.*
3. **Core content:** Levels 1–3, all fire types and extinguishers, the shop with about 15 upgrades, the standard engine, radio dispatch, the first 4 intermission calls.
4. **Big trucks:** the tiller ladder truck, Level 4, city traffic and pedestrians.
5. **Reactor:** Level 5, radiation, the hazmat unit.
6. **Gambling and polish:** personal cash, slots, poker, mission bets, cosmetics.
7. **Levels 6–10** and the remaining intermission calls.
8. **Launch prep:** demo or playtest, achievements, bug fixing.

**Biggest risks:** fire simulation over the network (prototyped first), vehicle physics that feel fun (especially the tiller truck), and scope creep: five polished levels beat twelve rough ones.

**Open questions:** the final game title, team size and who does art and sound, and the name for the fictional reactor plant.

---

## Development

- Open the project in **Unity 6** (see `ProjectSettings/ProjectVersion.txt`). Steam must be running for Steam features; test App ID 480 is used during development.
- `Tools/Firehouse/Build/Rebuild Everything` regenerates all generated prefabs, meshes and scenes.
- Test scenes are in `Assets/_Project/Scenes`: **Firehouse** (lobby), **TestRoad** (driving) and **TestFire** (fire and hose). Press Play in any of them, then click **Host local**.
- **Two players on one PC:** `Window > Multiplayer > Multiplayer Play Mode`, tick Player 2. Host local in one window, Join IP `127.0.0.1` in the other. Steam lobbies and voice need two PCs with two Steam accounts.
- **F1** opens the live tuning/debug panel.
- Code conventions, architecture and workflow for contributors (and Claude Code) are in [`CLAUDE.md`](CLAUDE.md).
