"""Complete the 24-cue material UI pack without rewriting the six approved WAVs.

Uses local CC0 Kenney source recordings/assets and the approved v2 DSP helpers.
No new musical synthesis, melodies, or reverb are added. Repeated builds refuse
unknown changes; identical outputs are left in place (including Unity GUIDs).
"""
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile
import numpy as np
import build_ui_material_v2 as dsp
from generate_ui_sfx import SPECS

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/ui-v2-full"
ASSETS = dsp.ASSETS
RATE = dsp.RATE
APPROVED = {"confirm": "click", "back": "cancel", "page_turn": "page_turn",
            "item_place": "item_place", "equip": "equip", "magic_socket": "magic_socket"}


def r(name, start, end, at=0, gain=1, speed=1, cutoff=6000, reverse=False):
    return dict(pack="rpg-audio", name=name, start=start, end=end, at=at,
                gain=gain, speed=speed, cutoff=cutoff, reverse=reverse)


def i(name, end=.15, at=0, gain=1, speed=1, cutoff=6000):
    return dict(pack="impact-sounds", name=name, start=0, end=end, at=at,
                gain=gain, speed=speed, cutoff=cutoff, reverse=False)


# duration, peak dBFS, transient drive, design description, source layers
RECIPES = {
    "select": (.075, -20, 1.3, "短而轻的木质触点，不加音阶。", [
        i("impactWood_light_003", .08, speed=1.22, cutoff=5600)]),
    "panel_open": (.255, -16, 2.2, "布料展开、皮带移动和轻木扣，表现拉开面板。", [
        r("cloth1", .07, .27, gain=.35, speed=1.15, cutoff=5600),
        r("beltHandle1", .08, .20, .035, .15, 1.1, 4800),
        i("impactWood_light_003", .10, .015, .65, .92, 4800)]),
    "adjust": (.060, -21, 1.5, "极短木质刻度声，音量低于普通点击。", [
        i("impactWood_light_004", .065, speed=1.4, cutoff=5000)]),
    "denied": (.235, -16, 2.4, "软闷击接阻挡木扣，表示操作受阻，不用报错音阶。", [
        i("impactSoft_medium_000", .10, speed=.80, cutoff=3400),
        i("impactWood_heavy_000", .10, .075, .40, .90, 3600)]),
    "success": (.270, -14, 3.0, "轻落物与锁扣确认，尾部仅一点玻璃颗粒。", [
        r("bookPlace1", .04, .17, gain=.55, speed=1.2, cutoff=4700),
        r("metalLatch", .035, .18, .038, .40, 1.15, 6000),
        i("impactGlass_light_002", .12, .085, .15, 1.15, 7000)]),
    "book_open": (.405, -14, 5.0, "翻开封面，书脊轻响后接纸张展开。", [
        r("bookOpen", .010, .150, gain=.75, speed=.93, cutoff=6500),
        r("creak1", .30, .52, .018, .15, 1.3, 3800),
        r("bookFlip3", .025, .215, .120, .34, 1.1, 7400)]),
    "book_close": (.300, -13, 2.3, "纸页收拢后封面落下，有厚度但不轰响。", [
        r("cloth3", .08, .23, gain=.14, speed=1.1, cutoff=4600),
        r("bookClose", .052, .230, .055, .90, .95, 5900),
        i("impactSoft_medium_000", .11, .055, .15, .90, 2900)]),
    "item_grab": (.145, -17, 3.0, "轻皮革提起与触点，区别于较重的落槽。", [
        r("handleSmallLeather2", .075, .245, gain=.85, speed=1.3, cutoff=5600),
        i("impactWood_light_003", .04, gain=.20, cutoff=5100)]),
    "organize": (.360, -16, 2.2, "纸页滑动和三个快速落槽，最后一次收稳。", [
        r("bookFlip3", .028, .205, gain=.30, speed=1.3, cutoff=6200),
        i("impactWood_light_003", .08, .015, .42, 1.08),
        i("impactWood_light_004", .08, .090, .55, .96),
        i("impactWood_light_002", .08, .168, .65, 1.02),
        r("bookPlace2", .07, .19, .210, .30, 1.3, 4600)]),
    "coins": (.345, -14, 4.0, "硬币滚动与碰撞，收短尾部，不用合成叮咚。", [
        r("handleCoins2", .014, .320, gain=.90, speed=1.02, cutoff=8000),
        r("handleCoins", .12, .23, .100, .14, 1.22, 7000)]),
    "pickup": (.255, -16, 1.7, "小物件收入袋中，带一次短而细的拾取亮点。", [
        r("handleSmallLeather", .052, .225, gain=.50, speed=1.25, cutoff=5600),
        i("impactGlass_light_002", .17, .050, .50, 1.06, 7200)]),
    "threat": (.430, -16, 1.7, "低沉软击叠少量钝金属压力感，只响一次。", [
        i("impactPlate_heavy_000", .29, gain=.24, speed=.72, cutoff=2400),
        i("impactSoft_heavy_000", .30, gain=.85, speed=.85, cutoff=2800)]),
    "warning": (.470, -14, 2.3, "较清楚的锁扣与第二下钝击，区别于普通无效操作。", [
        r("metalLatch", .038, .220, gain=.80, speed=.78, cutoff=4700),
        i("impactSoft_medium_000", .10, .130, .48, .75, 2800),
        i("impactPlate_light_000", .22, .140, .25, .68, 3500)]),
    "victory": (.950, -12, 2.5, "封盖开启、木质落点与战利品硬币展开，不播放庆祝旋律。", [
        r("bookOpen", .015, .150, gain=.24, cutoff=6500),
        r("creak2", .07, .27, .020, .12, 1, 4200),
        i("impactWood_medium_000", .19, .170, .55, .90, 5600),
        r("handleCoins", .05, .70, .200, .80, 1.12, 7800),
        i("impactGlass_light_002", .20, .330, .22, 1.08, 7500),
        i("impactGlass_light_001", .14, .520, .13, .95, 6800)]),
    "defeat": (.700, -15, 2.0, "布料收落、较沉的合盖与物品落下，不用警报和下降音阶。", [
        r("clothBelt2", .07, .48, gain=.28, speed=.85, cutoff=4200),
        r("bookClose", .055, .230, .100, .80, .80, 3900),
        i("impactSoft_heavy_000", .35, .110, .60, .68, 2000),
        r("dropLeather", .09, .29, .250, .24, .90, 3400)]),
    "member_join": (.290, -16, 2.0, "布料短动和清楚入槽，微量玻璃触点表示加入。", [
        r("cloth1", .075, .18, gain=.18, speed=1.2, cutoff=5400),
        i("impactWood_light_004", .12, .024, .80, 1, 6300),
        i("impactGlass_light_001", .12, .066, .22, 1.05, 6800)]),
    "member_leave": (.260, -18, 2.2, "短皮革抽出与闷木扣，不带玻璃亮点。", [
        r("handleSmallLeather", .06, .25, gain=.55, speed=1.2, cutoff=4600, reverse=True),
        i("impactWood_light_003", .115, .035, .45, .84, 4100)]),
    "depart": (.640, -13, 2.2, "收紧行装、木质落点和锁扣，接一个短滑动表示出发。", [
        r("clothBelt2", .07, .43, gain=.25, speed=1.25, cutoff=6400, reverse=True),
        i("impactWood_medium_000", .19, .150, .60, .90, 5700),
        r("metalLatch", .035, .21, .165, .50, 1, 6700),
        r("cloth1", .075, .40, .250, .18, 1.6, 6300),
        i("impactGlass_light_002", .15, .225, .10, .95, 7200)]),
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def pcm_from_bytes(data):
    with wave.open(io.BytesIO(data), "rb") as wav:
        if (wav.getframerate(), wav.getnchannels(), wav.getsampwidth()) != (RATE, 1, 2):
            raise ValueError("Unexpected WAV format")
        return np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float) / 32768


