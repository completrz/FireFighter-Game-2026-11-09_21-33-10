# Firehouse Co-op — Game Design Doc

Nov 9, 2026 · @completrz

## Pitch and pillars

A 1–8 player online co-op game where a scrappy volunteer fire crew drives a beat-up engine to fires, argues over who holds the hose, and gets paid just enough to buy a better truck. It's a Steam friendslop game: cheap, chaotic, physics-driven, and funnier the worse you play.

**Comparables and what we borrow**

| Game | What we take |
| --- | --- |
| Peak | A lobby full of toys, physics bodies that make failure funny, proximity voice chat |
| R.E.P.O. | Carrying awkward physics objects as a team, a paycheck-and-shop loop between runs |
| RV There Yet | One shared vehicle everyone has to cooperate to drive, rough terrain as the comedy engine |
| Lethal Company | Short runs, quota pressure, a hub you return to between missions |

**Design pillars**

1. **One truck, many jobs.** Driving, hoses, ladder, rescue and tool choice are separate jobs that anyone can pick up at any time; proximity chat is how the crew sorts out who does what.
2. **Readable chaos.** Fire, smoke and water are exaggerated and physical. Failure should be loud and funny, never confusing.
3. **Pick the right tool.** Each fire type rewards a specific agent. Using water on a grease or electrical fire makes things worse in a spectacular way.
4. **People first.** Civilians inside are worth more than the building. Splitting the crew between rescue and suppression is the core tension.
5. **Upgrade the station.** Every paycheck visibly changes the firehouse and the fleet.

## Art direction and characters

**Stylized low-poly with tiling textures.** Chunky, simple shapes (low-poly, flat-shaded faces) dressed with a small library of tiling, hand-tuned surface materials: brick, plaster, concrete, asphalt, wood, painted metal, roof, grass, trim, glass. Each material is a tileable albedo plus normal map, and a vertex-color grime layer darkens corners, bases and edges, so a simple box still reads as a real worn building. The look is a warm, slightly toy-like real world: think a model railroad town, not a photoreal city.

- **Environments and vehicles** use the shared surface library (one material per surface type, reused across every asset) instead of unique textures per object. Trucks are glossy red painted metal with chrome, trim and a lightbar.
- **Characters, fire, smoke and water stay bold and simple.** Smokies, flames, smoke columns, water streams and anything the player must read in chaos use bright solid colors and emissive accents, so they pop against the textured world.
- **Readability rules:** no high-frequency detail on gameplay surfaces, saturated hazard colors (fire orange, hose yellow, reflective stripes) are reserved for gameplay, and texture contrast stays low so smoke and fire are always the brightest things on screen.
- **Lighting:** warm sun, soft shadows, baked light in the firehouse; missions at golden hour, dusk and night so fire is the main light source.
- Assets are modeled in Blender (`ArtSource/`), exported to FBX, and set up in Unity by editor builders (URP Lit materials with albedo + normal maps).

**The crew character ("Smokies")**

A firefighter built to be funny in physics and readable in smoke, distinct from Peak's scouts and R.E.P.O.'s robots.

- **Body:** short, rounded bean shape with stubby legs and long, floppy ragdoll arms, so carrying victims and falling off ladders look clumsy.
- **Head:** fully hidden inside an oversized helmet and a round breathing mask. The face is two big round eye lenses that show simple expressions: happy arcs, squints, wide panic, X's when downed, swirls when dizzy.
- **Talking:** a small grille on the mask lights up when the player speaks, so you can see who's talking in proximity chat.
- **Air tank on the back:** visibly empties as the air meter drains, so teammates can see who needs to get out.
- **Coat color and helmet number:** each player picks a coat color and gets a helmet number 1–8. Reflective stripes glow in smoke.
- **Soot:** characters get blacker over a run. Getting hosed by a teammate washes it off, which gives friendly fire a use.
- **Budget:** roughly 1,500–3,000 triangles per character, one simple solid-color material (coat color as a tint), eye expressions as swappable decals.

## The firehouse lobby

The lobby is a two-story firehouse that players run around in while waiting for friends, and it doubles as the hub between missions. Everything in it should be physics-based and playable with 1–4 people.

