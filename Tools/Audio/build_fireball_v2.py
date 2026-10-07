"""Independent fireball launch and impact, using CC0 sources; no runtime edits."""
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile

import numpy as np

from build_ui_material_v2 import RATE, fade, filter_audio, wav_bytes

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/fireball-v2"
ASSETS = ROOT / "Assets/Res/Audio/SFX/Fireball_PrototypeV2"
SOURCE_FILES = [
    dict(key="fireball", file="output/audio/fireball-v2/sources/fireball-julien-matthey.wav",
         author="Julien Matthey", page="https://opengameart.org/content/fireball-1", license="CC0-1.0"),
    dict(key="crackle", file="output/audio/fireball-v2/sources/fire-crackling-antumdeluge.wav",
         author="AntumDeluge", page="https://opengameart.org/content/fire-crackling", license="CC0-1.0"),
    dict(key="ignition", file="output/audio/combat-v1/sources/flame_0.ogg",
         decoded="output/audio/combat-v1/sources/decoded/fire/flame_0.f32",
         author="themightyglider / qubodup", page="https://opengameart.org/content/catching-fire", license="CC0-1.0"),
]


def norm(x):
    return x / max(float(np.max(np.abs(x))), 1e-12)


def load_source(record):
    path = ROOT / record["file"]
    record["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
    if "decoded" in record:
        return np.fromfile(ROOT/record["decoded"], dtype="<f4").astype(float)
    with wave.open(str(path), "rb") as wav:
        assert wav.getsampwidth() == 2 and wav.getcomptype() == "NONE"
        raw = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float)
        raw = raw.reshape(-1, wav.getnchannels()).mean(axis=1)/32768
        record.update(original_rate=wav.getframerate(), original_channels=wav.getnchannels())
        return np.interp(np.arange(round(len(raw)*RATE/wav.getframerate()))*wav.getframerate()/RATE,
                         np.arange(len(raw)), raw)


def warped_source(raw, duration, output_times, source_times, low, high, drive=1.0):
    # Nonlinear resampling compresses the swell into a snappy launch without
    # cropping away the changing flame texture in the body and decay.
    t = np.arange(round(duration*RATE))/RATE
    source_positions = np.interp(t, output_times, source_times)*RATE
    highest_speed = max(np.diff(source_times)/np.diff(output_times))
    raw = filter_audio(raw, min(high, 16000/max(highest_speed, 1)), low)
    x = np.interp(source_positions, np.arange(len(raw)), raw)
    x = np.tanh(norm(x)*drive)
    return norm(fade(x, .0015, .05))


def noise(duration, seed, low, high, attack, decay):
    rng = np.random.default_rng(seed)
    t = np.arange(round(duration*RATE))/RATE
    x = filter_audio(rng.normal(size=len(t)), high, low)
    envelope = (1-np.exp(-t/attack))*np.exp(-t/decay)
    knots = np.linspace(0, duration, max(8, round(duration*90)))
    turbulence = np.interp(t, knots, rng.uniform(.5, 1., len(knots)))
    return norm(fade(x*envelope*turbulence, .0008, .045))


def fire_grains(raw, start, duration, speed, decay):
    positions = (start + np.arange(round(duration*RATE))/RATE*speed)*RATE
    assert positions[-1] < len(raw), "Fire-grain source is too short"
    x = np.interp(positions, np.arange(len(raw)), raw)
    x = filter_audio(x-np.mean(x), 6500, 650)
    # Compress a few exceptionally tall original pops, retaining the burning bed.
    x = norm(np.tanh(norm(x)*3.2))
    x *= np.exp(-np.arange(len(x))/RATE/decay)
    return fade(x, .009, .065)


def mix(layers, duration):
    x = np.zeros(round(duration*RATE))
    for clip, at, gain, _ in layers:
        offset = round(at*RATE)
        n = min(len(clip), len(x)-offset)
        assert n > 0
        x[offset:offset+n] += clip[:n]*gain
    return x


def make(source, impact=False):
    if not impact:
        layers = [
            (warped_source(source["fireball"], .70, [0,.055,.18,.40,.70], [.27,.48,.68,.98,1.40], 110,7800,1.35),
             0., .93, dict(source="fireball", output_times=[0,.055,.18,.40,.70], source_times=[.27,.48,.68,.98,1.40], band_hz=[110,7800], drive=1.35)),
            (noise(.18, 106201, 160, 2700, .003, .045), 0., .17,
             dict(synth="hot ignition pressure", seed=106201, band_hz=[160,2700], attack=.003, decay=.045)),
            (noise(.41, 106202, 650, 5800, .022, .15), .016, .085,
             dict(synth="brief turbulent flame jet", seed=106202, band_hz=[650,5800], attack=.022, decay=.15)),
            (fire_grains(source["crackle"], .38, .59, 1.09, .39), .10, .13,
             dict(source="crackle", start=.38, seconds=.59, speed=1.09, decay=.39, band_hz=[650,6500], drive=3.2)),
            (warped_source(source["ignition"], .14, [0,.14], [.025,.22], 300,5900), .004, .105,
             dict(source="ignition", output_times=[0,.14], source_times=[.025,.22], band_hz=[300,5900])),
        ]
        duration, peak, drive, name = .76, -12., 1.28, "release"
    else:
        layers = [
            (noise(.145, 106211, 180, 5400, .0007, .033), 0., .80,
             dict(synth="fast expanding explosion front", seed=106211, band_hz=[180,5400], attack=.0007, decay=.033)),
            (noise(.69, 106212, 55, 510, .0025, .17), .003, .86,
             dict(synth="short low pressure body", seed=106212, band_hz=[55,510], attack=.0025, decay=.17)),
            (warped_source(source["fireball"], .86, [0,.085,.27,.55,.86], [.52,.65,.92,1.27,1.69], 125,6700,1.5),
             .007, .80, dict(source="fireball", output_times=[0,.085,.27,.55,.86], source_times=[.52,.65,.92,1.27,1.69], band_hz=[125,6700], drive=1.5)),
            (fire_grains(source["crackle"], 1.52, .78, 1.0, .43), .125, .18,
             dict(source="crackle", start=1.52, seconds=.78, speed=1.0, decay=.43, band_hz=[650,6500], drive=3.2)),
            (noise(.57, 106213, 220, 1650, .016, .17), .043, .17,
             dict(synth="irregular thermal afterburst", seed=106213, band_hz=[220,1650], attack=.016, decay=.17)),
        ]
        duration, peak, drive, name = .98, -9.8, 1.45, "impact"
    x = mix(layers, duration)
    x = filter_audio(np.tanh(norm(x)*drive), 10500, 48)
    x = fade(x-np.mean(x), .0008, .05)
    x = norm(x)*10**(peak/20)
    return name, x, [dict(info, at_seconds=at, gain=gain) for _,at,gain,info in layers]


