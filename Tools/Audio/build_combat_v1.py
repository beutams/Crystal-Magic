"""Six one-shot combat auditions: CC0 elemental textures + procedural layers.

Does not modify skill graphs or runtime playback. Refuses to replace changed WAVs.
Uses only numpy and Python standard library. Deterministic seeds and source hashes.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile

import numpy as np

from build_ui_material_v2 import RATE, fade, filter_audio, wav_bytes

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/combat-v1"
SOURCES = OUT / "sources"
ASSETS = ROOT / "Assets/Res/Audio/SFX/Combat_PrototypeV1"
LAYERS = []


def normalize(x):
    return x / max(float(np.max(np.abs(x))), 1e-12)


def put(dst, clip, at=0., gain=1.):
    offset = round(at * RATE)
    length = min(len(clip), len(dst) - offset)
    if length <= 0:
        raise ValueError("Layer falls outside output")
    dst[offset:offset + length] += clip[:length] * gain
    LAYERS[-1].update(at_seconds=at, gain=gain)


def source(pack, name, start=0., end=None, speed=1., cutoff=6500., highpass=90., reverse=False):
    raw = np.fromfile(SOURCES / "decoded" / pack / (name + ".f32"), dtype="<f4").astype(float)
    active = np.flatnonzero(np.abs(raw) > np.max(np.abs(raw)) * .008)
    offset = max(0, int(active[0]) - round(.002 * RATE))
    raw = raw[offset:]
    if end is None:
        end = len(raw) / RATE
    x = raw[round(start * RATE):round(end * RATE)]
    if len(x) < 32:
        raise ValueError(f"Empty source segment: {pack}/{name}")
    if reverse:
        x = x[::-1]
    x = filter_audio(x - np.mean(x), min(cutoff, 17000 / max(speed, 1)), highpass)
    x = np.interp(np.arange(0, len(x)-1, speed), np.arange(len(x)), x)
    LAYERS.append(dict(source=f"{pack}/{name}", onset_trim_seconds=offset/RATE,
                       trim_seconds=[start, end], playback_speed=speed, reverse=reverse,
                       lowpass_hz=cutoff, highpass_hz=highpass))
    return normalize(fade(x, .001, .016))


def noise_body(duration, seed, low, high, attack=.009, decay=.13):
    rng = np.random.default_rng(seed)
    t = np.arange(round(duration * RATE)) / RATE
    x = filter_audio(rng.normal(size=len(t)), high, low)
    envelope = (1 - np.exp(-t / attack)) * np.exp(-t / decay)
    # Low-frequency irregular turbulence, not a pitched oscillator.
    knots = np.linspace(0, duration, max(6, round(duration * 65)))
    envelope *= np.interp(t, knots, rng.uniform(.6, 1., len(knots)))
    LAYERS.append(dict(synth="band-limited turbulent pressure", seed=seed,
                       seconds=duration, band_hz=[low, high], attack=attack, decay=decay))
    return normalize(fade(x * envelope, .001, .025))


def air_gesture(duration, seed, rise=False):
    rng = np.random.default_rng(seed)
    t = np.arange(round(duration * RATE)) / RATE
    x = np.zeros(len(t))
    centers = np.geomspace(260, 3500, 8)
    for index, frequency in enumerate(centers):
        u = index / (len(centers)-1)
        center_time = duration * (.12 + .68 * (u if rise else 1-u))
        envelope = np.exp(-.5 * ((t-center_time) / (duration*.22))**2)
        band = filter_audio(rng.normal(size=len(t)), frequency*1.8, frequency*.6)
        x += normalize(band) * envelope
    # Soft corners and no stable frequency: a breath/air gesture, not a chime.
    x *= np.sin(np.pi * t / duration)**.7
    LAYERS.append(dict(synth="eight overlapping filtered-noise air bands", seed=seed,
                       seconds=duration, direction="gather" if rise else "outward"))
    return normalize(fade(x, .012, .025))


def discharge(duration=.255, seed=10615):
    rng = np.random.default_rng(seed)
    x = np.zeros(round(duration * RATE))
    for at, length, gain in [(0., .027, 1.), (.023, .043, .66), (.061, .031, .45),
                              (.104, .037, .24), (.151, .048, .10)]:
        t = np.arange(round(length*RATE))/RATE
        crack = filter_audio(rng.normal(size=len(t)), 7600, 650)
        crack *= np.exp(-t / (length*.28))
        crack = fade(crack, .00035, .004)
        offset = round(at*RATE)
        x[offset:offset+len(crack)] += crack*gain
    t = np.arange(len(x))/RATE
    # Brief unstable broadband tail; intentionally no laser chirp or musical note.
    phase = np.cumsum(2*np.pi*(680+310*np.sin(2*np.pi*31*t))/RATE)
    tail = filter_audio(rng.normal(size=len(x)), 5400, 1100)
    x += tail*(.5+.5*np.sin(phase))*.18*np.exp(-t/.065)
    LAYERS.append(dict(synth="irregular electrical micro-discharges", seed=seed,
                       seconds=duration, notes="Noise bursts with a very short rough tail; no pitched carrier"))
    return normalize(fade(x, .0005, .018))


def make(number):
    LAYERS.clear()
    if number == 1:
        key, title, duration, peak = "cast_common", "通用起手：魔力聚拢", .245, -19.
        x = np.zeros(round(duration*RATE))
        put(x, air_gesture(.235, 10601, True), gain=.72)
        put(x, noise_body(.185, 10602, 180, 700, .035, .13), .027, .22)
        description = "轻、短、无元素倾向的内收气流；只在开始施法时播放一次。"
        use = "通用起手节点；不是循环，也不是吟唱完成提示。"
    elif number == 2:
        key, title, duration, peak = "fireball_release", "火球释放：短促喷焰", .355, -12.5
        x = np.zeros(round(duration*RATE))
        put(x, source("fire", "flame_0", end=.42, speed=1.30, cutoff=5800), gain=.82)
        put(x, air_gesture(.31, 10603), .004, .42)
        put(x, noise_body(.205, 10604, 120, 950, .007, .07), gain=.26)
        description = "快速推出一团火焰，保留轻微火苗纹理，尾巴短；不包含命中爆炸。"
        use = "火球创建弹体的释放节点。命中爆炸需要另一条音效。"
    elif number == 3:
        key, title, duration, peak = "pyroblast_release", "爆炎弹释放：重压喷焰", .475, -11.5
        x = np.zeros(round(duration*RATE))
        put(x, noise_body(.32, 10605, 65, 780, .004, .095), gain=.72)
        put(x, source("fire", "flame_0", start=.015, end=.43, speed=1.02, cutoff=4700), .008, .73)
        put(x, air_gesture(.39, 10606), .013, .40)
        put(x, noise_body(.21, 10607, 190, 2100, .009, .062), .049, .30)
        description = "更厚重的压力前沿与二段喷焰，强调弹体重量；不是单纯给火球降调。"
        use = "爆炎弹创建弹体的释放节点；不包含撞击爆炸。"
    elif number == 4:
        key, title, duration, peak = "waterbolt_release", "水弹释放：紧实水团", .37, -13.
        x = np.zeros(round(duration*RATE))
        put(x, source("water", "bubble_02", end=.26, speed=1.28, cutoff=5900), gain=.50)
        put(x, source("water", "splash_06", start=.015, end=.35, speed=1.12, cutoff=6500), .021, .82)
        put(x, air_gesture(.225, 10608), gain=.15)
        description = "短水腔与急流水团叠加，强调发射推力，收短落水尾声。"
        use = "水弹创建弹体的释放节点；不包含命中后大范围溅水。"
    elif number == 5:
        key, title, duration, peak = "lightning_release", "闪电释放：电弧撕裂", .27, -14.
        x = np.zeros(round(duration*RATE))
        put(x, discharge(), gain=1.)
        put(x, noise_body(.10, 10616, 160, 1050, .001, .023), gain=.16)
        description = "一次锋利短放电，后跟极短不规则电流；没有雷鸣或科幻激光音阶。"
        use = "闪电主电弧生成节点播放一次；不要给每个蔓延目标叠加完整释放声。"
    elif number == 6:
        key, title, duration, peak = "freeze_release", "冻结释放：冰层凝结", .64, -13.5
        x = np.zeros(round(duration*RATE))
        put(x, source("ice", "ice", start=.02, end=.84, speed=1.5, cutoff=7700, highpass=240), .015, .82)
        put(x, source("ice", "coldsnap", start=.08, end=.32, speed=1.16, cutoff=5200,
                      highpass=180, reverse=True), gain=.26)
        put(x, air_gesture(.38, 10617, True), gain=.16)
        description = "冰裂纹理逐步聚拢成短凝结声，保留冷硬颗粒，不叠铃铛或玻璃碎落。"
        use = "冻结范围效果发动节点播放一次，放在逐目标搜索/伤害链外。"
    else:
        raise ValueError(number)
    x = normalize(x)
    drive = {1: 1.0, 2: 1.8, 3: 2.0, 4: 4.0, 5: 2.8, 6: 8.0}[number]
    x = filter_audio(np.tanh(x*drive), 9200, 55)
    x = fade(x - np.mean(x), .001 if number != 1 else .007, .024)
    x = normalize(x) * 10**(peak/20)
    return dict(key=key, title=title, description=description, suggested_use=use,
                layers=[dict(layer) for layer in LAYERS], master_drive=drive), x


def protected_hashes():
    files = [ROOT / "Assets/Res/Data/SkillDataTable.json"]
    for directory in ["UI", "UI_MaterialV2"]:
        files.extend((ROOT / "Assets/Res/Audio/SFX" / directory).rglob("*"))
    return {str(file): hashlib.sha256(file.read_bytes()).hexdigest() for file in files if file.is_file()}


def main(revise_unreviewed=False):
    protected = protected_hashes()
    previous = {}
    if revise_unreviewed:
        old_manifest = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
        previous = {e["file"]: e["sha256"] for e in old_manifest["sounds"]}
    ASSETS.mkdir(parents=True, exist_ok=True)
    entries, sounds, unique = [], [], set()
    for number in range(1, 7):
        entry, x = make(number)
        data = wav_bytes(x)
        with wave.open(io.BytesIO(data), "rb") as wav:
            assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
            pcm = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float)/32768
        digest = hashlib.sha256(data).hexdigest()
        assert digest not in unique and np.all(np.isfinite(pcm))
        assert pcm[0] == pcm[-1] == 0 and abs(np.mean(pcm)) < .001
        assert .05 < np.max(np.abs(pcm)) < .4
        assert np.max(np.abs(np.diff(pcm))) < .30
        assert np.sqrt(np.mean(pcm*pcm)) > .008
        unique.add(digest)
        filename = "combat_v1_" + entry["key"] + ".wav"
        target = ASSETS / filename
        if target.exists() and target.read_bytes() != data:
            old_data = target.read_bytes()
            old_hash = hashlib.sha256(old_data).hexdigest()
            if not revise_unreviewed or previous.get(filename) != old_hash:
                raise ValueError(f"Refusing to replace changed audition: {target}")
            backup = OUT / "unreviewed-backups" / old_hash / filename
            backup.parent.mkdir(parents=True, exist_ok=True)
            backup.write_bytes(old_data)
        target.write_bytes(data)
        entry.update(number=number, file=filename, sample_rate=RATE, channels=1, bits=16,
                     seconds=round(len(x)/RATE, 4), sha256=digest,
                     audio_path="Combat_PrototypeV1/" + filename,
                     peak_dbfs=round(float(20*np.log10(np.max(np.abs(pcm)))), 2),
                     rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean(pcm*pcm)))), 2),
                     max_sample_delta=round(float(np.max(np.abs(np.diff(pcm)))), 5))
        entries.append(entry)
        sounds.append(x)
    gap = np.zeros(round(.9*RATE))
    medley, position = [], 0
    for entry, x in zip(entries, sounds):
        entry["preview_start_seconds"] = round(position/RATE, 3)
        medley.extend([x, gap])
        position += len(x) + len(gap)
    (OUT / "combat-v1-six-preview.wav").write_bytes(wav_bytes(np.concatenate(medley[:-1])))
    # All five examples use the exact same starting sound; timing is illustrative.
    demo = []
    for x in sounds[1:]:
        demo.extend([sounds[0], np.zeros(round(.12*RATE)), x, np.zeros(round(.85*RATE))])
    (OUT / "combat-v1-cast-release-demo.wav").write_bytes(wav_bytes(np.concatenate(demo[:-1])))
    source_records = json.loads((SOURCES / "selected-sources.json").read_text(encoding="utf-8"))
    used = {layer["source"] for entry in entries for layer in entry["layers"] if "source" in layer}
    selected = [s for s in source_records if s["pack"] + "/" + Path(s["local_file"]).stem in used]
    for source_record in selected:
        assert hashlib.sha256((ROOT/source_record["local_file"]).read_bytes()).hexdigest() == source_record["sha256"]
    manifest = dict(version=1, status="Audition only; not connected to any runtime Effect",
                    method="Edited/layered CC0 audio and procedural texture synthesis, not a text-to-audio model",
                    sounds=entries, source_files=selected)
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    credits = Path(__file__).with_name("COMBAT_V1_SOURCES.txt").read_bytes()
    (OUT / "SOURCES.txt").write_bytes(credits)
    (ASSETS / "SOURCES.txt").write_bytes(credits)
    readme = "# 战斗音效第一轮试听\n\n1 个通用起手 + 5 个独立技能释放音，不包含命中爆炸，不是完整技能音效包。\n\n"
    readme += "本次只制作素材，没有修改技能表、Effect、音频组件或任何 UI 音效。\n\n"
    readme += "制作方式：CC0 元素素材剪辑叠加 + 程序合成气流/压力/电弧；不是文字转音效模型。\n\n"
    readme += "## 试听顺序\n\ncombat-v1-six-preview.wav 每个声音之间留 0.9 秒。\n\n"
    readme += "|编号|声音|起始秒数|长度|声音设计|\n|---|---|---|---|---|\n"
    for entry in entries:
        readme += f"|{entry['number']}|{entry['title']}|{entry['preview_start_seconds']}|{entry['seconds']}s|{entry['description']}|\n"
    readme += "\ncombat-v1-cast-release-demo.wav：相同起手分别接火球、爆炎弹、水弹、闪电、冻结。间隔仅供试听，不代表实际吟唱时长。\n\n"
    readme += "## 后续使用\n\n每条素材为 48 kHz / 16-bit PCM / 单声道 WAV。以下是确认后可填写的 AudioPath，目前未自动配置。\n\n"
    for entry in entries:
        readme += f"- {entry['title']}：`{entry['audio_path']}`。{entry['suggested_use']}\n"
    readme += "\n具体来源、许可、裁切区间、叠层、随机种子和文件校验值见 SOURCES.txt 与 manifest.json。\n"
    (OUT / "README.md").write_text(readme, encoding="utf-8")
    assert protected == protected_hashes(), "Existing UI assets or skill data changed"
    result = dict(count=6, unique=True, pcm_valid=True, clipped_samples=0,
                  protected_existing_files=len(protected), existing_files_unchanged=True,
                  runtime_integration=False, sounds=[{k:e[k] for k in ["number", "title", "seconds", "peak_dbfs", "rms_dbfs", "max_sample_delta"]} for e in entries])
    (OUT / "validation.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    with zipfile.ZipFile(OUT / "crystal-magic-combat-audition-v1.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for entry in entries:
            archive.write(ASSETS/entry["file"], "WAV/"+entry["file"])
        for file in ["README.md", "SOURCES.txt", "manifest.json", "validation.json", "combat-v1-six-preview.wav", "combat-v1-cast-release-demo.wav"]:
            archive.write(OUT/file, file)
        for record in selected:
            archive.write(ROOT/record["local_file"], "source-audio/"+record["pack"]+"/"+Path(record["local_file"]).name)
        for file in ["build_combat_v1.py", "build_ui_material_v2.py", "prepare_combat_sources.py", "decode_combat_sources.cjs", "COMBAT_V1_SOURCES.txt"]:
            archive.write(Path(__file__).with_name(file), "tools/"+file)
    with zipfile.ZipFile(OUT / "crystal-magic-combat-audition-v1.zip") as archive:
        assert archive.testzip() is None
        for entry in entries:
            assert hashlib.sha256(archive.read("WAV/"+entry["file"])).hexdigest() == entry["sha256"]
    print(json.dumps(result, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--revise-unreviewed", action="store_true",
                        help="Back up and revise this turn's own unreviewed drafts; verifies prior manifest hashes.")
    main(parser.parse_args().revise_unreviewed)
