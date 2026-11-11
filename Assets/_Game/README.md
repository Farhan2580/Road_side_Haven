# Roadside Haven — first playable

Unity 6000.3.28f1 / URP 17.3.0. Package and Editor versions are preserved.

## Generate and play

1. Return to the existing Unity Editor and allow script import/compilation. Use Assets > Refresh if needed.
2. Run **Roadside Haven > Create or Update Prototype**. Save any unrelated scene if prompted. The command opens a new scene and writes only prototype output to `Assets/_Game/Generated/`. If TMP settings or a usable font are missing, it imports Unity's TMP Essential Resources and automatically resumes generation after the import finishes. Wait for the completion message before pressing Play. No Inspector wiring is needed.
3. Press Play in a landscape Game view (16:9 or 20:9). After two seconds the first guest arrives; allow them to reach their reception queue marker.
4. Click/tap a waiting capsule, then a room floor, bed, wall, or open door to reserve it. Each guest owns one queue slot and each room accepts one reservation. Watch the room details for Reserved, then Occupied.
5. After the default 45-second stay, the guest pays 35 coins once, leaves, and the room becomes Dirty. Select that room and press **CLEAN ROOM**. After eight seconds it becomes Available.
6. Leave a guest waiting for 55 seconds to verify patience departure. Leave all rooms unavailable to verify the queue continues to expire rather than blocking arrivals forever. The default cap is eight guests, including departures.
7. Open Window > General > Test Runner > EditMode and run `RoadsideHaven.Tests`. Tests cover exclusive reservations, owner checks, exactly-once payment, and timed cleaning.

Drag with one finger or the left mouse button to pan. Pinch or scroll to zoom. A 12-pixel threshold distinguishes dragging from selection. HUD panels block world input. Bounds and zoom limits are serialized on the camera controller; the HUD respects the safe area.

## Tuning and build

`Assets/_Game/Generated/HotelSettings.asset` exposes arrival interval, cap, patience, stay/cleaning times, rate, navigation timeout, initial balance, and target FPS (30). Regeneration preserves this configuration. Generated geometry/materials/scene are replaceable output; keep custom work outside Generated. The command saves a baked AI Navigation NavMesh and appends the prototype to Build Settings without removing existing scenes. For Android, choose the prototype as the startup scene in your Build Profile, switch to Android, select landscape, and Build And Run on the A53. Portrait rotation is disabled by the generator.

Core owns scene composition, typed notifications and service interfaces. Hotel and Economy hold plain runtime models; AI wraps NavMeshAgent, and UI subscribes/unsubscribes to scene-owned events. Editor and tests have separate Editor-only assemblies. No global mutable gameplay singleton is used. Path requests require a complete path; failures release reservations, and navigation deadlines remove stuck departures. UI snapshots update twice per second rather than formatting text each frame.

## Live room overview update

## Stylized environment upgrade

### Regional world: 2 km x 2 km

The additive `Regional World 2km v1` installer creates a 2000-by-2000-meter physical foundation (4 square kilometers), with a centered coordinate range of -1000 to +1000 on X/Z. It preserves the existing motel/village neighborhood and deactivates its four small boundary walls so vehicles can enter the surrounding region. Regional boundary walls retain vehicles inside the world. Camera focus bounds expand to +/-970 meters and overview zoom to 1100. Camera distance/far clip and orthographic fog offsets adapt to zoom; scroll out to inspect the region.

Zone markers follow +Z = North and -X = West: mountain ridge, northern highway, parked trucks, rest apron and a decorative highway motel in the north; city blocks and school/college landmarks in the west; the working motel base at the center; sand, huts and visual ocean in the south; warehouses, loading yards and chimneys in the east. Highways connect the zones. The original small city remains near the base as a local neighborhood. Main facade detail is capped at three window rows and six columns per building to bound primitive counts. This is a low-detail regional blockout with real driving surfaces, not a fully populated 4 km2 city.

The highway motel, factories and beach huts are scenery with physical building masses. Ocean has no swimming/water physics; a shoreline barrier prevents driving into it. Trucks are parked decoration. Existing car/CNG and car-wash controls are retained. Motel pedestrian NavMesh is not extended over the new region. No terrain streaming, regional traffic routing, economy/services in the new buildings, or multiplayer is added. Apply the visual upgrade and save outside Play Mode for builds. C# compilation is checked; Play Mode navigation, driving across region junctions, GPU appearance and device performance require Unity validation.

### Outer roads, seasonal sky and calendar

The additive `Outer Roads v1` layer adds a driveable perimeter loop, north/south roads and cross-district connectors inside the existing 240-by-180-meter world. It is installed even when the village/city layer already exists. These roads have physical colliders; existing buildings and pedestrian NavMesh are retained.

