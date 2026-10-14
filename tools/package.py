#!/usr/bin/env python3
"""Build dist/GorillaDrive-<version>.zip from the packaging sheet. Needs the BepInEx
5.4.23.2 win_x64 zip and UABEA's classdata.tpk (paths via env or defaults)."""
import json, os, pathlib, zipfile, hashlib

root = pathlib.Path(__file__).resolve().parent.parent
version = "0.1.2"
bep = pathlib.Path(os.environ.get("BEPINEX_ZIP", "/opt/dl/bepinex.zip"))
tpk = pathlib.Path(os.environ.get("CLASSDATA_TPK", "/opt/dl/tpk/lz4_file/lz4.tpk"))
build = root / "src/GorillaDrive/bin/Release/net472"
out = root / "dist" / f"GorillaDrive-{version}.zip"
out.parent.mkdir(exist_ok=True)
plug = "BepInEx/plugins/GorillaDrive/"
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    with zipfile.ZipFile(bep) as b:
        for i in b.infolist():
            n = i.filename
            if n in ("winhttp.dll", "doorstop_config.ini", ".doorstop_version") or (n.startswith("BepInEx/core/") and n.endswith(".dll")):
                z.writestr(n, b.read(n))
    z.write(build / "GorillaDrive.dll", plug + "GorillaDrive.dll")
    z.write(build / "AssetsTools.NET.dll", plug + "AssetsTools.NET.dll")
    z.write(tpk, plug + "classdata.tpk")
    z.write(root / "packaging/CREDITS.txt", plug + "CREDITS.txt")
    z.write(root / "packaging/README.md", plug + "README.md")
    for lic in sorted((root / "packaging/licenses").glob("*.txt")):
        z.write(lic, plug + "licenses/" + lic.name)
    if (root / "LICENSE").exists():
        z.write(root / "LICENSE", plug + "LICENSE.txt")
data = out.read_bytes()
print(out, len(data), hashlib.sha256(data).hexdigest())
# check every packaging-sheet row is in the zip
names = zipfile.ZipFile(out).namelist()
for r in json.load(open(root / "design/sheets/packaging.json"))["rows"]:
    ok = any(n == r["archive_path"] or n.startswith(r["archive_path"]) for n in names)
    print(("OK  " if ok else "MISSING ") + r["id"], r["archive_path"])
