"""Read-only binary, packaging and inline-preview validation for UI SFX v1."""
import argparse
import base64
import gzip
import hashlib
import json
from pathlib import Path
import re
import wave
import zipfile
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--preview", required=True, type=Path)
args = parser.parse_args()
output = ROOT / "output/audio/ui-v1"
manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))["sounds"]
fragment = args.preview.read_text(encoding="utf-8")
payload = json.loads(re.search(r'<script type="application/json"[^>]*>(.*?)</script>', fragment, re.S)[1])
assert len(manifest) == len(payload) == 24
assert len(fragment.encode("utf-8")) < 1_000_000
hashes = set()
max_step = 0
with zipfile.ZipFile(output / "crystal-magic-ui-sfx-v1.zip") as archive:
    assert archive.testzip() is None
    for entry, embedded in zip(manifest, payload):
        path = ROOT / "Assets/Res/Audio/SFX/UI" / entry["file"]
        raw = path.read_bytes()
        digest = hashlib.sha256(raw).hexdigest()
        assert digest == entry["sha256"] and digest not in hashes
        hashes.add(digest)
        assert archive.read("WAV/" + entry["file"]) == raw
        deltas = np.frombuffer(gzip.decompress(base64.b64decode(embedded["wav_delta16_gzip"])), dtype="<u2")
        reconstructed = (np.cumsum(deltas, dtype=np.uint64) & 65535).astype("<u2").tobytes()
        assert reconstructed == raw
        with wave.open(str(path), "rb") as wav:
            assert (wav.getframerate(), wav.getnchannels(), wav.getsampwidth()) == (48000, 1, 2)
            pcm = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float) / 32768
        assert pcm[0] == pcm[-1] == 0
        assert 0.001 < np.max(np.abs(pcm)) < .5
        assert np.sqrt(np.mean(pcm ** 2)) > .001
        assert abs(np.mean(pcm)) < .001
        step = np.max(np.abs(np.diff(pcm)))
        max_step = max(max_step, float(step))
        assert step < .2, (path, step)
print(json.dumps(dict(validated=24, wave_format="48000 Hz / 16 bit PCM / mono",
                      distinct_files=24, preview_lossless=True, zip_verified=True,
                      max_adjacent_sample_delta=round(max_step, 6),
                      peak_dbfs_range=[min(e["peak_dbfs"] for e in manifest), max(e["peak_dbfs"] for e in manifest)]), indent=2))
