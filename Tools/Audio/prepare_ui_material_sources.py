"""Extract only selected, already-downloaded Kenney CC0 source files."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "output/audio/ui-v2/sources"
SELECTION = {
    "rpg-audio": ["bookFlip1", "bookFlip2", "bookFlip3", "bookPlace1", "bookPlace2",
                  "cloth1", "cloth3", "dropLeather", "handleSmallLeather", "beltHandle2",
                  "metalClick", "metalLatch", "bookOpen", "bookClose", "creak1", "creak2",
                  "handleCoins", "handleCoins2", "handleSmallLeather2", "beltHandle1", "clothBelt2"],
    "impact-sounds": ["impactWood_light_000", "impactWood_light_001", "impactWood_light_002",
                      "impactGlass_light_000", "impactGlass_light_001", "impactMetal_light_000",
                      "impactWood_light_003", "impactWood_light_004", "impactWood_medium_000",
                      "impactWood_heavy_000", "impactSoft_medium_000", "impactSoft_heavy_000",
                      "impactPlate_light_000", "impactPlate_heavy_000", "impactGlass_light_002"],
}

records = []
for pack, names in SELECTION.items():
    archive_path = SOURCES / f"kenney_{pack}.zip"
    with zipfile.ZipFile(archive_path) as archive:
        license_bytes = archive.read("License.txt")
        license_text = license_bytes.decode("utf-8-sig")
        if "CC0" not in license_text:
            raise ValueError(f"CC0 notice missing from {pack}")
        (SOURCES / f"LICENSE-Kenney-{pack}.txt").write_bytes(license_bytes)
        print(license_text)
        for name in names:
            member = f"Audio/{name}.ogg"
            data = archive.read(member)
            destination = SOURCES / "selected" / pack / f"{name}.ogg"
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
            records.append(dict(pack=pack, source_page=f"https://kenney.nl/assets/{pack}",
                                archive_member=member, source_sha256=hashlib.sha256(data).hexdigest(),
                                local_file=str(destination.relative_to(ROOT)).replace('\\', '/')))
(SOURCES / "selected-sources.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Selected {len(records)} CC0 source files; no archive paths were extracted blindly.")
