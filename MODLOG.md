# MODLOG

## Name
The listing is "Studcraft" because Melty already has a live "RobloxCraft" (Roblox avatars and items on a
baseplate, a different idea). The mod id is `studcraft`.

## Route
- Host: Minecraft Java 1.21.1 + Fabric Loader 0.16.14 (Melty installs Fabric via Prism's mmc-pack.json).
- Roblox: companion. It is never modded or launched. Files are read from the player's install
  (Hyperion anti-tamper; Melty installs no loader for Roblox).
- Mod: one client-only Fabric mod. An in-memory resource pack (`RobloxPack`) is appended in
  `PackRepository.openAllSelected` (client repository only). A `LevelRenderer.renderSky` hook draws the
  skybox and `renderClouds` is cancelled.

## Roblox install facts (client 0.743.0.7431129, version-7a0c6d10fcb34a2c)
- Package to folder: content-textures3 -> PlatformContent/pc/textures, content-sky -> content/sky,
  content-sounds -> content/sounds, content-fonts -> content/fonts.
- studs.dds: DXT1, 128x2048, 16 tiles of 128x128. Tiles 0-7 are studs, 8-15 inlets/universal.
  plastic/diffuse.dds has the same layout, fainter.
- sky512_{ft,bk,lf,rt,up,dn}.tex are DXT1 DDS files at 1024x1024 with mips. Edge matching (left to
  right while turning clockwise) gives ft -> lf -> bk -> rt. up is rotated 90 deg clockwise against
  ft's top; dn is rotated 90 deg anticlockwise against ft's bottom.
- The material textures (brick, wood, ...) are no longer in the client; they're downloaded on demand.
  So parts use studs.dds plus the Minecraft block's own colour.
- Fonts: BuilderSans is CFF .otf, which Minecraft 1.21.1's TTF provider rejects ("Font is not in TTF
  format"). SourceSansPro-*.ttf works.
- Sounds: oof.ogg, ouch.ogg, action_falling.ogg and volume_slider.ogg are Ogg Vorbis. action_* are mp3,
  which Minecraft can't play.

## Gotchas
1. FontManager finds fonts by listing `font/*.json`, so an override pack must list it, not just answer
   getResource. Providers from the top pack come first.
2. Block textures must be listed too (`textures/block`), or the atlas keeps the lower pack's copy.
3. DynamicTexture defaults to repeat wrap, which bleeds sky seams. Set CLAMP_TO_EDGE after register.
4. Maven Central rate-limits (429) cloud builds. The Google mirror is first in the repositories.

## Verified
- Dev client and release-jar production client (Loom `prodClient`, intermediary + refmap) under
  Xvfb/llvmpipe on Linux, with real Roblox 0.743 files laid out as a Roblox version folder.
- Checked in game: studded blocks, skybox (seamless), no vanilla clouds, Roblox sun, Source Sans in
  chat and menus.
- Melty-style test mode with the release jar (LOCALAPPDATA pointed at a scratch Melty/test file):
  mod-loads, roblox-found, studs-applied, roblox-sky, roblox-font and roblox-oof all pass.
- Melty validate_recipe and one_click_check: valid, one click yes.
- Not yet checked: hearing the oof (no audio device in the build sandbox; the bytes are served),
  Windows, and Melty's own test_release on a PC with both games.
- Packaging: game_info says a Minecraft-hosted mashup ships only its jar to {minecraft-instance}/mods
  (Melty brings Prism, Java and Fabric). The first Prism-bundle package was dropped for that reason.
