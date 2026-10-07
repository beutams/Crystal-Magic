"""Reproducible UI SFX sketches. Requires numpy; no external audio samples.

Writes 48 kHz, signed 16-bit mono WAV masters, a manifest, an audition medley,
and a compact preview containing losslessly gzipped copies of the same WAVs.
This is procedural synthesis, not recorded Foley or model-generated audio.
"""
from __future__ import annotations

import argparse
import base64
import gzip
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile

import numpy as np

RATE = 48000
ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets/Res/Audio/SFX/UI"
OUTPUT = ROOT / "output/audio/ui-v1"

# number, id, Chinese name, group, duration, target peak dBFS, intended trigger
SPECS = [
    (1, "select", "选中 / 聚焦", "通用", .085, -20, "主菜单、书签、技能链切换；不用于背包逐格悬停"),
    (2, "confirm", "点击 / 确认", "通用", .14, -14, "普通按钮、选择存档、确认交互"),
    (3, "back", "返回 / 取消", "通用", .17, -16, "返回、Esc、取消拖拽；合书时用专用音"),
    (4, "panel_open", "面板打开", "通用", .26, -16, "商店、仓库、设置、存档、确认框、增幅选择面板"),
    (5, "adjust", "数值调整", "通用", .065, -22, "数量加减、滑杆离散步进或松开；不逐帧播放"),
    (6, "denied", "操作无效", "通用", .24, -16, "钱不足、背包满、槽位不符、联机操作失败"),
    (7, "success", "操作完成", "通用", .38, -15, "明确的保存成功、设置保存、删除完成"),
    (8, "book_open", "打开书本", "书本", .48, -15, "CharacterUI 打开"),
    (9, "book_close", "合上书本", "书本", .37, -14, "CharacterUI 关闭"),
    (10, "page_turn", "翻页", "书本", .34, -17, "装备、技能、图鉴、设置书签切页；重复点击不播放"),
    (11, "item_grab", "拿起物品", "物品", .10, -18, "实际开始拖动物品"),
    (12, "item_place", "放下物品", "物品", .15, -16, "移动、合并、卸下、仓库存取、道具配置、技能排序"),
    (13, "equip", "装备扣合", "物品", .23, -13, "武器、饰品装备或替换成功"),
    (14, "magic_socket", "魔法嵌入", "物品", .49, -14, "技能石装入、增幅选择成功"),
    (15, "organize", "整理背包", "物品", .34, -17, "背包、仓库、商店背包整理完成"),
    (16, "coins", "金币", "物品", .36, -15, "买卖成功、拾取金币；批量合并触发"),
    (17, "pickup", "获得物品", "通知", .33, -16, "物品获得提示；连续拾取需限频"),
    (18, "threat", "威胁提示", "通知", .53, -16, "威胁出现或跨过关键阈值；不用于进度每次更新"),
    (19, "warning", "重要警告", "通知", .64, -15, "复仇通知、意外断线等重要提示"),
    (20, "victory", "成功结算", "结算", 1.45, -12, "DungeonSettlementUI 成功时一次"),
    (21, "defeat", "失败结算", "结算", 1.15, -15, "DungeonSettlementUI 失败时一次"),
    (22, "member_join", "成员加入", "联机", .42, -16, "创建或加入成功、新队友进入；不在列表刷新时播放"),
    (23, "member_leave", "成员离开", "联机", .40, -18, "成员实际离开"),
    (24, "depart", "准备出发", "联机", .88, -13, "确认进入战斗或地牢成功时一次"),
]


def timeline(duration: float) -> np.ndarray:
    return np.arange(round(duration * RATE), dtype=np.float64) / RATE


def edge_fade(x: np.ndarray, attack: float = .0015, release: float = .025) -> np.ndarray:
    x = x.copy()
    a, r = min(len(x), round(attack * RATE)), min(len(x), round(release * RATE))
    if a:
        x[:a] *= np.sin(np.linspace(0, np.pi / 2, a)) ** 2
    if r:
        x[-r:] *= np.cos(np.linspace(0, np.pi / 2, r)) ** 2
    x[0] = x[-1] = 0
    return x


