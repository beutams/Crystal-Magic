"""Extract an allowlist of CC0 elemental source sounds; never touch UI assets."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/combat-v1/sources"
PACKS = [
    ("ice", "icespells.zip", ["ice.wav", "coldsnap.wav"],
     "bart (source recording by Stephan / pdsounds)", "https://opengameart.org/content/ice-spells"),
    ("water", "water-splash-slime-sfx.zip",
     ["bubble_02.ogg", "splash_01.ogg", "splash_03.ogg", "splash_06.ogg", "splash_09.ogg", "splash_13.ogg"],
     "rubberduck", "https://opengameart.org/content/40-cc0-water-splash-slime-sfx"),
]


def main():
    records = []
    for pack, archive_name, members, author, page in PACKS:
        with zipfile.ZipFile(OUT / archive_name) as archive:
            for member in members:
                data = archive.read(member)
                target = OUT / "selected" / pack / member
                target.parent.mkdir(parents=True, exist_ok=True)
                if target.exists() and target.read_bytes() != data:
                    raise ValueError(f"Refusing to overwrite changed source: {target}")
                target.write_bytes(data)
                records.append(dict(pack=pack, local_file=target.relative_to(ROOT).as_posix(),
                                    archive=archive_name, member=member, author=author,
                                    page=page, license="CC0-1.0", sha256=hashlib.sha256(data).hexdigest()))
    flame = OUT / "flame_0.ogg"
    records.append(dict(pack="fire", local_file=flame.relative_to(ROOT).as_posix(),
                        author="themightyglider; derived from qubodup",
                        page="https://opengameart.org/content/catching-fire",
                        license="CC0-1.0", sha256=hashlib.sha256(flame.read_bytes()).hexdigest()))
    (OUT / "selected-sources.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(records, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
