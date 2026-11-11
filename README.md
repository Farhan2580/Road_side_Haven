# Roadside Haven

A work-in-progress Unity motel and convenience-shop management prototype with an expanding single-player driving world.

## Open and play

1. Install **Unity 6000.3.28f1** through Unity Hub.
2. Clone this repository and add its root folder in Unity Hub.
3. Open it and allow Unity to restore packages, import assets and compile scripts.
4. Open `Assets/_Game/Generated/RoadsideHavenPrototype.unity`.
5. Use **Roadside Haven > Apply Visual Upgrade** and save the scene to persist generated world additions for builds. In the Editor, pressing Play also previews additions automatically.
6. Press Play in a landscape Game view.

If the prototype scene is missing, use **Roadside Haven > Create or Update Prototype**.

## Current features

- Guest reception queues, room assignment, checkout payments and timed cleaning.
- Shop stock, scanning and checkout with a shared wallet.
- Procedural characters and support for optional animated prefabs.
- Car/CNG driving and a prototype car-wash interaction.
- A generated 2 km x 2 km regional blockout: central motel, western city, northern mountain highway, southern beach and eastern industry.
- Day/night lighting, six-season visual changes and a procedural sky shader.

## Controls

Drag to pan, scroll/pinch to zoom, Q/E to rotate. Select a waiting guest, then an available room. Select a dirty room to clean it.

Use the on-screen driving buttons or Tab to select a car/CNG. WASD/arrows drive, Space brakes, R resets and MOTEL returns to management. F6 previews sunset, F7 night, F8 the next season and F9 a skyline camera view outside driving mode.

## Status and validation

This is a primitive-based prototype. Regional buildings are mostly exterior scenery. The motel's pedestrian NavMesh does not cover the full region. There is no multiplayer, persistent save system, realistic vehicle suspension or complete city simulation.

Recent C# changes were compiled against installed Unity assemblies. Unity Play Mode, shader rendering, regional driving, mobile input and device performance still require interactive validation. EditMode tests are under `Assets/_Game/Tests/Editor`; run them using Unity's Test Runner.

More implementation notes: [game README](Assets/_Game/README.md).

## Repository contents

Assets (including `.meta` files), Packages and ProjectSettings are versioned. Unity caches, local backups, generated IDE projects and build outputs are ignored and recreated locally. No license for redistribution is declared by this repository; bundled third-party resources retain their respective notices.