def noise(duration: float, rng: np.random.Generator, low=180, high=4200) -> np.ndarray:
    t = timeline(duration)
    f = np.fft.rfftfreq(len(t), 1 / RATE)
    spectrum = np.fft.rfft(rng.normal(size=len(t)))
    mask = (1 - np.exp(-(f / low) ** 4)) * np.exp(-(f / high) ** 4)
    x = np.fft.irfft(spectrum * mask, n=len(t))
    return x / max(np.sqrt(np.mean(x * x)), 1e-9)


def hit(duration: float, frequency: float, material="wood") -> np.ndarray:
    t = timeline(duration)
    ratios, weights, decay = {
        "wood": ([1, 1.61, 2.72, 4.05], [1, .38, .16, .045], .022),
        "metal": ([1, 1.47, 2.08, 2.63, 3.91], [1, .38, .23, .12, .045], .07),
        "felt": ([1, 1.97, 3.03], [1, .17, .045], .042),
    }[material]
    x = np.zeros_like(t)
    for i, (ratio, weight) in enumerate(zip(ratios, weights)):
        x += weight * np.sin(2 * np.pi * frequency * ratio * t) * np.exp(-t / (decay / (1 + i * .32)))
    return edge_fade(x, .0008, .015)


def bell(duration: float, frequency: float, warmth=1.0) -> np.ndarray:
    t = timeline(duration)
    x = np.zeros_like(t)
    for i, (ratio, weight) in enumerate([(1, 1), (2.002, .18), (2.997, .07), (4.17, .025)]):
        x += weight * np.sin(2 * np.pi * frequency * ratio * t) * np.exp(-t / (.14 * warmth / (1 + i * .55)))
    return edge_fade(x, .004, .05)


def paper(duration: float, rng: np.random.Generator, intensity=1.0) -> np.ndarray:
    t = timeline(duration)
    envelope = np.sin(np.pi * t / duration) ** 1.4
    grains = np.zeros_like(t)
    for center in rng.uniform(.015, duration - .015, 19):
        width = rng.uniform(.0015, .010)
        grains += rng.uniform(.03, .19) * np.exp(-.5 * ((t - center) / width) ** 2)
    envelope *= .1 + grains
    return edge_fade(noise(duration, rng, 420, 4700) * envelope * intensity, .008, .025)


def add(dst: np.ndarray, src: np.ndarray, at=0.0, gain=1.0) -> None:
    index = round(at * RATE)
    size = min(len(src), len(dst) - index)
    if size > 0:
        dst[index:index + size] += src[:size] * gain


def room(x: np.ndarray, gain=.1) -> np.ndarray:
    out = x.copy()
    for delay, level in [(.027, gain), (.053, gain * .48), (.079, gain * .22)]:
        add(out, x, delay, level)
    return out


