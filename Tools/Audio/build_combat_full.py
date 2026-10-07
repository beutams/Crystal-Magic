"""Build the combat palette without replacing any previous auditions.

Explosions derive from the user's selected explodemini.wav, retaining its envelope
and spectrum. Other families use recorded Foley/element textures and short noise
gestures. No musical notification tones; no new text-to-audio service is used.
"""
import hashlib
import io
import json
from pathlib import Path
import wave

import numpy as np

from build_ui_material_v2 import RATE, fade, filter_audio, wav_bytes
from build_combat_v1 import air_gesture, discharge

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/combat-full-v1"
ASSETS = ROOT / "Assets/Res/Audio/SFX/Combat"
RECORDS = []
SOUNDS = {}
USED = {}


def read(path):
    path = ROOT / path
    USED[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    if path.suffix == ".f32":
        return np.fromfile(path, dtype="<f4").astype(float)
    with wave.open(str(path), "rb") as wav:
        assert wav.getsampwidth() == 2
        data = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float)
        data = data.reshape(-1, wav.getnchannels()).mean(axis=1) / 32768
        rate = wav.getframerate()
    return np.interp(np.arange(round(len(data) * RATE / rate)) * rate / RATE,
                     np.arange(len(data)), data)


def norm(x):
    return x / max(float(np.max(np.abs(x))), 1e-12)


def shift(x, speed=1):
    return np.interp(np.arange(0, len(x) - 1, speed), np.arange(len(x)), x)


def tex(pack, name, seconds, speed=1, start=0, reverse=False):
    if pack == "foley":
        path = f"output/audio/combat-full-v1/sources/{name}.f32"
    elif pack == "impact":
        path = f"output/audio/ui-v2/sources/decoded/impact-sounds/{name}.f32"
    else:
        path = f"output/audio/combat-v1/sources/decoded/{pack}/{name}.f32"
    raw = read(path)
    active = np.flatnonzero(np.abs(raw) > np.max(np.abs(raw)) * .008)
    raw = raw[max(0, active[0] - round(.002 * RATE)):]
    raw = raw[round(start * RATE):]
    x = shift(raw, speed)[:round(seconds * RATE)]
    assert len(x) > 64, path
    if reverse:
        x = x[::-1]
    return norm(fade(x - np.mean(x), .001, .03))


def mix(seconds, *layers):
    x = np.zeros(round(seconds * RATE))
    for clip, at, gain in layers:
        begin = round(at * RATE)
        count = min(len(clip), len(x) - begin)
        assert count > 0
        x[begin:begin+count] += clip[:count] * gain
    return x


def save(key, title, x, peak=-14, method="Recorded textures edited/layered with short noise gestures"):
    x = fade(x, .0003, .025)
    x = norm(x) * 10 ** (peak / 20)
    data = wav_bytes(x)
    target = ASSETS / (key + ".wav")
    if target.exists() and target.read_bytes() != data:
        raise ValueError(f"Refusing to overwrite changed asset: {target}")
    if not target.exists():
        target.write_bytes(data)
    pcm = np.frombuffer(data[44:], dtype="<i2").astype(float) / 32768
    assert np.isfinite(pcm).all() and .005 < np.max(np.abs(pcm)) < .5
    assert pcm[0] == pcm[-1] == 0
    assert abs(np.mean(pcm)) < .002
    SOUNDS[key] = x
    RECORDS.append(dict(key=key, title=title, audio_path="Combat/" + key + ".wav",
                        seconds=round(len(x)/RATE, 4), peak_dbfs=peak,
                        rms_dbfs=round(float(20*np.log10(np.sqrt(np.mean(pcm*pcm)))), 2),
                        sha256=hashlib.sha256(data).hexdigest(), method=method))