**Layout**

- **Apparatus bay (ground floor):** the truck(s) parked here. Climb in, honk, run the siren, practice the ladder. The bay door opens to start a mission.
- **Kitchen and day room:** couch, TV that plays the last run's replay or a bloopers reel, a fridge of throwable food.
- **Bunk room (upstairs):** cosmetics locker and outfit mirror.
- **Fire pole:** slide from upstairs to the bay. Instant classic, costs almost nothing to build.
- **Back lot:** training yard with the bigger toys.
- **Captain's office:** the mission board and the upgrade shop terminal.

**Interactive toys (the Peak equivalents)**

| Toy | Where | Why it's fun |
| --- | --- | --- |
| Hoop on the bay wall | Apparatus bay | Classic firehouse driveway basketball, easy to build |
| Hose target practice | Back lot | Knock cans or spin a target with water pressure; teaches hose controls |
| Training tower climb | Back lot | Ladder and rope climbing race, teaches climbing |
| Dummy carry race | Back lot | Carry a rescue dummy through a course, teaches rescue |
| Chili cook-off stove | Kitchen | Overcook it and start a small grease fire, a safe extinguisher tutorial |
| Station dog | Everywhere | Pettable, follows players, steals hats |
| Poker table and slot machine | Day room | Bet personal cash between runs (see Soft gambling) |
| Fire pole | Bunk room | Slide race, stacking players on the pole |
| Bell and PA system | Bay | Ring the alarm bell or talk over the PA to everyone |

Most toys double as tutorials, so new players learn hoses, ladders, carrying and extinguishers before their first fire.

**Inviting players**

- Host clicks **Play**, which creates an 8-player Steam lobby instantly. No server browser required.
- **Invite Friends** button opens the Steam overlay invite list; friends can also right-click the host in Steam and choose Join Game.
- Show a **short lobby code** (e.g. 6 letters) for Discord friends who aren't on the host's Steam friends list.
- Late joiners drop in through the front door of the firehouse while others are playing.
- Optional public lobbies with a simple browser later, after launch.

## Core round loop

Each round is one call: leave the firehouse, drive, fight the fire and rescue people, get paid, upgrade, repeat.

&#91;embedded content: core loop · 5 steps, repeats until the last level\]

The drive is part of every round, not a loading screen. Arriving fast but wrecked costs money, so the driver matters as much as the hose crew.

**Radio dispatch**

Players get mission information from a radio, not a menu or HUD. The dispatcher is a voiced character with a personality, and whoever hears the radio has to pass the details on to everyone else.

- **The call:** alarm tones sound in the firehouse, then the dispatcher reads the address, what's burning, how many people are reported, and hazards ("caller says two kids upstairs, power lines down in the yard").
- **Proximity matters:** the radio is a physical speaker in the firehouse and the truck cab. Players across the room hear it faintly or not at all, so someone has to relay it over voice chat.
- **Call ticket:** the station printer spits out a paper ticket with the address and key details. It's a physical object someone can grab, carry and lose.
- **Information can be wrong:** callers miscount people, miss a pet, or misreport the fire type. Players still have to search and check.
- **Mid-mission updates:** "neighbor says there's a dog in the garage", "second floor is going to go", "gas company is five minutes out".
- **Directions:** at first there's no GPS. The dispatcher gives turn-by-turn directions and there's a paper map in the cab. A GPS screen is an upgrade.
- **Handheld radio upgrade:** hear dispatch anywhere on scene and talk to the whole crew at any distance.

## Trucks and driving

The truck is the heart of the game: everyone rides it, it carries every tool, and losing control of it should be the funniest part of any run.

**Fleet**

| Truck | Unlock | Crew seats | What it adds |
| --- | --- | --- | --- |
| Starter mini-pumper | Start | Driver + 3 riding on the back step and cab | Small water tank, 1 hose reel, 1 extinguisher, axe, short ground ladder |
| Standard engine | Upgrade | Driver + 3 seated | Bigger tank, 2 hose lines, hydrant hookup, tool compartments |
| Tiller ladder truck | Late upgrade | Front driver, rear tiller steerer, ladder operator | Rear-wheel steering from the back, aerial ladder with turntable |
| Foam / hazmat unit | Late upgrade | Driver + 3 | Foam tank, CO2, dry chemical, sand, and radiation gear for the reactor level |

