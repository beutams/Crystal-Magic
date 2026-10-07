"""Create short, lossless PCM excerpts; leave the downloaded music unchanged."""
import json
from pathlib import Path
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/audio/bgm-audition-alan-zaring'
FILES = [('02 Town 2 LOOP.wav', 'town-preview-60s.wav'),
         ('09 Jungle Dungeon LOOP.wav', 'dungeon-preview-60s.wav')]
report = []
for source_name, target_name in FILES:
    with wave.open(str(OUT / source_name), 'rb') as source:
        params = source.getparams()
        assert params.sampwidth == 2 and params.nchannels == 2 and params.framerate == 48000
        frames = source.readframes(min(params.nframes, params.framerate * 60))
        samples = np.frombuffer(frames, dtype='<i2').astype(np.float64) / 32768
        peak = float(np.max(np.abs(samples)))
        assert 0.01 < peak <= 1.0
        with wave.open(str(OUT / target_name), 'wb') as target:
            target.setparams(params)
            target.writeframes(frames)
        report.append({'source': source_name, 'preview': target_name,
                       'source_duration_seconds': params.nframes / params.framerate,
                       'preview_duration_seconds': len(frames) / (params.framerate * 4),
                       'peak': peak, 'rms': float(np.sqrt(np.mean(samples * samples))),
                       'changes': 'First 60 seconds only; no resampling, gain adjustment or re-encoding.'})
(OUT / 'previews.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