def synth(number: int, duration: float) -> np.ndarray:
    rng = np.random.default_rng(60420 + number)
    x = np.zeros(round(duration * RATE))

    def tap(at, frequency, gain=1, material="wood"):
        add(x, hit(duration - at, frequency, material), at, gain)

    def note(at, frequency, gain=1, warmth=1):
        add(x, bell(duration - at, frequency, warmth), at, gain)

    if number == 1:
        tap(0, 860)
    elif number == 2:
        tap(0, 620)
        tap(.027, 940, .35)
    elif number == 3:
        tap(0, 770, .55)
        tap(.045, 500, 1)
    elif number == 4:
        add(x, paper(.20, rng), 0, .6)
        tap(.05, 330, .6, "felt")
        note(.075, 587.33, .18, .55)
    elif number == 5:
        tap(0, 1070)
    elif number == 6:
        tap(0, 290, 1, "felt")
        tap(.085, 245, .75, "felt")
    elif number == 7:
        note(0, 587.33, .8)
        note(.10, 880, 1)
    elif number == 8:
        add(x, paper(.40, rng), .01, .8)
        tap(0, 170, .28, "felt")
        t = timeline(.26)
        creak = np.sin(2 * np.pi * (160 * t + 65 * t * t)) * np.sin(np.pi * t / .26) ** 2
        add(x, creak, .05, .09)
        tap(.32, 240, .20, "wood")
    elif number == 9:
        add(x, paper(.24, rng), 0, .6)
        tap(.15, 125, .9, "felt")
        tap(.153, 330, .17)
    elif number == 10:
        add(x, paper(.31, rng), 0, 1)
        add(x, paper(.075, rng), .24, .18)
    elif number == 11:
        tap(0, 730, .7, "felt")
        add(x, paper(.08, rng), .005, .18)
    elif number == 12:
        tap(0, 310, 1, "felt")
        tap(.008, 640, .17)
    elif number == 13:
        tap(0, 380, 1, "wood")
        tap(.030, 1380, .23, "metal")
    elif number == 14:
        add(x, paper(.2, rng), 0, .12)
        note(.02, 440, .8, 1.15)
        note(.07, 659.25, .65, 1.15)
        note(.13, 880, .50, 1.15)
        tap(.115, 800, .14, "metal")
    elif number == 15:
        for at, freq, gain in [(0, 350, .6), (.065, 430, .7), (.13, 510, .8), (.195, 640, 1)]:
            tap(at, freq, gain)
        add(x, paper(.23, rng), .02, .16)
    elif number == 16:
        for at, freq, gain in [(0, 1730, 1), (.054, 2090, .75), (.13, 1510, .42), (.21, 1910, .23)]:
            tap(at, freq, gain, "metal")
    elif number == 17:
        tap(0, 420, .4, "felt")
        note(.023, 1174.66, .7, .9)
    elif number == 18:
        note(0, 220, 1, 1.5)
        note(.15, 293.66, .45, 1.4)
        tap(0, 145, .3, "felt")
    elif number == 19:
        note(0, 523.25, .8, 1.1)
        note(.22, 392, 1, 1.25)
        tap(.22, 180, .18, "felt")
    elif number == 20:
        for at, freq, gain in [(0, 293.66, .7), (.15, 440, .7), (.30, 587.33, .7), (.48, 739.99, .65), (.67, 880, .8)]:
            note(at, freq, gain, 2.5)
        note(.67, 587.33, .38, 2.6)
        note(.67, 293.66, .22, 2.6)
    elif number == 21:
        for at, freq, gain in [(0, 349.23, .7), (.21, 293.66, .75), (.43, 220, 1)]:
            note(at, freq, gain, 2.4)
        note(.43, 146.83, .35, 2.5)
    elif number == 22:
        note(0, 440, .75)
        note(.115, 659.25, 1, 1.1)
    elif number == 23:
        note(0, 659.25, .6)
        note(.105, 440, .8, 1.05)
    elif number == 24:
        for at, freq, gain in [(0, 293.66, .7), (.12, 440, .75), (.24, 587.33, .9)]:
            note(at, freq, gain, 1.7)
        note(.24, 880, .3, 1.7)
        add(x, paper(.30, rng), 0, .2)
    else:
        raise ValueError(number)
    if number in (7, 14, 17, 18, 19, 20, 21, 22, 23, 24):
        x = room(x, .075)
    x -= np.mean(x)
    return edge_fade(x, .001, min(.065, duration / 4))


def wav_bytes(pcm: np.ndarray) -> bytes:
    out = io.BytesIO()
    with wave.open(out, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.astype("<i2").tobytes())
    return out.getvalue()