The tiller truck is the signature co-op vehicle: the front driver and the rear steerer have to talk through every turn, or the truck jackknifes.

**Big crews (up to 8):** seats are limited on purpose. Extra players ride the back step, cling to the ladder or sit on the hose bed, and can fall off on bumps. Fire size, victim count and payouts scale with the number of players so 2 and 8 both feel right.

**Truck interactions**

- **Hoses:** grab a line from the reel, drag it (it has real length and snags on corners), connect to the truck outlet or a hydrant, then open the valve.
- **Pump panel:** one player sets water pressure. High pressure reaches farther but the nozzle kicks and can knock the hose holder over. Two players on one big line stay steady.
- **Tank vs hydrant:** the truck tank runs dry fast. Hooking a hydrant gives unlimited water but pins the truck in place.
- **Ladder:** a seat-based control panel to rotate, raise and extend. Players climb it with the normal climb controls, carry victims down it, or spray from the tip.
- **Siren, horn, lights:** the driver controls these. Siren makes AI traffic pull over (mostly). The horn is just for fun.
- **Compartments:** physical storage. Players open a door and grab a tool; tools must be returned or are lost at the end of the run.

**Terrain and obstacles by tier**

| Tier | Terrain | Obstacles |
| --- | --- | --- |
| Rural | Bumpy dirt roads, potholes, cattle grates, a narrow bridge | Mailboxes, fences, livestock, players falling off the back step |
| Suburban | Speed bumps, tight cul-de-sacs, parked cars | Kids' bikes, garbage cans, a car that won't pull over |
| City | Traffic, one-way streets, construction zones | Moving cars, pedestrians, double-parked trucks, blocked lanes needing a detour |
| Industrial | Gravel yards, rail crossings, gates | Trains, forklifts, debris from the incident |

Hitting pedestrians should never be possible to do for laughs: they dive out of the way, and close calls cost a small reputation penalty on the paycheck.

## Fire system

Fires are simulated on a grid of burnable cells: each object has a material, a heat value and a fire class, and fire spreads to neighbors based on heat, material and airflow. The right agent knocks a fire down fast; the wrong one does nothing or makes it worse.

**Fire classes and agents** (based loosely on the real US classes, simplified for play)

| Fire type | Example in game | Best agent | Wrong agent and what happens |
| --- | --- | --- | --- |
| Ordinary (wood, paper, cloth) | Brush, sheds, furniture | Water | — |
| Flammable liquid | Gas station, car, fuel tank | Foam, dry chemical | Water spreads the burning liquid around |
| Electrical | Breaker box, transformer, server room | CO2, dry chemical; or cut the power first | Water shocks the hose holder and arcs |
| Grease / kitchen | Restaurant, house kitchen | Wet chemical extinguisher, lid / fire blanket | Water causes a fireball |
| Metal / chemical | Factory, warehouse | Dry powder, sand | Water causes small explosions |
| Reactor core | Nuclear plant level | Boron and sand drops, cooling systems | Water does nothing useful and raises radiation |

**Tools and agents**

- Hose lines (water, later foam)
- Handheld extinguishers: water, CO2, dry chemical, wet chemical
- Fire blanket, sand buckets
- Axe and halligan bar for doors, walls and vent holes
- Power shutoff at the breaker box or utility pole (turns electrical fires into ordinary ones)
- Gas shutoff valve (stops a flammable source from re-igniting)

**Hazards that make fires interesting**

- **Smoke:** builds up and blinds players indoors. Breaking a window or venting the roof clears it, but feeds the fire oxygen.
- **Backdraft:** opening a door to a sealed room full of smoke causes a blast. Feel the door first (it glows hot).
- **Collapse:** floors and roofs weaken as they burn; a creaking sound and visible sag warn players.
- **Explosive objects:** propane tanks, cars, gas cylinders. Cool them or move them.
- **Heat and oxygen:** players have a heat meter and an air tank. Upgrades extend both.
- **Downed players:** a burned or knocked-out player can be dragged out and revived by teammates, R.E.P.O.-style.