`MotelAtmosphere` now uses six seasons: Summer, Monsoon, Autumn, LateAutumn, Winter and Spring. A default day lasts eight real minutes and each season lasts three in-game days. Season changes affect vegetation/ground colors, cloud cover, sunlight strength, daylight duration and haze. Monsoon is cloudier/greener, winter has a shorter day and reduced visibility. There is no rainfall simulation, snow accumulation or weather effect on vehicle grip. Inspector controls include starting hour/season, day duration, days per season and fog visibility. The calendar is session-local and is not saved.

The new `RoadsideHaven/Seasonal Sky` shader renders blue daytime sky, warm sunset glow, a moving sun disc, opposite full moon, procedural night stars and drifting cloud patterns. The installer assigns the sky material even to existing atmosphere components. A runtime material clone preserves the saved asset; disabling the component restores previous sky, light, ambient, fog and vegetation overrides. The on-screen panel shows season/day/time. **F6** previews sunset, **F7** night and **F8** advances one season. **F9** switches between the overhead map and a perspective skyline view so the sky/horizon are easier to see; Q/E rotates this view. Camera mode changes are blocked during driving/shop input modes. These are artistic sky effects, not astronomical lunar phases or geographic sun calculations.

Source compilation passes for Runtime and Editor code. The custom shader still needs import/compilation and visual checking in the active Unity Editor; C# compilation does not validate GPU shaders. After Unity finishes import, open the generated prototype and press Play. To include changes in a player build, apply the visual upgrade and save the scene outside Play Mode.

### Village beside a city: local driving prototype

The additive `Village City World v1` layer extends the foundation to **240 x 180 meters**, with west-side village homes, pitched roofs, fields, a pond and market stalls, plus east-side city buildings, a school and college. A main highway and branching village/city streets connect the districts. New foundations, roads, boundaries and main building masses have physical colliders. Existing motel gameplay and its baked pedestrian navigation are retained. School/college are exterior landmarks; there are no lessons, student schedules, or enterable interiors. This is one connected single-player world, not networked multiplayer or multiple universes.

Press **Play** in the generated prototype to preview. Use **Roadside Haven > Apply Visual Upgrade** outside Play Mode and save to include the additions in a build. Installation preserves the older layers and does not duplicate the named world layer. Custom modifications should live outside generated layers. Scroll out and pan west/east to see the village/city.

The on-screen **DRIVE CAR** and **DRIVE CNG** buttons switch control between two local vehicles; **MOTEL** returns the camera/input to management. **Tab** cycles car, CNG and management. Drive with **WASD/arrow keys**, brake with **Space**, and use **R/RESET** to recover the vehicle at its starting point. Touch-friendly prototype hold buttons are included. The camera follows the active vehicle and management world taps are blocked while driving. Close the shop modal before entering driving mode. The CNG is a three-wheel auto-rickshaw model with the same simplified arcade handling as the car; it does not simulate three-wheel balance, tire suspension or passenger fares.

Drive to the **CAR WASH**, around `(55, -24)` in world X/Z, and stop inside its open bay for five seconds. The spray indicator appears while washing; completion restores the vehicle body color from its dirty tint. Leaving the bay or switching vehicles interrupts the timer. Each vehicle can be washed once per Play session. This interaction is free, has no economy/reward loop, and resets on Play. Both vehicles use gravity, collision, fixed-step acceleration/braking and lateral grip, with roll/pitch locked for stable arcade driving. Existing decorative props remain nonblocking; this is not a realistic suspension/crash simulator. Use the `WorldDrivingController` Inspector to tune handling and the wash duration/area.

The new source files compile against the installed Unity assemblies. Unity Play Mode, car-wash interaction, collisions, mobile touch input and performance still need interactive validation. Large-world pedestrian navigation and multiplayer have not been implemented.

### Parking guest arrivals

The installer now connects four pedestrian arrival markers on the parking-facing edge of the existing courtyard. Guests rotate through the markers, walk to their exclusive reception queue slot, and return to the same marker on departure. Before spawning, the game checks that the marker is near the NavMesh, has a complete reception route, and is not occupied by another motel guest. If all routes are unavailable, that arrival attempt is skipped. Existing scenes without parking configuration retain the original spawn/exit flow. Custom nonempty parking marker arrays are preserved by the installer.

The white bay markers are on the courtyard side of parked cars, away from moving district traffic. This is pedestrian arrival/departure; guests do not drive or open car doors. No district-wide NPC navigation or new NavMesh bake is introduced. Runtime and Editor sources compile with zero warnings/errors. Run `ParkingNavigationTests` in Unity's EditMode Test Runner to check every bay against all eight reception slots in both directions using the generated NavMesh asset. Those Unity tests and a new Play Mode run have not been executed here.

