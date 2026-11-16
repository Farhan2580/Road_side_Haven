# Roadside Haven

[Play the browser demo](https://farhan2580.github.io/Road_side_Haven/) · Desktop/laptop keyboard and mouse

A Unity PC/laptop prototype combining motel and shop management with an open driving world.

## Run

1. Open the project with **Unity 6000.3.28f1** and wait for import/compilation.
2. Open `Assets/_Game/Generated/RoadsideHavenPrototype.unity`.
3. Use **Roadside Haven > Apply Visual Upgrade** and save the scene to include generated additions in builds.
4. Press Play. Editor Play Mode also previews additions automatically.

If the scene is missing, use **Roadside Haven > Create or Update Prototype**. Keep custom work outside generated layers.

## Web demo and automatic updates

The public demo is hosted at [farhan2580.github.io/Road_side_Haven](https://farhan2580.github.io/Road_side_Haven/). Anyone with a compatible desktop browser can play without installing Unity or downloading the project.

`Publish browser demo` deploys a tested WebGL export from a GitHub Release. To update this way, zip the **contents** of `Builds/WebGL` so `index.html` is at the archive root, name it `RoadsideHaven-WebGL.zip`, attach it to a release whose tag begins `web-demo-`, then publish the release. The workflow validates the files and updates the same Pages URL. Existing releases also work through **Actions > Publish browser demo > Run workflow**, using the release tag.

For automatic **source-push builds**, `Web demo` builds the saved prototype scene whenever game changes are pushed to `main`, once Unity cloud activation is configured. Without activation, that workflow skips the build and leaves the current published demo online. Failed builds also leave the previous demo available.

One-time repository setup:

1. Under **Settings > Pages > Build and deployment**, select **GitHub Actions** as the source.
2. Follow [GameCI's Personal license activation guide](https://game.ci/docs/github/activation/) for the Unity account used for builds. Under **Settings > Secrets and variables > Actions**, add repository secrets named `UNITY_LICENSE` (the license file contents), `UNITY_EMAIL` and `UNITY_PASSWORD`. Add these directly in GitHub, never to source files. This workflow uses Personal license activation; paid/floating licenses require the matching GameCI configuration.
3. Open **Actions > Web demo > Run workflow** after adding the secrets. Subsequent game pushes to `main` run it automatically. Save scene changes before committing; Editor-only previews are not included. Local-build release publishing does not require these Unity secrets.

The browser demo is intended for desktop/laptop keyboard and mouse. It uses single-threaded WebGL, gzip decompression fallback for static hosting, hashed asset names, a loading screen and fullscreen controls. **Visuals > Sharp** is the default: full render scale, up to 2x display pixel density and 2x MSAA. Balanced and Performance reduce the canvas resolution without restarting the game. Browser performance still needs testing on the target laptop; the open world may require further optimization. GitHub Pages supports a published site up to [1 GB](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits); the workflow checks the exported size before deploying.

For a local export, install **Web Build Support** for Unity **6000.3.28f1** in Unity Hub, then use **Roadside Haven > Web Demo > Build Browser Demo**. With Node.js installed, run `node scripts/serve-web-demo.mjs` and open `http://127.0.0.1:8080/`. Double-clicking `index.html` will not run Unity Web builds. Exported files and local license files are ignored by Git.

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