**Friendly fire is on.** A high-pressure hose knocks teammates over or off ladders, an axe swing can down a teammate, extinguishers blast them backward, and the driver can run the crew over. Downed teammates are revived, never killed outright, so it stays funny instead of punishing.

## Rescue system

Civilians are the highest-value objective in any level: each one saved pays more than the property saved, and losing one cuts the paycheck hard. This forces the crew to split between rescue and suppression.

**Civilian types**

| Type | Behavior | How to rescue |
| --- | --- | --- |
| Walker | Panicked but mobile | Find them and they follow you out |
| Hider | Under beds, in closets; calls for help | Listen for voices, search rooms, then lead or carry |
| Unconscious | Collapsed from smoke | Carry or drag (heavy; two players carry faster) |
| Trapped | Stuck on an upper floor or behind debris | Ladder truck, axe through a wall, or a rope |
| Pet | Cat, dog | Small bonus, pure comedy |
| Stubborn | Refuses to leave without their stuff | Grab the item (TV, cat, trophy) and they follow |

**Rescue mechanics**

- Victims are physics bodies, carried R.E.P.O.-style: clumsy, can be dropped, can bonk on door frames.
- A **thermal camera** upgrade shows victims through smoke and walls.
- Victims have a **health timer** that drains faster in smoke and heat, shown as a pulsing icon when you're close.
- Saved victims are handed to an **ambulance zone** near the truck; dropping them anywhere else outside counts but pays less.

**No classes or roles**

Every player has the same abilities and can do every job: drive, run the pump, work the ladder, carry victims, swing the axe. Nobody picks a class. Players settle into jobs on their own by talking in proximity chat, and swap whenever they want.

## Level progression

Launch with 10 levels. Levels 1–5 are built first, each introducing one new idea; levels 6–10 are built once the core game is done, and all 10 ship at launch. Each level should take 10–20 minutes including the drive.

| # | Level | Setting | New idea it teaches | Victims | Truck needed |
| --- | --- | --- | --- | --- | --- |
| 1 | Brush fire | Rural field and a barn, bumpy dirt road | Driving, hoses, tank runs dry | 0 (livestock bonus) | Starter |
| 2 | House fire | Suburban two-story home | Going inside, smoke, rescue, kitchen grease fire | 2–3 | Starter |
| 3 | Strip mall | Restaurant, electronics store, gas pumps out front | Fire types: grease, electrical, flammable liquid; power and gas shutoffs | 4–6 | Standard engine recommended |
| 4 | City apartment block | 5-story apartment plus spread to neighbors, city traffic | Ladder rescues, multi-building spread, hydrants, moving traffic | 8–12 | Tiller ladder truck recommended |
| 5 | Reactor meltdown | Soviet-era nuclear power plant at night | No-water fire, radiation, multi-phase objective | Plant workers | Foam / hazmat unit |

**Level 5: the reactor**

The finale breaks the normal rules: water is useless on the core and players must work in short shifts because of radiation.

1. **Arrive and evacuate.** Find and pull out plant workers from the turbine hall and control room. Dosimeters start ticking.
2. **Knock down the roof fires.** Burning debris on nearby roofs threatens the other reactors. Players climb up with dry chemical and sand and push debris off.
3. **Smother the core.** Load boron and sand bags into a crane or helicopter drop rig and drop them on the exposed core. One player spots, one operates the rig, others load.
4. **Cool and seal.** Restart backup pumps in the flooded basement (dark, tight corridors) and close the containment doors.

**Radiation rules**

- Each player has a dose meter that fills faster near the core. Full meter = downed, and must be carried out.
- Players rotate in and out, which forces the whole crew to stay busy.
- Lead-lined suits and a shielded truck cab are upgrades bought before attempting the level.

A fictional plant name (rather than Chernobyl itself) avoids depicting a real disaster with real victims and gives the design freedom to invent the layout; the setting can still clearly evoke it.

**Levels 6–10 (built second, ship at launch)**

