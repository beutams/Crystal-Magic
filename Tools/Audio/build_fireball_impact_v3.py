"""Brighter, shorter fireball explosion. Keeps V2 release and all old audio intact."""
import hashlib
import io
import json
from pathlib import Path
import wave

import numpy as np

from build_fireball_v2 import (ROOT, RATE, SOURCE_FILES, load_source, warped_source,
                              noise, fire_grains, mix, norm, hashes)
from build_ui_material_v2 import filter_audio, fade, wav_bytes

OUT = ROOT / "output/audio/fireball-v3"
ASSETS = ROOT / "Assets/Res/Audio/SFX/Fireball_PrototypeV3"
V2 = ROOT / "Assets/Res/Audio/SFX/Fireball_PrototypeV2"


def read_wav(data):
    with wave.open(io.BytesIO(data), "rb") as wav:
        assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
        return np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float)/32768


def measure(x):
    spectrum = np.abs(np.fft.rfft(x))**2
    frequencies = np.fft.rfftfreq(len(x), 1/RATE)
    total = np.sum(spectrum)
    return dict(seconds=round(len(x)/RATE,4),
                peak_dbfs=round(float(20*np.log10(np.max(np.abs(x)))),2),
                rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean(x*x)))),2),
                below_250_hz_energy_fraction=round(float(np.sum(spectrum[frequencies<250])/total),5),
                above_1500_hz_energy_fraction=round(float(np.sum(spectrum[frequencies>1500])/total),5),
                max_sample_delta=round(float(np.max(np.abs(np.diff(x)))),5))


