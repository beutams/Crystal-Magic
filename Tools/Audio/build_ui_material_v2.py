"""Six material-led UI sketches from Kenney CC0 sources; preserves v1.

Input: decoded float32 mono/48k files from decode_ui_material_sources.cjs.
No musical notes or chords. Only the magic-socket air layer is synthesized.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/ui-v2"
SOURCES = OUT / "sources"
ASSETS = ROOT / "Assets/Res/Audio/SFX/UI_MaterialV2"
RATE = 48000
used_layers = []


def fade(x, attack=.0006, release=.009):
    x = x.copy()
    a, r = min(len(x), round(attack * RATE)), min(len(x), round(release * RATE))
    if a:
        x[:a] *= np.sin(np.linspace(0, np.pi / 2, a)) ** 2
    if r:
        x[-r:] *= np.cos(np.linspace(0, np.pi / 2, r)) ** 2
    x[0] = x[-1] = 0
    return x


def filter_audio(x, cutoff=6000, highpass=90):
    # Zero-padding keeps the FFT filter's circular wrap outside the useful clip.
    padding = RATE // 20
    expanded = np.pad(x, (padding, padding))
    f = np.fft.rfftfreq(len(expanded), 1 / RATE)
    transfer = np.exp(-(f / cutoff) ** 4) * (1 - np.exp(-(f / highpass) ** 4))
    result = np.fft.irfft(np.fft.rfft(expanded) * transfer, n=len(expanded))
    return result[padding:padding + len(x)]


def source(pack, name, start=0, end=None, speed=1, cutoff=6000, reverse=False):
    x = np.fromfile(SOURCES / "decoded" / pack / f"{name}.f32", dtype="<f4").astype(float)
    if end is None:
        end = len(x) / RATE
    x = x[round(start * RATE):round(end * RATE)]
    if reverse:
        x = x[::-1]
    x -= np.mean(x)
    x = filter_audio(x, min(cutoff, 19000 / max(speed, 1)))
    x = np.interp(np.arange(0, len(x) - 1, speed), np.arange(len(x)), x)
    x = fade(x)
    x /= max(np.max(np.abs(x)), 1e-10)
    used_layers.append(dict(pack=pack, source=f"Audio/{name}.ogg", trim_seconds=[start, end],
                            playback_speed=speed, lowpass_hz=cutoff, reverse=reverse))
    return x


def layer(dst, clip, at=0, gain=1):
    offset = round(at * RATE)
    size = min(len(clip), len(dst) - offset)
    assert size > 0
    dst[offset:offset + size] += clip[:size] * gain
    used_layers[-1].update(at_seconds=at, gain=gain)


def air_snap(duration=.11):
    # Short, nonpitched, band-filtered energy gesture, not a bell or arpeggio.
    rng = np.random.default_rng(600602)
    t = np.arange(round(duration * RATE)) / RATE
    x = filter_audio(rng.normal(size=len(t)), 7200, 1100)
    x *= (np.sin(np.pi * t / duration) ** 1.6) * (.65 + .35 * np.sin(2 * np.pi * 47 * t) ** 2)
    x /= max(np.max(np.abs(x)), 1e-9)
    return fade(x, .009, .02)


def make(number):
    global used_layers
    used_layers = []
    if number == 1:
        key, title, duration, peak = "click", "点击：短木扣", .105, -14
        x = np.zeros(round(duration * RATE))
        layer(x, source("impact-sounds", "impactWood_light_000", 0, .11, 1.08, 6500))
        description = "轻木碰撞压成一次短扣，无音阶、无混响。"
    elif number == 2:
        key, title, duration, peak = "cancel", "取消：回收轻扣", .175, -16
        x = np.zeros(round(duration * RATE))
        layer(x, source("rpg-audio", "cloth3", .10, .25, 1.4, 4200), 0, .25)
        layer(x, source("impact-sounds", "impactWood_light_001", 0, .12, .86, 4200), .026, .8)
        description = "短布料滑动与稍闷的木扣，表现收回操作。"
    elif number == 3:
        key, title, duration, peak = "page_turn", "翻页：纸张擦动", .365, -14
        x = np.zeros(round(duration * RATE))
        layer(x, source("rpg-audio", "bookFlip2", .028, .415, 1.1, 7600))
        description = "来自 bookFlip2 的纸页声，去前静音、略加快并收尾，不加电子音。"
    elif number == 4:
        key, title, duration, peak = "item_place", "放物品：皮革落槽", .190, -14
        x = np.zeros(round(duration * RATE))
        layer(x, source("rpg-audio", "dropLeather", .085, .290, 1.3, 5200), 0, .8)
        layer(x, source("rpg-audio", "bookPlace1", .053, .17, 1.25, 4300), .010, .26)
        description = "皮革落下叠加轻落物，保留触碰和摩擦，不做奖励提示。"
    elif number == 5:
        key, title, duration, peak = "equip", "装备：金属扣合", .265, -12
        x = np.zeros(round(duration * RATE))
        layer(x, source("rpg-audio", "beltHandle2", .09, .35, 1.20, 5700), 0, .27)
        layer(x, source("rpg-audio", "metalLatch", .035, .235, 1.04, 6900), .035, .85)
        description = "皮带操作声作底，金属锁扣作为确认瞬间。"
    elif number == 6:
        key, title, duration, peak = "magic_socket", "魔法嵌入：扣入微闪", .320, -13
        x = np.zeros(round(duration * RATE))
        layer(x, source("impact-sounds", "impactWood_light_002", 0, .10, .95, 5000), 0, .55)
        layer(x, source("impact-sounds", "impactGlass_light_000", 0, .18, .92, 8000), .019, .50)
        layer(x, source("impact-sounds", "impactGlass_light_001", 0, .12, 1.21, 7200), .061, .17)
        used_layers.append(dict(synth="nonpitched filtered air snap", seed=600602))
        layer(x, air_snap(), .011, .15)
        description = "先扣入，再带极短玻璃颗粒和无固定音高的气流，取消铃声音阶。"
    else:
        raise ValueError(number)
    # Shorten near-silent tails while retaining release; preserve the intended gesture.
    active = np.flatnonzero(np.abs(x) > max(np.max(np.abs(x)) * .003, 1e-8))
    if len(active):
        x = x[:min(len(x), active[-1] + round(.022 * RATE))]
    # Source Foley has occasional very narrow peaks. Tame those so the paper/
    # leather body remains audible instead of normalizing everything to a spike.
    peak_drive = {1: 1.3, 2: 1.8, 3: 9.0, 4: 1.2, 5: 3.2, 6: 1.0}[number]
    x /= max(np.max(np.abs(x)), 1e-12)
    x = filter_audio(np.tanh(x * peak_drive), 9500, 75)
    used_layers.append(dict(master_processing="soft transient saturation", drive=peak_drive))
    x = fade(x - np.mean(x), .0004, .012)
    x *= 10 ** (peak / 20) / max(np.max(np.abs(x)), 1e-12)
    return key, title, description, x, list(used_layers)


def wav_bytes(x):
    pcm = np.rint(np.clip(x, -.9999, .9999) * 32767).astype("<i2")
    stream = io.BytesIO()
    with wave.open(stream, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.tobytes())
    return stream.getvalue()


def main(force):
    names = ["click", "cancel", "page_turn", "item_place", "equip", "magic_socket"]
    if not force and any((ASSETS / f"ui_v2_{name}.wav").exists() for name in names):
        raise SystemExit("V2 exists. Inspect before explicitly replacing with --force.")
    old_manifest = json.loads((ROOT / "output/audio/ui-v1/manifest.json").read_text(encoding="utf-8"))
    for entry in old_manifest["sounds"]:
        assert hashlib.sha256((ROOT / "Assets/Res/Audio/SFX/UI" / entry["file"]).read_bytes()).hexdigest() == entry["sha256"]
    ASSETS.mkdir(parents=True, exist_ok=True)
    records, medley, ab_medley, hashes = [], [], [], set()
    gap = np.zeros(round(.6 * RATE))
    used_source_names = set()
    for number, old_id in enumerate(["confirm", "back", "page_turn", "item_place", "equip", "magic_socket"], 1):
        key, title, description, x, layers = make(number)
        assert np.all(np.isfinite(x)) and x[0] == x[-1] == 0
        assert .03 < np.max(np.abs(x)) < .4 and abs(np.mean(x)) < .001
        data = wav_bytes(x)
        digest = hashlib.sha256(data).hexdigest()
        assert digest not in hashes
        hashes.add(digest)
        filename = f"ui_v2_{key}.wav"
        (ASSETS / filename).write_bytes(data)
        for used in layers:
            if "pack" in used:
                used_source_names.add((used["pack"], used["source"]))
        records.append(dict(number=number, title=title, file=filename, description=description,
                            sample_rate=RATE, channels=1, bits=16, seconds=round(len(x)/RATE,4),
                            peak_dbfs=round(20*np.log10(np.max(np.abs(x))),2),
                            rms_dbfs=round(20*np.log10(np.sqrt(np.mean(x*x))),2),
                            sha256=digest, layers=layers))
        medley.extend([x, gap])
        with wave.open(str(ROOT / "Assets/Res/Audio/SFX/UI" / f"ui_{old_id}.wav"), "rb") as wav:
            old = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float) / 32768
        # Loudness-matched by peak solely for comparison; master files are unmodified.
        old *= np.max(np.abs(x)) / max(np.max(np.abs(old)), 1e-9)
        ab_medley.extend([old, gap, x, np.zeros(RATE)])
    (OUT / "ui-v2-preview.wav").write_bytes(wav_bytes(np.concatenate(medley[:-1])))
    (OUT / "ui-v1-v2-comparison.wav").write_bytes(wav_bytes(np.concatenate(ab_medley[:-1])))
    source_records = json.loads((SOURCES / "selected-sources.json").read_text(encoding="utf-8"))
    manifest = dict(version=2, source="Edited/layered Kenney CC0 assets, plus one procedural nonpitched air layer",
                    integration="Preview only; no UI event bindings changed", sounds=records,
                    source_files=[r for r in source_records if (r["pack"],r["archive_member"]) in used_source_names])
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    readme = Path(__file__).with_name("UI_MATERIAL_V2_README.md").read_text(encoding="utf-8")
    readme += "\n## 六个试样\n\n|编号|音效|文件|长度|处理|\n|---|---|---|---|---|\n"
    for entry in records:
        readme += f"|{entry['number']:02}|{entry['title']}|{entry['file']}|{entry['seconds']:.3f}s|{entry['description']}|\n"
    (OUT / "README.md").write_text(readme, encoding="utf-8")
    with zipfile.ZipFile(OUT / "crystal-magic-ui-material-v2.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for record in records:
            archive.write(ASSETS / record["file"], "WAV/" + record["file"])
        for file in ["README.md", "manifest.json", "ui-v2-preview.wav", "ui-v1-v2-comparison.wav"]:
            archive.write(OUT / file, file)
        for pack in ["rpg-audio", "impact-sounds"]:
            archive.write(SOURCES / f"LICENSE-Kenney-{pack}.txt", f"LICENSE-Kenney-{pack}.txt")
        for source_record in manifest["source_files"]:
            archive.write(ROOT / source_record["local_file"], "source-audio/" + source_record["pack"] + "/" + Path(source_record["local_file"]).name)
        for name in ["build_ui_material_v2.py", "prepare_ui_material_sources.py", "decode_ui_material_sources.cjs", "UI_MATERIAL_V2_README.md"]:
            archive.write(Path(__file__).with_name(name), "tools/" + name)
    with zipfile.ZipFile(OUT / "crystal-magic-ui-material-v2.zip") as archive:
        assert archive.testzip() is None
    print(json.dumps(dict(count=6, original_24_unchanged=True, folder=str(ASSETS),
                          source_files=len(used_source_names),
                          sounds=[{k:r[k] for k in ["number","title","seconds","peak_dbfs","rms_dbfs"]} for r in records]), ensure_ascii=True, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true")
    main(parser.parse_args().force)
