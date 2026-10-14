# Gorilla Drive

Play **The Long Drive** as a **Gorilla Tag** gorilla. A Melty mashup (single player).

- Your gorilla's body, hands and fur are read from **your own Gorilla Tag install** each time the game starts. Nothing from either game is shipped.
- Gorilla Tag's arm locomotion on a mouse: hold **left/right mouse** to grab the ground, a wall or your car with that hand, **drag the mouse** to pull yourself along, **let go** to fling yourself with Gorilla Tag's jump rules.
- Everything else in The Long Drive works as normal: get in the car to drive, loot, refuel. Gorilla movement pauses while you're seated.
- **G** toggles gorilla mode, **V** toggles a third-person view of your gorilla.

## Requirements
The Long Drive (Steam 1017180) and Gorilla Tag (Steam 1533390), both installed. Melty installs BepInEx 5 (bundled) and the mod into The Long Drive and passes your Gorilla Tag folder at launch (`--gorilladrive-gt=<folder>`). Without Melty, set `GorillaTagFolder` in `BepInEx/config/gg.melty.gorilladrive.cfg`, or it's found from your Steam libraries.

## How it's built
- `design/sheets/*.json`: the design, one row per thing. `tools/gen_sheets.py` turns rows into `src/GorillaDrive/Sheets.g.cs`; `tools/preflight.py` lists unfilled cells, broken references and cells not yet verified in game.
- `src/GorillaDrive`: BepInEx 5 plugin (net472). `GtReader` reads Gorilla Tag's serialized assets with AssetsTools.NET; `GorillaMotor` is the GorillaLocomotion-style arm movement; `TldHooks` finds The Long Drive's player at runtime (no patches).
- `tests/MeshDecodeTests`: unit tests for the vertex-buffer decoder (`dotnet run`).
- `tools/package.py`: builds `dist/GorillaDrive-<version>.zip`.

Build: `dotnet build -c Release src/GorillaDrive && python3 tools/package.py`

## Credits
BepInEx (MIT), UnityDoorstop (LGPL-2.1, NeighTools), AssetsTools.NET (MIT, nesrak1), Unity type data from AssetRipper Tpk (MIT, ds5678). Movement follows Another Axiom's open-source GorillaLocomotion (MIT). Not affiliated with Gabor Pinter or Another Axiom.
