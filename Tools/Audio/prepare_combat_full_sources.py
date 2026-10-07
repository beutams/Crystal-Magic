"""Extract five explicitly selected CC0 Foley assets from the existing Kenney ZIP."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/combat-full-v1/sources"
NAMES = ("knifeSlice", "knifeSlice2", "chop", "metalPot1", "drawKnife1")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    records = []
    with zipfile.ZipFile(ROOT / "output/audio/ui-v2/sources/kenney_rpg-audio.zip") as archive:
        for name in NAMES:
            member = f"Audio/{name}.ogg"
            data = archive.read(member)
            path = OUT / (name + ".ogg")
            if path.exists() and path.read_bytes() != data:
                raise ValueError(f"Refusing to overwrite {path}")
            path.write_bytes(data)
            records.append(dict(file=path.relative_to(ROOT).as_posix(), member=member,
                                sha256=hashlib.sha256(data).hexdigest(), author="Kenney",
                                license="CC0-1.0", source="https://kenney.nl/assets/rpg-audio"))
    (OUT / "sources.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
    print(f"Extracted {len(records)} explicit audio members; no downloaded code executed.")


if __name__ == "__main__":
    main()
