"""Inspect a licensed stock fire explosion, without editing it or project assets."""
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/fire-reference-study"
MEMBER = "04_Fire_explosion_04_medium.wav"


def measure(data):
    with wave.open(io.BytesIO(data), "rb") as wav:
        rate, channels, width = wav.getframerate(), wav.getnchannels(), wav.getsampwidth()
        assert width in (2,3) and wav.getcomptype() == "NONE"
        pcm = wav.readframes(wav.getnframes())
        if width == 2:
            x = np.frombuffer(pcm,dtype="<i2").astype(float)/32768
        else:
            packed = np.frombuffer(pcm,dtype=np.uint8).reshape(-1,3).astype(np.int32)
            values = packed[:,0] | (packed[:,1]<<8) | (packed[:,2]<<16)
            values = np.where(values & 0x800000,values-0x1000000,values)
            x = values.astype(float)/8388608
        x = x.reshape(-1,channels)
    # Sum spectral energy across channels, rather than downmixing a stereo
    # reference and possibly cancelling its correlated/anti-correlated content.
    spectrum = np.sum(np.abs(np.fft.rfft(x,axis=0))**2,axis=1)
    freq = np.fft.rfftfreq(len(x),1/rate)
    total = np.sum(spectrum)
    energy = np.sum(x*x,axis=1)
    cumulative = np.cumsum(energy)/np.sum(energy)
    times = {str(p):round(float(np.searchsorted(cumulative,p/100))/rate,4) for p in [10,50,90,99]}
    active = np.flatnonzero(np.max(np.abs(x),axis=1) > np.max(np.abs(x))*.01)
    return dict(seconds=round(len(x)/rate,4),sample_rate=rate,channels=channels,bits=width*8,
                peak_dbfs=round(float(20*np.log10(np.max(np.abs(x)))),2),
                rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean(x*x)))),2),
                frequency_energy_percent={
                    "below_250_hz":round(float(np.sum(spectrum[freq<250])/total*100),2),
                    "250_to_1500_hz":round(float(np.sum(spectrum[(freq>=250)&(freq<1500)])/total*100),2),
                    "above_1500_hz":round(float(np.sum(spectrum[freq>=1500])/total*100),2)},
                cumulative_energy_time_seconds=times,
                active_bounds_seconds=[round(float(active[0])/rate,4),round(float(active[-1])/rate,4)],
                sha256=hashlib.sha256(data).hexdigest())


def main():
    source_archive = OUT/"leohpaz/8_rpg_battle_magic_sfx_free_samples.zip"
    with zipfile.ZipFile(source_archive) as archive:
        assert archive.testzip() is None
        data = archive.read(MEMBER)  # Extract this one exact known member only.
    target = OUT/"leohpaz"/MEMBER
    if target.exists() and target.read_bytes() != data:
        raise ValueError("Existing reference differs; refusing to overwrite")
    target.write_bytes(data)
    assert hashlib.sha256(target.read_bytes()).digest() == hashlib.sha256(data).digest()
    files = {
        "leohpaz_original_medium_fire_explosion":target,
        "our_v2_impact":ROOT/"Assets/Res/Audio/SFX/Fireball_PrototypeV2/fireball_v2_impact.wav",
        "our_v3_impact":ROOT/"Assets/Res/Audio/SFX/Fireball_PrototypeV3/fireball_v3_impact.wav",
    }
    results = {name:measure(path.read_bytes()) for name,path in files.items()}
    report = dict(purpose="Reference study only; no EQ, cuts, normalization, synthesis or project integration",
                  source_author="Leohpaz",source_title="8 Magic Attacks",
                  source_page="https://opengameart.org/content/8-magic-attacks",
                  author_pack="https://leohpaz.itch.io/50-rpg-battle-magic-sfx",
                  license_on_download_page="CC-BY-4.0",
                  license_url="https://creativecommons.org/licenses/by/4.0/",
                  original_reference_unmodified=True,
                  measurement_note="Unweighted spectral energy and amplitude envelope, not perceived loudness or a listening assessment.",
                  sounds=results)
    (OUT/"comparison.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(report,ensure_ascii=True,indent=2))


if __name__ == "__main__":
    main()
