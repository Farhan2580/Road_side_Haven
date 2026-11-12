# Roadside Haven

A Unity PC/laptop prototype combining motel and shop management with an open driving world.

## Run

1. Open the project with **Unity 6000.3.28f1** and wait for import/compilation.
2. Open `Assets/_Game/Generated/RoadsideHavenPrototype.unity`.
3. Use **Roadside Haven > Apply Visual Upgrade** and save the scene to include generated additions in builds.
4. Press Play. Editor Play Mode also previews additions automatically.

If the scene is missing, use **Roadside Haven > Create or Update Prototype**. Keep custom work outside generated layers.

## Controls

| Key | Action |
| --- | --- |
| WASD / arrows | Move or drive |
| Mouse / scroll | Look / camera distance |
| Shift | Sprint |
| Space | Jump / vehicle brake |
| E (or F) / G | Enter-exit nearby vehicle / nearby work |
| M / P | Management map / on foot |
| J / L | Garage and quests / NPC dialogue |
| T / B | Dimensional portal / business portfolio |
| R / Escape | Reset vehicle / release cursor and close menus |

Click the Game view for keyboard focus. MENU contains actions and controls help. F6/F7 preview sunset/night, F8 changes season, and F9 changes the management camera view.

## Features

- Protected junction signals (red/amber/green and a pedestrian phase), zebra crossings, 3m sidewalks, jogging citizens and oak/pine/palm avenue trees. AI traffic obeys stop lines; pedestrians use sidewalk/corner routes and wait for crossing clearance.

- Persistent town traffic: up to 48 cars, two buses, 96 citizens and 8 students across the road network, with separated safe spawns and full vehicle obstacle checks. Road profiles use 12m highways, 8m city streets and 5m dirt roads. Students use school/college stops; workers and doctors commute, police patrol by stations/roadside boxes. E takes over buses; school service resumes after exiting. This is a finite prototype population; medical treatment, arrests and full building interiors are not implemented.
- Guest check-in, room cleaning, checkout, shop stock and scanning.
- Third-person walking, vehicle chase camera, car/CNG/truck ownership and upgrades.
- Truck deliveries, NPC quests, relationships, reputation and optional conflict events.
- A 2 km x 2 km regional blockout, solid-world collision repair and framed signs.
- Modern downtown high-rises, restaurant plazas and five original modern car designs; E enters any nearby vehicle.
- Off-road driving and small curb climbing with collision-tested vehicle movement.
- Six-season visuals, street/building/garden lights and an 80% day / 20% night cycle.
- Four dimensional overlays, separate wallets and cross-universe item trading.
- Business purchases, unlocks, upgrades, safe income and fictional bank investments.

Gameplay settings are in `Assets/_Game/Generated/HotelSettings.asset`. Business tuning is in `Assets/_Game/Business/Configuration/BusinessCatalog.asset` after installation.

## Status

This is a primitive-based, single-player prototype. Progress resets on Play; save/load is not implemented. Dimensions share the physical map. Regional properties mostly use exterior/economic representations; full combat, helicopter flights, refueling and complete city simulation are not implemented.

Runtime/Editor C# compilation, isolated native vehicle physics and town route/spawn regression checks have passed. Full-scene Unity Play Mode, final visual placement and laptop performance still require validation. Run the tests under `Assets/_Game/Tests/Editor` using Unity Test Runner.

Assets and their `.meta` files, Packages and ProjectSettings are versioned. Local caches, recovery backups and generated IDE files are ignored. Bundled third-party resources retain their respective notices.