| # | Level | Setting | New idea it adds | Victims | Truck needed |
| --- | --- | --- | --- | --- | --- |
| 6 | Hillside wildfire | Fire sweeping toward a small town, shifting wind | Firebreaks, evacuating a whole street, fire that moves | 15+ | Standard engine or tiller |
| 7 | Airport fuel fire | Burning jet and fuel trucks on the runway | Huge flammable-liquid fire, foam, passengers on the plane | 10–15 | Foam / hazmat unit |
| 8 | High-rise | 20-story office tower | Stairwells, standpipe hookups, ladder only reaches the lower floors | 15–20 | Tiller ladder truck |
| 9 | Chemical plant | Tanks, pipes and a warehouse | Metal and chemical fires, toxic clouds, closing valves | Workers | Foam / hazmat unit |
| 10 | Refinery blaze | Wide tank farm across several blocks | Chain explosions, multi-site fire, splitting the crew across the map | Workers | Whole fleet |

Campaign order follows difficulty, so the reactor can move to the final slot once levels 6–10 exist. Endless mode with randomized fires comes after launch.

**Intermission calls**

Short, silly, low-stakes calls of 3–5 minutes between main levels. The radio offers 1–2 random ones between each main level so every run is different. They pay a small bonus, give the crew a breather, and practice one mechanic each. Players can skip them.

| Call | What happens | Mechanic it practices |
| --- | --- | --- |
| Cat in a tree | The cat climbs higher as you approach; catch it with a blanket held by several players or reach it with the ladder | Ladder, teamwork |
| Backyard barbecue fire | A grill flare-up at a party with guests in the way | Grease fire, wet chemical extinguisher |
| Open hydrant | Kids opened a hydrant and the street is flooding; shut it off while they run away | Hydrant tools, water pressure |
| Stuck elevator | People trapped between floors in an office building | Prying doors, rescue |
| Car fire on the highway | A burning car on the shoulder with traffic flying past | Driving in traffic, flammable liquid fire |
| False alarm | Burnt toast. The homeowner is embarrassed. Maybe a little real fire in the toaster | Quick in-and-out, comedy |
| Raccoon in the chimney | Get it out without wrecking the house | Climbing roofs, carrying |
| Kite guy on a roof | Someone climbed up for their kite and can't get down | Ladder rescue |

Intermission calls are cheap to build because they reuse existing maps and systems, so they are a good way to make the campaign feel bigger.

## Economy and upgrades

Most of the paycheck goes into a shared crew fund spent together in the captain's office, so arguing over what to buy is part of the fun. Each player also keeps a small personal cut for cosmetics and gambling. Numbers below are starting values to tune in playtests.

**Paycheck breakdown** (shown as a funny itemized pay stub after each run)

| Line item | Starting value |
| --- | --- |
| Base pay for the call | $500 × level number |
| Each civilian saved | +$300 |
| Each civilian lost | −$500 |
| Property saved | Up to +$1,000, scaled by % of building standing |
| Speed bonus | +$200 if under par time |
| Damages | − cost of mailboxes, cars and fences hit on the way |
| Lost equipment | − cost of tools left behind |

Failing a level (too many victims lost or the building fully burns) still pays a small amount, and the crew can retry.

**Upgrade tree**

| Category | Examples |
| --- | --- |
| Trucks | Standard engine, tiller ladder truck, foam / hazmat unit |
| Truck parts | Bigger tank, stronger pump, better suspension, longer ladder, louder siren |
| Hoses and agents | Longer hose, second line, foam concentrate, CO2 and dry chemical extinguishers |
| Personal gear | Bigger air tank, heat-resistant suit, lead-lined suit, thermal camera, radio range |
| Tools | Halligan bar, chainsaw, rope kit, fire blanket, jaws of life |
| Station | Firehouse decorations, new lobby toys, garage bays for more trucks |
| Cosmetics | Helmets, uniforms, truck paint jobs, station dog outfits |

Cosmetics should be earned in game only, never sold, to keep the friendslop goodwill.

**Soft gambling (in-game cash only)**