### Character and lighting presentation

The visual installer now adds `MotelAtmosphere` and a global `Motel Color Grading` Volume even to already-upgraded scenes. The default cycle starts at 15:00 and lasts eight real minutes, changing sun direction, sunlight color/intensity and ambient light. Its Inspector exposes timing and lighting controls; disabling it restores previous ambient and sun settings. The URP profile in `Generated/Visuals/MotelPresentation.asset` uses ACES, subtle bloom, contrast and exposure adjustments. The gameplay camera enables HDR, post-processing and FXAA. These settings can be tuned in the profile; they still depend on the active URP asset's capabilities. Camera view distance now keeps the larger district in front of its near clip plane when zoomed out.

Built-in characters have hair, eyes, shoes, varied clothing, a breathing idle and smoothed walking transitions. To use real animation assets, assign a meter-scale prefab to **MotelGameRoot > Guest Character Prefab** and/or **ShopController > Customer Character Prefab** in the scene, then save. The prefab should face local +Z, have its feet at local Y=0 and contain an Animator/controller with a float `Speed` parameter (meters per second) driving idle/walk transitions. The wrapper disables root motion, visual-prefab colliders and dynamic rigidbodies so its existing NavMeshAgent and selection collider remain authoritative. No rigged model, animation clips or third-party assets are included in this project.

Validation: updated Runtime and Editor sources compile together with zero warnings/errors against the installed Unity/URP assemblies. Existing Editor log was inspected; this is not a new Play Mode run. Character animation, the day/night cycle, post-processing appearance and device frame rate still need Unity validation. Outer-district NPC navigation remains unchanged.

### Expanded district and moving characters

The visual installer now also adds an 86-by-74-meter district foundation, a connected four-road loop, sidewalks, neighborhood building exteriors, a park, street lamps, and three moving cars. The `Expanded District v1` marker makes this additive even when `Visual Polish v1` already exists. Pan limits expand to 36/29 meters, zoom supports sizes 6–42, and Q/E rotates the camera. Scroll out to see the district. Existing motel and shop destinations and their baked NavMesh are retained; the surrounding district is scenery rather than new NPC destinations or purchasable business space.

Guests and shop customers now use agent-speed-driven procedural limb movement and body bobbing. These are articulated primitive placeholders, not imported rigged characters or motion-captured animations. Original selection colliders remain in place. Traffic uses interpolated kinematic Rigidbody movement in FixedUpdate with box colliders on a closed loop. This supplies consistent ambient movement, not tire simulation, suspension, yielding, collision response between cars, or crash physics. Neighborhood props remain nonblocking decoration. Building interiors are not enterable.

Open the prototype and press Play to preview; use **Apply Visual Upgrade** and save outside Play Mode before building. The larger environment and characters require profiling on the target device. Source compilation was checked, but Unity visual checks, navigation behavior, physics behavior, and device performance remain unverified.

Open `Assets/_Game/Generated/RoadsideHavenPrototype.unity` and press Play after script compilation. An Editor-only Play Mode hook adds the decorative layer automatically to this specific prototype scene. The upgrade includes roadside parking, two decorative cars, landscaping, a reception canopy, room rugs/windows/lamps, stocked shop shelves, a roadside sign, and a soft sky fill light. Decorations have no colliders and live outside Navigation Geometry, preserving baked paths and click targets. Cars and trees are decorative; NPCs do not avoid them, so they are placed at the property edges.

To include the upgrade in a player build, stop Play Mode, use **Roadside Haven > Apply Visual Upgrade**, and save the scene. **Create or Update Prototype** also installs and saves it. The named `Visual Polish v1` layer prevents duplicate installation. Keep custom objects outside that layer. Materials are stored in `Generated/Visuals`; no downloaded art or paid assets are needed. This is a primitive-based stylized environment pass, with no new character animations, audio, or post-processing effects. Mobile performance and visual composition require Play Mode/device validation.

The updated Editor sources, including the new installer, compile with zero warnings and errors using the local .NET SDK. Unity Play Mode and a player build have not been exercised for this visual update.

## Room overview behavior

Existing generated scenes now show live room state on their room-number signs, including cleaning and checkout countdowns. The selected room sign turns orange and gains a `>` marker. Available signs are green and dirty signs are red; state text also identifies each status without relying on color. The HUD shows ready, reserved, dirty, and cleaning totals, plus the occupied total in its existing top bar. Updates use the existing twice-per-second snapshot cadence. No scene regeneration or Inspector wiring is required; allow Unity to compile and enter Play Mode. Signs reuse the generated TextMeshPro room labels; custom rooms need a child TextMeshPro label to show this information.