def make(key):
    duration, peak_db, drive, description, recipe = RECIPES[key]
    dsp.used_layers = []
    x = np.zeros(round(duration * RATE))
    for layer in recipe:
        clip = dsp.source(layer["pack"], layer["name"], layer["start"], layer["end"],
                          layer["speed"], layer["cutoff"], layer["reverse"])
        dsp.layer(x, clip, layer["at"], layer["gain"])
    active = np.flatnonzero(np.abs(x) > max(np.max(np.abs(x)) * .003, 1e-8))
    x = x[:min(len(x), active[-1] + round(.025 * RATE))]
    x /= max(np.max(np.abs(x)), 1e-12)
    x = dsp.filter_audio(np.tanh(x * drive), 9500, 65)
    x = dsp.fade(x - np.mean(x), .0004, .018)
    x *= 10 ** (peak_db / 20) / max(np.max(np.abs(x)), 1e-12)
    layers = list(dsp.used_layers) + [dict(master_processing="soft transient saturation", drive=drive)]
    return dsp.wav_bytes(x), description, layers


def validate_audio(data):
    x = pcm_from_bytes(data)
    assert np.all(np.isfinite(x)) and x[0] == x[-1] == 0
    assert .001 < np.max(np.abs(x)) < .4
    assert abs(np.mean(x)) < .001
    assert np.sqrt(np.mean(x*x)) > .001
    return x