def main():
    protected = hashes()
    protected.update({str(p): hashlib.sha256(p.read_bytes()).hexdigest()
                      for p in V2.rglob("*") if p.is_file()})
    old_manifest = json.loads((ROOT/"output/audio/fireball-v2/manifest.json").read_text(encoding="utf-8"))
    for item in old_manifest["sounds"]:
        assert hashlib.sha256((V2/item["file"]).read_bytes()).hexdigest() == item["sha256"]
    records = [dict(r) for r in SOURCE_FILES if r["key"] in ("fireball", "crackle")]
    source = {r["key"]: load_source(r) for r in records}
    expected = {r["key"]: r["sha256"] for r in old_manifest["source_files"]}
    assert all(r["sha256"] == expected[r["key"]] for r in records)
    layers = [
        (noise(.16,106211,520,7200,.0015,.031),0.,.85,
         dict(synth="bright flame rupture",seed=106211,band_hz=[520,7200],attack=.0015,decay=.031)),
        # The old 55-510 Hz rumble layer is removed. This small, short body only
        # supports the transient, with no long sub-bass resonance.
        (noise(.22,106212,210,1000,.002,.045),.003,.19,
         dict(synth="short mid-low impact support",seed=106212,band_hz=[210,1000],attack=.002,decay=.045)),
        (warped_source(source["fireball"],.52,[0,.040,.14,.32,.52],
                       [.52,.65,.92,1.27,1.69],370,8000,1.35),.005,.73,
         dict(source="fireball",output_times=[0,.040,.14,.32,.52],source_times=[.52,.65,.92,1.27,1.69],band_hz=[370,8000],drive=1.35)),
        (fire_grains(source["crackle"],1.52,.47,1.23,.24),.073,.24,
         dict(source="crackle",start=1.52,seconds=.47,speed=1.23,decay=.24,band_hz=[650,6500],drive=3.2)),
        (noise(.32,106213,720,3800,.006,.070),.018,.20,
         dict(synth="brief hot flame dispersion",seed=106213,band_hz=[720,3800],attack=.006,decay=.070)),
    ]
    x = mix(layers,.64)
    x = filter_audio(np.tanh(norm(x)*1.3),9400,190)
    x = fade(x-np.mean(x),.001,.055)
    x = norm(x)*10**(-9.8/20)  # Same peak as V2, not merely louder.
    data = wav_bytes(x)
    pcm = read_wav(data)
    old = read_wav((V2/"fireball_v2_impact.wav").read_bytes())
    old_stats, new_stats = measure(old), measure(pcm)
    assert np.all(np.isfinite(x)) and pcm[0] == pcm[-1] == 0
    assert abs(np.mean(pcm)) < .001 and .1 < np.max(np.abs(pcm)) < .4
    assert new_stats["max_sample_delta"] < .3 and new_stats["rms_dbfs"] > -32
    assert new_stats["below_250_hz_energy_fraction"] < old_stats["below_250_hz_energy_fraction"]*.5
    assert new_stats["above_1500_hz_energy_fraction"] > old_stats["above_1500_hz_energy_fraction"]
    ASSETS.mkdir(parents=True,exist_ok=True)
    OUT.mkdir(parents=True,exist_ok=True)
    target = ASSETS/"fireball_v3_impact.wav"
    if target.exists() and target.read_bytes() != data:
        raise ValueError("Refusing to replace modified V3 audio")
    target.write_bytes(data)
    release_data = (V2/"fireball_v2_release.wav").read_bytes()
    release = read_wav(release_data)
    preview = np.zeros(round(1.1*RATE)+len(pcm))
    preview[:len(release)] += release
    preview[round(1.1*RATE):] += pcm
    (OUT/"fireball-v3-sequence.wav").write_bytes(wav_bytes(preview))
    (OUT/"impact-v2-v3-comparison.wav").write_bytes(wav_bytes(np.concatenate([old,np.zeros(round(.8*RATE)),pcm])))
    manifest = dict(version=3,revision="Only the explosion is revised; V2 release unchanged",
                    runtime_integration=False,method="CC0 source editing and procedural layering",
                    changes=["remove original low rumble layer","reduce low-frequency body",
                             "emphasize middle/high flame rupture","shorten decay from 0.98 to 0.64 seconds"],
                    file=target.name,audio_path="Fireball_PrototypeV3/"+target.name,
                    sha256=hashlib.sha256(data).hexdigest(),source_files=records,
                    sample_rate=RATE,channels=1,bits=16,
                    original_release_file="Fireball_PrototypeV2/fireball_v2_release.wav",
                    original_release_sha256=hashlib.sha256(release_data).hexdigest(),
                    layers=[dict(info,at_seconds=at,gain=gain) for _,at,gain,info in layers],
                    master=dict(highpass_hz=190,lowpass_hz=9400,drive=1.3,peak_dbfs=-9.8),
                    old_impact=old_stats,new_impact=new_stats,
                    comparison_order=["old V2","0.8 seconds silence","new V3"],
                    sequence_timing=dict(release_seconds=0,impact_seconds=1.1,illustrative_only=True))
    (OUT/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    credits = "Fireball impact V3 — brighter/shorter explosion only.\n\n"
    credits += "V2 release and old explosion files are unchanged; no Effect or skill graph was modified.\n"
    credits += "Edited/layered CC0 audio with procedural noise; not a text-to-audio model.\n\n"
    for r in records:
        credits += f"{r['key']} — {r['author']} — {r['license']}\n{r['page']}\n"
    credits += "\nCC0: https://creativecommons.org/publicdomain/zero/1.0/\n"
    (ASSETS/"SOURCES.txt").write_text(credits,encoding="utf-8")
    (OUT/"SOURCES.txt").write_text(credits,encoding="utf-8")
    for path,expected_hash in protected.items():
        assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected_hash
    validation = dict(pcm_valid=True,clipped_samples=0,endpoints_zero=True,
                      protected_files=len(protected),existing_files_unchanged=True,
                      release_unchanged=True,old=old_stats,new=new_stats)
    (OUT/"validation.json").write_text(json.dumps(validation,indent=2),encoding="utf-8")
    print(json.dumps(validation,indent=2))


if __name__ == "__main__":
    main()