def hashes():
    paths = [ROOT/"Assets/Res/Data/SkillDataTable.json"]
    for folder in ["UI", "UI_MaterialV2", "Combat_PrototypeV1"]:
        paths.extend((ROOT/"Assets/Res/Audio/SFX"/folder).rglob("*"))
    return {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths if p.is_file()}


def main():
    original = hashes()
    source = {record["key"]: load_source(record) for record in SOURCE_FILES}
    records, clips = [], {}
    ASSETS.mkdir(parents=True, exist_ok=True)
    for impact in [False, True]:
        name, x, layers = make(source, impact)
        filename = f"fireball_v2_{name}.wav"
        data = wav_bytes(x)
        with wave.open(io.BytesIO(data), "rb") as wav:
            assert (wav.getnchannels(), wav.getframerate(), wav.getsampwidth()) == (1,RATE,2)
            pcm = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float)/32768
        assert np.all(np.isfinite(x)) and pcm[0] == pcm[-1] == 0
        assert abs(np.mean(pcm)) < .001 and .1 < np.max(np.abs(pcm)) < .4
        assert np.max(np.abs(np.diff(pcm))) < .3
        assert np.sqrt(np.mean(pcm*pcm)) > .018
        target = ASSETS/filename
        if target.exists() and target.read_bytes() != data:
            raise ValueError(f"Refusing to replace a modified audition: {target}")
        target.write_bytes(data)
        clips[name] = x
        records.append(dict(file=filename, role=name, seconds=round(len(x)/RATE,4),
                            sample_rate=RATE, channels=1, bits=16,
                            peak_dbfs=round(float(20*np.log10(np.max(np.abs(pcm)))),2),
                            rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean(pcm*pcm)))),2),
                            max_sample_delta=round(float(np.max(np.abs(np.diff(pcm)))),5),
                            sha256=hashlib.sha256(data).hexdigest(), layers=layers,
                            audio_path="Fireball_PrototypeV2/"+filename))
    assert records[0]["sha256"] != records[1]["sha256"]
    sequence = np.zeros(round(2.08*RATE))
    sequence[:len(clips["release"])] += clips["release"]
    sequence[round(1.1*RATE):] += clips["impact"]
    (OUT/"fireball-v2-sequence.wav").write_bytes(wav_bytes(sequence))
    manifest = dict(version=2, title="Fireball launch and hit explosion", source_files=SOURCE_FILES,
                    runtime_integration=False, method="CC0 source editing and procedural layering, not a text-to-audio model",
                    reference="Public launch/hit separation concept; no audio sampled from the commercial reference preview",
                    sounds=records, preview=dict(file="fireball-v2-sequence.wav", release_seconds=0, impact_seconds=1.1))
    (OUT/"manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    readme = Path(__file__).with_name("FIREBALL_V2_README.md").read_bytes()
    (OUT/"README.md").write_bytes(readme)
    (ASSETS/"SOURCES.txt").write_bytes(readme)
    assert hashes() == original, "Existing assets or skill configuration changed"
    validation = dict(pcm_valid=True, clipped_samples=0, endpoints_zero=True, independent_files=2,
                      protected_files=len(original), existing_files_unchanged=True,
                      sounds=[{k:r[k] for k in ["file","seconds","peak_dbfs","rms_dbfs","max_sample_delta"]} for r in records])
    (OUT/"validation.json").write_text(json.dumps(validation, indent=2), encoding="utf-8")
    with zipfile.ZipFile(OUT/"fireball-v2-launch-and-impact.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for record in records:
            archive.write(ASSETS/record["file"], "WAV/"+record["file"])
        for name in ["README.md","manifest.json","validation.json","fireball-v2-sequence.wav"]:
            archive.write(OUT/name, name)
        for record in SOURCE_FILES:
            archive.write(ROOT/record["file"], "source-audio/"+Path(record["file"]).name)
        for name in ["build_fireball_v2.py","build_ui_material_v2.py","FIREBALL_V2_README.md"]:
            archive.write(Path(__file__).with_name(name), "tools/"+name)
    with zipfile.ZipFile(OUT/"fireball-v2-launch-and-impact.zip") as archive:
        assert archive.testzip() is None
        for record in records:
            assert hashlib.sha256(archive.read("WAV/"+record["file"])).hexdigest() == record["sha256"]
    print(json.dumps(validation, indent=2))


if __name__ == "__main__":
    main()