def protected_files():
    approved = json.loads((ROOT / "output/audio/ui-v2/manifest.json").read_text(encoding="utf-8"))
    old = json.loads((ROOT / "output/audio/ui-v1/manifest.json").read_text(encoding="utf-8"))
    protected = []
    for manifest, folder in [(approved, ASSETS), (old, ROOT / "Assets/Res/Audio/SFX/UI")]:
        for sound in manifest["sounds"]:
            path = folder / sound["file"]
            assert digest(path.read_bytes()) == sound["sha256"], f"Protected audio changed: {path}"
            protected.append((path, sound["sha256"]))
            meta = path.with_name(path.name + ".meta")
            if meta.exists():
                protected.append((meta, digest(meta.read_bytes())))
    return approved, protected


def build():
    approved, protected = protected_files()
    approved_by_file = {s["file"]: s for s in approved["sounds"]}
    assert len(RECIPES) == 18 and len(APPROVED) == 6
    assert set(RECIPES) | set(APPROVED) == {s[1] for s in SPECS}
    generated, records, all_clips, new_clips, new_groups = {}, [], [], [], {}
    cursor, new_cursor = 0, 0
    gap = np.zeros(round(.5 * RATE))
    for number, key, name, group, _, _, usage in SPECS:
        filename = f"ui_v2_{APPROVED.get(key, key)}.wav"
        is_approved = key in APPROVED
        if is_approved:
            previous = approved_by_file[filename]
            data = (ASSETS / filename).read_bytes()
            description, layers = previous["description"], previous["layers"]
        else:
            data, description, layers = make(key)
        x = validate_audio(data)
        record = dict(number=number, id="ui_" + key, name=name, group=group, file=filename,
                      approved_unchanged=is_approved, description=description, usage=usage,
                      seconds=round(len(x)/RATE, 4), sample_rate=RATE, channels=1, bits=16,
                      peak_dbfs=round(20*np.log10(np.max(np.abs(x))), 2),
                      rms_dbfs=round(20*np.log10(np.sqrt(np.mean(x*x))), 2),
                      preview_start_seconds=round(cursor / RATE, 4), sha256=digest(data), layers=layers)
        if is_approved:
            record["approved_preview_number"] = previous["number"]
        else:
            record["new18_preview_start_seconds"] = round(new_cursor / RATE, 4)
            new_clips.extend([x, gap])
            new_cursor += len(x) + len(gap)
            new_groups.setdefault(group, []).extend([x, gap])
            generated[filename] = data
        records.append(record)
        all_clips.extend([x, gap])
        cursor += len(x) + len(gap)
    assert len({r["sha256"] for r in records}) == 24
    # Preflight everything before writing a new master. Unknown changes are never overwritten.
    for filename, data in generated.items():
        path = ASSETS / filename
        if path.exists() and path.read_bytes() != data:
            raise ValueError(f"Existing non-identical file; refusing to overwrite: {path}")
    OUT.mkdir(parents=True, exist_ok=True)
    for filename, data in generated.items():
        path = ASSETS / filename
        if not path.exists():
            path.write_bytes(data)
    (OUT / "ui-material-full24.wav").write_bytes(dsp.wav_bytes(np.concatenate(all_clips[:-1])))
    (OUT / "ui-material-new18.wav").write_bytes(dsp.wav_bytes(np.concatenate(new_clips[:-1])))
    groups = []
    group_names = {"通用":"general", "书本":"book", "物品":"items", "通知":"alerts", "结算":"settlement", "联机":"lobby"}
    for group, clips in new_groups.items():
        filename = f"preview-new-{group_names[group]}.wav"
        (OUT / filename).write_bytes(dsp.wav_bytes(np.concatenate(clips[:-1])))
        groups.append(dict(group=group, file=filename, numbers=[r["number"] for r in records if r["group"]==group and not r["approved_unchanged"]]))
    used = {(layer["pack"], layer["source"]) for record in records for layer in record["layers"] if "pack" in layer}
    sources = json.loads((dsp.SOURCES / "selected-sources.json").read_text(encoding="utf-8"))
    source_records = [s for s in sources if (s["pack"], s["archive_member"]) in used]
    assert len(source_records) == len(used)
    for source_record in source_records:
        assert digest((ROOT/source_record["local_file"]).read_bytes()) == source_record["source_sha256"]
    manifest = dict(version="2-full", source="Edited/layered Kenney CC0 assets; approved magic_socket retains its procedural air layer",
                    integration="Assets only; no UI playback bindings changed", new_count=18, approved_preserved_count=6,
                    sounds=records, grouped_new_previews=groups, source_files=source_records)
    manifest_json = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    (OUT / "manifest.json").write_text(manifest_json, encoding="utf-8")
    (ASSETS / "UI_MaterialV2.manifest.json").write_text(manifest_json, encoding="utf-8")
    readme = Path(__file__).with_name("UI_MATERIAL_FULL_README.md").read_text(encoding="utf-8")
    readme += "\n## 完整清单（编号沿用最初的 24 项）\n\n|编号|声音|状态|文件|完整试听起点|用途|\n|---|---|---|---|---|---|\n"
    for record in records:
        state = "已认可，原样保留" if record["approved_unchanged"] else "本次新增"
        readme += f"|{record['number']:02}|{record['name']}|{state}|{record['file']}|{record['preview_start_seconds']:.2f}s|{record['usage']}|\n"
    readme += "\n## 新增 18 项试听顺序\n\n" + " → ".join(f"{r['number']:02} {r['name']}" for r in records if not r["approved_unchanged"]) + "\n"
    (OUT / "README.md").write_text(readme, encoding="utf-8")
    # The package is made only from the completed manifest, never directory globs.
    with zipfile.ZipFile(OUT / "crystal-magic-ui-material-full24.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for record in records:
            archive.write(ASSETS / record["file"], "WAV/" + record["file"])
        for file in ["README.md", "manifest.json", "ui-material-full24.wav", "ui-material-new18.wav"]:
            archive.write(OUT / file, file)
        for group in groups:
            archive.write(OUT / group["file"], group["file"])
        for pack in ["rpg-audio", "impact-sounds"]:
            archive.write(dsp.SOURCES / f"LICENSE-Kenney-{pack}.txt", f"LICENSE-Kenney-{pack}.txt")
        for source_record in source_records:
            archive.write(ROOT / source_record["local_file"], "source-audio/" + source_record["pack"] + "/" + Path(source_record["local_file"]).name)
        for name in ["build_ui_material_full.py", "build_ui_material_v2.py", "generate_ui_sfx.py", "prepare_ui_material_sources.py", "decode_ui_material_sources.cjs", "UI_MATERIAL_FULL_README.md"]:
            archive.write(Path(__file__).with_name(name), "tools/" + name)
        archive.write(ROOT / "output/audio/ui-v2/manifest.json", "approved-six-manifest.json")
    with zipfile.ZipFile(OUT / "crystal-magic-ui-material-full24.zip") as archive:
        assert archive.testzip() is None
        assert len([n for n in archive.namelist() if n.startswith("WAV/")]) == 24
        for record in records:
            assert digest(archive.read("WAV/" + record["file"])) == record["sha256"]
    for path, expected in protected:
        assert digest(path.read_bytes()) == expected, f"Protected file modified: {path}"
    result = dict(count=24, added=18, approved_six_unchanged=True, old_v1_unchanged=True,
                  archive_verified=True, source_count=len(source_records), folder=str(ASSETS),
                  new18_preview_seconds=round((new_cursor-len(gap))/RATE, 3),
                  full24_preview_seconds=round((cursor-len(gap))/RATE, 3),
                  measurements=[{k:r[k] for k in ["number","file","seconds","peak_dbfs","rms_dbfs"]} for r in records])
    (OUT / "validation.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    build()
