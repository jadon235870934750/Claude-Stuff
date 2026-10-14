# Robloxcraft

**Minecraft, built out of Roblox.** You're still Steve, with Minecraft's own block menu, but the world
is made of Roblox's studded plastic parts under Roblox's sky.

Minecraft: Java Edition 1.21.1 (Fabric) is the game you play. Roblox brings its own content in from
**your own Roblox install** while you play. Nothing from Roblox or Minecraft is copied into this mod
or its download.

![Robloxcraft world](media/robloxcraft-world.png)

## What you get
- **Studded plastic blocks.** Each block's colour becomes a Roblox part made from Roblox's own
  `studs.dds`: studs on top, inlets underneath, smooth sides. Single-texture blocks (stone, dirt,
  planks) are studded all over. Flowers, leaves and glass keep their shape in flat plastic colour.
- **Roblox's sky, sun and moon.** Roblox's default skybox (`sky512_*`) replaces the Overworld sky, and
  Minecraft's blocky clouds are hidden. The sun and moon are Roblox's `sun.jpg` and `moon.jpg`.
  The sky darkens at night, and stars still come out.
- **Roblox's font.** All text uses Roblox's Source Sans (`SourceSansPro-Semibold.ttf`): chat, menus
  and the block menu.
- **The oof.** Getting hurt or dying plays Roblox's `oof.ogg`.
- **Still Minecraft.** You keep Steve (or your own skin), the creative block menu, survival,
  crafting, mobs and worlds. Single player.

![Block menu](media/robloxcraft-block-menu.png)

## How it finds Roblox
In order:
1. `ROBLOXCRAFT_ROBLOX_DIR` (Melty sets this from your Roblox install).
2. `%LOCALAPPDATA%\Roblox\Versions`.
3. The same folder under Bloxstrap, Fishstrap or Voidstrap.
4. `Program Files\Roblox`.

It picks the newest version folder that holds `PlatformContent/pc/textures/studs.dds`. Without Roblox,
Minecraft stays vanilla and the log says so.

## Layout
| Path | What |
|---|---|
| `sheets/*.json` | Design sheets, the source of truth: Roblox files read, block styles, sky faces, asset overrides, game hooks |
| `tools/sheets.py` | `preflight` checks every cell and cross-sheet reference; `generate` writes `Sheets.java` from the rows |
| `mod/` | Fabric mod (Mojang mappings, Loom 1.11.8, Minecraft 1.21.1, Fabric Loader 0.16.14). No Fabric API needed |
| `packaging/` | Prism Launcher instance files, credits, `package.sh`, draft Melty recipe |
| `media/` | Screenshots of this version running |

## Build
```
python3 tools/sheets.py generate     # after editing a sheet
cd mod && ./gradlew build             # mod jar in mod/build/libs
./packaging/package.sh                # dist/Robloxcraft-<version>-windows.zip (portable Prism + instance)
```

## Credits
Mod by agoodclanker, built with Claude Code. The package bundles Prism Launcher (GPL-3.0); see
`packaging/CREDITS.txt`. Minecraft is a trademark of Mojang/Microsoft and Roblox of Roblox
Corporation; this project is not affiliated with either.