| Game | Where | Stakes |
| --- | --- | --- |
| Slot machine | Day room | Personal cash; wins pay cash or rare cosmetics |
| Poker / blackjack table | Day room | Players bet personal cash against each other |
| Scratch-off tickets | Firehouse vending machine | Cheap tickets, small chance at a big payout |
| Mission bets | Mission board, before a run | Crew votes to bet part of the paycheck on a goal (no victims lost, under par time) for a multiplied payout, or lose it |
| Mystery supply crates | Shop | Random tool, upgrade part or cosmetic |
| Hose-off side bets | Back lot | Bet on who wins a training-yard race |

Rules: no real money in or out, nothing bought with real money, and the shared crew fund can only be gambled through a crew vote. Simulated gambling can raise the age rating in some regions, so check rating rules before launch.

**End state:** the campaign ends after the final level, showing a career summary (lives saved, trucks crashed, worst moment). Then unlock a harder "Captain" difficulty and endless mode.

## Multiplayer, Steam and tech

Use host-based peer-to-peer over Steam's relay network: no dedicated servers to pay for, friends join through Steam, and NAT problems are handled by Steam.

**Engine and stack: Unity**

- **Unity 6 LTS** with URP; low-poly flat-shaded meshes with a shared library of tiling textured URP Lit materials
- **Netcode for GameObjects** with a Steam transport, host-based peer-to-peer
- **Facepunch.Steamworks** for lobbies, invites, Rich Presence, achievements and voice
- **In-game proximity voice:** Steam voice captured per player and played through a 3D audio source on their character's head, so volume falls off with distance and walls muffle it. A radio upgrade lets you reach the whole crew.

Unity is chosen because Claude Code will write most of the code: C# scripts and editor scripts that build scenes are plain text it can create and change, and Unity has the most friendslop-style examples to learn from.

**Steam features to use**

- Steam lobbies for host and join, Rich Presence so "Join Game" works from the friends list
- Steam Networking Sockets relay for P2P traffic
- In-game proximity voice chat, no Discord needed
- Achievements ("Saved the cat", "Jackknifed the tiller truck", "Water on a grease fire")
- Steam Cloud for save files; Remote Play Together as a free bonus

**What needs syncing, and who owns it**

- **Host-authoritative:** fire grid state, civilians, truck physics, the paycheck. Send fire cells as compressed changes, a few times a second.
- **Client-owned:** each player's own movement and the hose nozzle they hold, with the host validating.
- **Local only:** water particles, smoke visuals, sparks. Every client simulates its own pretty effects from the synced state.

The truck is the hardest thing to network well: give the driver client authority over the truck while they drive, so steering feels instant.

## Scope, milestones and open questions

Build the smallest fun version first: one truck, one hose, one house fire, two players over Steam. If that isn't fun with friends, nothing else will fix it.

**Milestones**

1. **Prototype:** Smokies character controller with ragdoll arms, a drivable starter truck on a bumpy road, one hose with pressure, a basic fire grid. Two players over Steam with proximity voice.
2. **Vertical slice:** firehouse lobby with 3 toys and the invite flow, Level 2 (house fire) with smoke and rescue, the paycheck screen, 8-player lobbies.
3. **Core content:** Levels 1–3, all fire types and extinguishers, the shop with the first \~15 upgrades, the standard engine, friendly fire, radio dispatch, the first 4 intermission calls.
4. **Big trucks:** the tiller ladder truck, Level 4, city traffic and pedestrians.
5. **Reactor:** Level 5, radiation, the hazmat unit.
6. **Soft gambling and polish:** personal cash, slots, poker, mission bets, cosmetics.
7. **Levels 6–10:** wildfire, airport, high-rise, chemical plant, refinery, plus the remaining intermission calls.
8. **Steam launch prep:** store page and wishlist push early (right after the vertical slice), a demo or playtest, achievements, bug fixing.

Launch with all 10 levels, then add endless mode and new levels in free updates.

**Biggest risks**

- **Fire simulation over the network** is the hardest technical problem. Prototype it first.
- **Vehicle physics that feel fun**, not frustrating, especially the tiller truck.
- **Scope creep:** five well-polished levels beat twelve rough ones.

**Open questions**

**Decided:** up to 8 players · stylized low-poly with tiling textures · friendly fire on · Unity · no classes · in-game proximity chat · soft gambling with in-game cash.

- [ ] Game title
- [ ] Team size and who does art and sound
- [ ] Real or fictional name for the reactor plant