Validation for this update: Runtime, Editor, and Tests projects build with the local .NET SDK. Visual readability and countdown behavior still require checking in Unity Play Mode.

## Validation and limits

All scripts were compiled together using the installed Unity Editor's Roslyn compiler and its existing project assembly references. The three NUnit test methods passed when invoked directly outside Unity. This does not validate Unity assembly import, serialization, scene generation, the actual NavMesh bake, Play Mode, or Android behavior. The project was locked by the running Editor, so no second Editor was started and Unity Test Runner/device tests remain to be run using the steps above.

The prototype resets on Play; there is no persistence, staff, loans, or shop interaction. Guests and buildings use primitives. Room state is shown in the selection panel; there is no full room management dashboard. Cleaning is free. Camera framing, safe area, agent avoidance, TMP readability, and A53 performance need visual/device validation. Regenerating intentionally replaces the generated scene; preserve custom scenes elsewhere.

## Play as the motel owner

The visual installer now adds an `Owner Activities` layer with a local owner character, industrial supplier and rival sign. Apply the visual upgrade outside Play Mode and save to include it in a build; the Editor Play preview installs it automatically.

- **P / WALK** toggles walking and management. On foot use camera-relative **WASD**, hold **Shift** to run, or use the prototype directional buttons.
- **F / ENTER-EXIT** enters a nearby car/CNG. Vehicle buttons also require the owner to be close; switching remotely between vehicles is blocked. Stop first before exiting. The controller checks for clear ground beside the vehicle before placing the owner there.
- **G / INTERACT** near reception assigns a waiting guest to a reachable available room. Near a dirty room it starts existing timed cleaning. At the shop delivery marker `(7, -8)` it unloads cargo or opens the scanning/checkout UI. Movement pauses while shop checkout is open.
- At the industrial supplier `(500, -230)`, interact on foot to buy a four-unit water crate for eight coins. Carry it back, including while driving, and deliver it to the shop. Delivery increases stock and pays 20 coins. Full shelves reject the delivery without discarding cargo or charging again. This repeatable supplier trip is the first extra-income mission loop; it is not a story mission system.
- At the fictional rival sign `(180, 710)`, interact to disable its advertising for 45 seconds. Your guest arrival attempts become 25% more frequent during that time, within the existing queue/guest caps. A 120-second cooldown prevents immediate repetition. This is a sign prank and temporary simulation modifier; there is no destructive gameplay or rival NPC simulation.

The owner uses a primitive articulated mesh, CharacterController collision and procedural walking, not a rigged animation asset. Night exploration uses the existing lighting cycle. Owner/cargo/mission state resets on Play; save/load and multiplayer remain absent. Existing management UI is retained as an optional mode.

Validation: Runtime and Editor source compilation passes. Seven inventory checks were executed directly against the project shop model sources; they verify delivery acceptance, reserved capacity, invalid/full delivery rejection, unchanged state and no second debit. An NUnit regression test was also added. Unity Play Mode, vehicle entry/exit clearances, touch controls, on-foot navigation and mission interaction still require interactive validation.

## World road, building and garden night lighting

`World Night Lighting v1` adds lamp fixtures along both sides of generated roads, with 30-meter spacing on highways and 22-meter spacing on smaller roads. Junction fixtures are deduplicated approximately. Building entrances receive warm lanterns; facade/house/room windows receive night emission. Garden bollards are placed near trees, planters, motel rooms and courtyard paths. Fixtures have no colliders, preserving pedestrian paths and driving routes.

`WorldNightLighting` uses the atmosphere clock: lighting fades in during the evening and out after dawn. Distant bulbs and windows glow across the map, but actual illumination uses a fixed pool of at most 20 nearby unshadowed point lights within 85 meters of the current camera focus. Camera focus follows the owner or vehicle during exploration. URP's active additional-light settings can further limit the lights affecting an individual surface. Tune light budget, radius, intensity, range and emission on the controller. This is distance-prioritized real-time illumination, not a fully baked lighting solution for every surface of the 4 km2 world.

Open the prototype after script compilation, then use **Apply Visual Upgrade**, save, and press Play. Press **F7** to preview night. Existing generated scenes receive the additive lighting layer automatically in the Editor Play preview. Runtime materials are cloned and restored on disable; shared project material assets are not changed by the day/night fade. Delete only the generated lighting layer and reapply to regenerate fixtures after custom layout changes.

Validation: Runtime and Editor C# sources compile with zero warnings/errors. Night exposure, light placement, shader bloom, URP additional-light limits and target-device frame rate still require Unity Play Mode/device verification. These changes have not been pushed to GitHub.