def main():
    ASSETS.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    approved = read("output/audio/explosive-reference-study/michel-baradari/explosions/explodemini.wav")
    assert USED["output/audio/explosive-reference-study/michel-baradari/explosions/explodemini.wav"] == \
        "59eac39b32c9a89dfe9f18a3477b7ad6ca780e757e0aa1d92688beb46db1803a"
    for key, title, speed, peak in [
        ("explosion_small", "小型炸药爆炸（选定模板）", 1, -10),
        ("explosion_medium", "中型炸药爆炸", .93, -9),
        ("explosion_large", "大型炸药爆炸", .85, -8.5),
    ]:
        save(key, title, shift(approved, speed), peak,
             f"Michel Baradari explodemini.wav; mono/resample/gain/fades; speed={speed}; no EQ or extra rumble")
    save("cast_common", "共用起手", read("Assets/Res/Audio/SFX/Combat_PrototypeV1/combat_v1_cast_common.wav"), -19)
    fire = read("Assets/Res/Audio/SFX/Fireball_PrototypeV2/fireball_v2_release.wav")
    save("fireball_release", "火球释放", fire, -13)
    save("pyroblast_release", "爆炎弹释放", mix(.92, (shift(fire, .88), 0, 1),
         (air_gesture(.52, 106701), .035, .12)), -12.5)
    save("meteor_release", "流星下坠", mix(.50, (air_gesture(.49, 106702), 0, .9),
         (tex("fire", "flame_0", .4, .92), .05, .3)), -15)
    save("ignite_release", "点燃", tex("fire", "flame_0", .42, 1.15), -15)
    save("scorched_release", "焦土展开", mix(.85, (shift(fire, .95), 0, .8),
         (air_gesture(.66, 106703), .03, .3)), -16)
    save("scorched_tick", "焦土脉冲", tex("fire", "flame_0", .29, 1.3), -23)
    save("detonate_release", "引爆前沿", air_gesture(.16, 106704, True), -20)
    save("firepath_release", "火径横向喷涌", mix(.75, (air_gesture(.7, 106705), 0, .8),
         (tex("fire", "flame_0", .45, .95), .04, .5)), -15)
    save("fireshield_release", "火盾环绕", mix(.65, (air_gesture(.60, 106706, True), 0, .65),
         (tex("fire", "flame_0", .46, 1.05), .09, .5)), -16)
    save("waterbolt_release", "水弹释放", read("Assets/Res/Audio/SFX/Combat_PrototypeV1/combat_v1_waterbolt_release.wav"), -14)
    save("pool_release", "水潭铺开", mix(.95, (tex("water", "splash_09", .85, .85), 0, .9),
         (tex("water", "bubble_02", .45, .92), .18, .25)), -16)
    save("foam_release", "泡沫", mix(.65, (tex("water", "bubble_02", .42, 1.12), 0, .8),
         (tex("water", "bubble_02", .32, 1.35), .2, .4)), -16)
    save("mana_release", "魔力回流", mix(.70, (tex("water", "bubble_02", .44, .83, reverse=True), 0, .45),
         (air_gesture(.63, 106707, True), .02, .55)), -17)
    save("waterjet_release", "水流喷射", mix(.55, (tex("water", "splash_13", .54, 1.12), 0, .85),
         (air_gesture(.48, 106708), .01, .22)), -14)
    save("rain_release", "降雨起落", mix(1.05, (tex("water", "splash_03", .7, .9), 0, .4),
         (tex("water", "splash_01", .64, 1.3), .24, .45),
         (tex("water", "splash_06", .42, 1.4), .56, .22)), -18)
    save("freeze_release", "冻结", read("Assets/Res/Audio/SFX/Combat_PrototypeV1/combat_v1_freeze_release.wav"), -14.5)
    save("lightning_release", "闪电", read("Assets/Res/Audio/SFX/Combat_PrototypeV1/combat_v1_lightning_release.wav"), -15)
    save("thunderfield_release", "雷场展开", mix(.7, (discharge(.255, 106709), 0, .7),
         (discharge(.255, 106710), .17, .45), (discharge(.255, 106711), .4, .23)), -16)
    save("lightningball_release", "闪电球", mix(.48, (discharge(.255, 106712), 0, .7),
         (air_gesture(.4, 106713), .03, .3)), -15)
    save("thunder_release", "打雷", mix(1.1, (discharge(.255, 106714), 0, .55),
         (norm(shift(approved, 1.02))[:round(RATE*1.04)], .035, .65)), -13)
    blade = tex("foley", "knifeSlice", .37, 1.05)
    heavy = tex("foley", "knifeSlice2", .5, .82)
    save("blade_swing", "刀剑挥击", mix(.4, (blade, 0, 1), (air_gesture(.23, 106715), 0, .16)), -17)
    save("heavy_swing", "重武器挥击", mix(.53, (heavy, 0, .85), (air_gesture(.4, 106716), .02, .4)), -16)
    save("spear_thrust", "长枪突刺", mix(.30, (tex("foley", "knifeSlice", .27, 1.42), 0, 1),
         (tex("foley", "drawKnife1", .15, 1.5), .01, .15)), -17)
    save("bow_release", "弓弦释放", mix(.28, (air_gesture(.17, 106717), .012, .55),
         (tex("impact", "impactWood_light_003", .12, 1.35), 0, .25)), -17)
    save("arrow_hit", "箭矢命中", mix(.2, (tex("foley", "chop", .19, 1.3), 0, .8),
         (tex("impact", "impactWood_light_002", .1), .015, .18)), -21)
    save("melee_hit", "近战命中", tex("foley", "chop", .26, .98), -23)
    save("slime_attack", "史莱姆弹跳攻击", mix(.43, (tex("water", "bubble_02", .30, .8), 0, .7),
         (tex("water", "splash_06", .29, 1.15), .08, .5)), -18)
    save("slime_heavy", "史莱姆重砸", mix(.57, (tex("water", "splash_09", .5, .8), .035, .85),
         (tex("impact", "impactSoft_heavy_000", .29, .85), 0, .4)), -17)
    save("claw_swipe", "兽爪撕击", mix(.36, (tex("foley", "knifeSlice2", .3, 1.2), 0, .65),
         (air_gesture(.23, 106718), .02, .35)), -18)
    save("beast_slam", "猛兽重击", mix(.48, (tex("impact", "impactSoft_heavy_000", .36, .8), .03, .7),
         (tex("foley", "chop", .35, .85), 0, .5)), -17)
    save("bat_attack", "蝙蝠掠击", mix(.29, (air_gesture(.23, 106719), 0, .8),
         (tex("foley", "knifeSlice", .16, 1.75), .02, .18)), -20)
    save("shield_guard", "举盾", tex("foley", "metalPot1", .23, .87), -20)
    save("exposed", "力竭破绽", air_gesture(.3, 106720), -22)
    save("holy_strike", "神圣打击", mix(.50, (tex("ice", "ice", .43, 1.45, .03), .015, .7),
         (air_gesture(.30, 106721), 0, .3)), -17)
    save("heal_release", "治疗展开", mix(.70, (tex("ice", "coldsnap", .52, 1.05, .08, True), .06, .3),
         (air_gesture(.64, 106722, True), 0, .6)), -18)
    save("dark_strike", "亡灵攻击", mix(.45, (tex("foley", "knifeSlice2", .37, .9), 0, .6),
         (tex("ice", "coldsnap", .37, .82, .05, True), .05, .3)), -17)
    save("dark_field", "亡灵法阵", mix(.8, (air_gesture(.7, 106723, True), 0, .65),
         (tex("ice", "coldsnap", .65, .8, .03, True), .05, .5)), -18)
    manifest = dict(method="Licensed stock edits, layering, and deterministic noise gestures",
                    approved_explosion="Michel Baradari / explodemini.wav / CC-BY-3.0",
                    sounds=RECORDS, source_sha256=USED)
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    # Small, ordered audition files instead of one long unlabelled reel.
    for name, keys in {
        "explosions": ["explosion_small", "explosion_medium", "explosion_large"],
        "fire": ["fireball_release", "pyroblast_release", "meteor_release", "ignite_release", "scorched_release", "firepath_release", "fireshield_release"],
        "water-lightning": ["waterbolt_release", "pool_release", "foam_release", "mana_release", "waterjet_release", "rain_release", "freeze_release", "lightning_release", "thunderfield_release", "lightningball_release", "thunder_release"],
        "weapons": ["blade_swing", "heavy_swing", "spear_thrust", "bow_release", "slime_attack", "beast_slam", "holy_strike", "dark_field"],
    }.items():
        parts = []
        for key in keys:
            parts.extend([SOUNDS[key], np.zeros(round(.6*RATE))])
        (OUT / ("preview-" + name + ".wav")).write_bytes(wav_bytes(np.concatenate(parts[:-1])))
    print(json.dumps(dict(sounds=len(RECORDS), seconds=sum(x["seconds"] for x in RECORDS),
                         approved_source_preserved=True), indent=2))


if __name__ == "__main__":
    main()
