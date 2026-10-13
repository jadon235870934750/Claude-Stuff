#!/usr/bin/env python3
"""Preflight: overlay every design sheet, list unfilled cells, unverified cells
and cross-sheet references that don't resolve. Exit 1 if any cell is unfilled
or any reference is broken; unverified cells are listed but don't fail the build
(they need the games installed to check)."""
import json, sys, pathlib, re

root = pathlib.Path(__file__).resolve().parent.parent
sheets = {p.stem: json.load(open(p)) for p in sorted((root / "design/sheets").glob("*.json"))}
src = "\n".join(p.read_text() for p in (root / "src").rglob("*.cs"))

unfilled, unverified, broken = [], [], []
for name, s in sheets.items():
    for row in s["rows"]:
        for col in s["columns"]:
            v = row.get(col)
            cell = f"{name}.{row.get('id')}.{col}"
            if v is None or (isinstance(v, str) and not v.strip()):
                unfilled.append(cell)
            elif col == "verified" and not str(v).startswith(("yes", "generic")):
                unverified.append(f"{cell}: {v}")

# cross-sheet references
struct_rows = [r for r in sheets["gt_content"]["rows"]]
for r in struct_rows:
    if not re.search(rf"\b(class|struct)\s+{r['code_struct']}\b", src):
        broken.append(f"gt_content.{r['id']}.code_struct -> {r['code_struct']} not defined in src")
for r in sheets["locomotion"]["rows"]:
    if not re.search(rf"\b{r['id']}\b", src):
        broken.append(f"locomotion.{r['id']} -> no field in src")
for r in sheets["controls"]["rows"]:
    if f'"{r["id"]}"' not in src:
        broken.append(f"controls.{r['id']} -> no config binding in src")
for r in sheets["tld_hooks"]["rows"]:
    if f'"{r["id"]}"' not in src:
        broken.append(f"tld_hooks.{r['id']} -> no hook lookup in src")
slugs = {r["melty_slug"] for r in sheets["games"]["rows"]}
recipe = root / "melty.json"
if recipe.exists():
    rec = json.load(open(recipe))
    for g in rec["games"]:
        if g["slug"] not in slugs:
            broken.append(f"melty.json game {g['slug']} not in games sheet")
    for r in sheets["packaging"]["rows"]:
        pass

for title, items in (("UNFILLED", unfilled), ("BROKEN REFERENCES", broken), ("UNVERIFIED (needs games installed)", unverified)):
    print(f"== {title}: {len(items)}")
    for i in items: print("  -", i)
sys.exit(1 if unfilled or broken else 0)