def build(force: bool, preview_path: Path) -> None:
    expected = [ASSETS / f"ui_{s[1]}.wav" for s in SPECS]
    if not force and any(p.exists() for p in expected):
        raise SystemExit("Existing WAVs found; review them before rerunning with --force.")
    manifest, payload, chunks, generated = [], [], [], {}
    cursor = 0
    gap = np.zeros(round(RATE * .4), dtype=np.int16)
    for number, key, name, group, duration, peak_db, usage in SPECS:
        x = synth(number, duration)
        x *= 10 ** (peak_db / 20) / max(np.max(np.abs(x)), 1e-12)
        pcm = np.rint(x * 32767).astype(np.int16)
        assert np.all(np.isfinite(x)) and .001 < np.max(np.abs(x)) < .5
        assert pcm[0] == pcm[-1] == 0
        raw = wav_bytes(pcm)
        filename = f"ui_{key}.wav"
        entry = dict(number=number, id=f"ui_{key}", name=name, group=group, file=filename,
                     duration_seconds=round(len(pcm) / RATE, 4), sample_rate=RATE, channels=1,
                     bits_per_sample=16, peak_dbfs=round(20 * np.log10(max(np.max(np.abs(pcm.astype(float))) / 32768, 1e-12)), 2),
                     rms_dbfs=round(20 * np.log10(max(np.sqrt(np.mean((pcm / 32768) ** 2)), 1e-12)), 2),
                     preview_start_seconds=round(cursor / RATE, 4), usage=usage,
                     sha256=hashlib.sha256(raw).hexdigest())
        manifest.append(entry)
        payload.append(dict(number=number, id=entry["id"], name=name, group=group,
                            duration=entry["duration_seconds"], usage=usage,
                            wav_delta16_gzip=base64.b64encode(gzip.compress(
                                np.diff(np.frombuffer(raw, dtype="<u2").astype(np.int64), prepend=0)
                                .astype("<u2").tobytes(), compresslevel=9, mtime=0)).decode("ascii")))
        generated[filename] = raw
        chunks.extend([pcm, gap])
        cursor += len(pcm) + len(gap)

    template = Path(__file__).with_name("ui-sfx-preview.template.html").read_text(encoding="utf-8")
    assert template.count("__AUDIO_PAYLOAD__") == 1
    fragment = template.replace("__AUDIO_PAYLOAD__", json.dumps(payload, ensure_ascii=False, separators=(",", ":")))
    size = len(fragment.encode("utf-8"))
    if size >= 1_000_000:
        raise ValueError(f"Inline preview too large: {size}")
    ASSETS.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    for filename, raw in generated.items():
        (ASSETS / filename).write_bytes(raw)
    (OUTPUT / "ui-preview.wav").write_bytes(wav_bytes(np.concatenate(chunks[:-1])))
    manifest_json = json.dumps(dict(version=1, source="Procedural synthesis; no external audio samples", sounds=manifest), ensure_ascii=False, indent=2) + "\n"
    (OUTPUT / "manifest.json").write_text(manifest_json, encoding="utf-8")
    readme = Path(__file__).with_name("UI_SFX_README.md").read_text(encoding="utf-8")
    readme += "\n## 编号与文件\n\n| 编号 | 声音 | 文件 | 长度 | 合集起点 | 使用位置 |\n| --- | --- | --- | --- | --- | --- |\n"
    for e in manifest:
        readme += f"| {e['number']:02} | {e['name']} | {e['file']} | {e['duration_seconds']:.3f}s | {e['preview_start_seconds']:.3f}s | {e['usage']} |\n"
    (OUTPUT / "README.md").write_text(readme, encoding="utf-8")
    preview_path.write_text(fragment, encoding="utf-8")
    with zipfile.ZipFile(OUTPUT / "crystal-magic-ui-sfx-v1.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for filename, raw in generated.items():
            archive.writestr(f"WAV/{filename}", raw)
        archive.writestr("manifest.json", manifest_json)
        archive.writestr("README.md", readme)
        for filename in ("generate_ui_sfx.py", "ui-sfx-preview.template.html", "UI_SFX_README.md"):
            archive.write(Path(__file__).with_name(filename), f"source/{filename}")
    print(json.dumps(dict(count=len(manifest), total_sound_seconds=round(sum(e["duration_seconds"] for e in manifest), 3),
                          preview_fragment_bytes=size, master_folder=str(ASSETS),
                          audition=str(preview_path), archive=str(OUTPUT / "crystal-magic-ui-sfx-v1.zip")), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true", help="Explicitly replace this generated v1 pack")
    parser.add_argument("--preview", required=True, type=Path, help="Writable absolute path for audition fragment")
    args = parser.parse_args()
    build(args.force, args.preview.resolve())
