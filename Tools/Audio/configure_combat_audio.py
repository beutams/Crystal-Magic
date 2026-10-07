"""Prepare minimal, append-only JSON patches; never write live tables directly.

Existing effect indices and saved graph layout paths remain unchanged. The
baseline snapshots include all user changes present at the start of this task.
"""
import difflib
import json
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/audio/combat-full-v1"
SOUND_TYPE = "CrystalMagic.Game.Data.Effects.SpawnSoundEffectData, Assembly-CSharp"
CAST_GUID = "combat_audio_common_cast_start"
PLAYER = ["fireball_release", "pyroblast_release", "meteor_release", "ignite_release",
          "scorched_release", "detonate_release", "firepath_release", "fireshield_release",
          "waterbolt_release", "pool_release", "foam_release", "mana_release", "waterjet_release",
          "rain_release", "freeze_release", "lightning_release", "thunderfield_release",
          "lightningball_release", "thunder_release"]
FAMILIES = {
    "slime_attack": [19, 20], "slime_heavy": [42],
    "blade_swing": [21, 22, 24, 25, 29, 30, 35, 36, 50, 51, 53, 54, 61, 62],
    "heavy_swing": [23, 26, 27, 28, 31, 32, 33, 37, 38, 39, 52, 63, 64, 65, 66],
    "shield_guard": [34], "bat_attack": [40, 41], "claw_swipe": [43, 44, 46, 47],
    "beast_slam": [45], "exposed": [48], "bow_release": [49, 55, 67, 68],
    "holy_strike": [56], "heal_release": [57], "spear_thrust": [58, 59, 60],
    "dark_strike": [69], "dark_field": [70],
}


def sound(key, follow=False, volume=.8, pitch=1):
    return {"$type": SOUND_TYPE, "AudioPath": f"Combat/{key}.wav", "Channel": 1,
            "Volume": volume, "Pitch": pitch, "SpatialBlend": .85,
            "DelaySeconds": 0.0, "FollowCaster": follow, "Conditions": []}


def index_json(text):
    """Index actual JSON value spans, without canonicalizing any existing data."""
    spans = {}
    decoder = json.JSONDecoder()
    def skip(i):
        while i < len(text) and text[i].isspace(): i += 1
        return i
    def visit(i, path):
        i = skip(i)
        begin = i
        if text[i] == '{':
            i = skip(i+1)
            while text[i] != '}':
                key, end = decoder.raw_decode(text, i)
                i = skip(end)
                assert text[i] == ':'
                i = skip(visit(i+1, path+(key,)))
                if text[i] == ',': i = skip(i+1)
                else: break
            assert text[i] == '}'
            i += 1
        elif text[i] == '[':
            i = skip(i+1)
            index = 0
            while text[i] != ']':
                i = skip(visit(i, path+(index,)))
                index += 1
                if text[i] == ',': i = skip(i+1)
                else: break
            assert text[i] == ']'
            i += 1
        else:
            _, i = decoder.raw_decode(text, i)
        spans[path] = (begin, i)
        return i
    visit(0, ())
    return spans


def append_only_patch(path, additions):
    current = path.read_text(encoding="utf-8-sig")
    original = (OUT / ('before-' + path.name)).read_text(encoding="utf-8-sig")
    spans = index_json(original)
    edits = []
    for pointer, objects in additions.items():
        begin, end = spans[pointer]
        old = original[begin:end]
        line_start = original.rfind('\n', 0, begin)+1
        indent = re.match(r' *', original[line_start:begin]).group()
        child_indent = indent + '  '
        body = ',\n'.join('\n'.join(child_indent+line for line in
                          json.dumps(obj, ensure_ascii=False, indent=2).splitlines()) for obj in objects)
        if old == '[]':
            replacement = '[\n'+body+'\n'+indent+']'
            edits.append((begin, end, replacement))
        else:
            closing = old.rfind('\n')
            assert old[closing+1:] == indent+']'
            # Insert at the closing boundary, so edits in nested arrays cannot
            # overlap or be replaced by a parent-array rewrite.
            edits.append((begin+closing, begin+closing, ',\n'+body))
    updated = original
    for begin, end, replacement in sorted(edits, reverse=True):
        updated = updated[:begin]+replacement+updated[end:]
    # Parsing is checked before a patch can reach the live table.
    json.loads(updated)
    # Prove that removing the explicitly appended objects reconstructs every
    # original field, effect, condition and graph edge exactly.
    before = json.loads(original)
    after = json.loads(updated)
    for pointer, objects in sorted(additions.items(), key=lambda item: len(item[0]), reverse=True):
        array = after
        for part in pointer:
            array = array[part]
        assert array[-len(objects):] == objects
        del array[-len(objects):]
    assert after == before, 'Non-additive or non-audio change detected'
    diff = list(difflib.unified_diff(current.splitlines(), updated.splitlines(), n=3, lineterm=''))
    patch = f"*** Update File: {path.relative_to(ROOT).as_posix()}\n"
    for line in diff[2:]:
        patch += ('@@' if line.startswith('@@') else line)+'\n'
    return patch


def without_our_sounds(value):
    if isinstance(value, list):
        return [without_our_sounds(item) for item in value
                if not (isinstance(item, dict) and item.get('$type') == SOUND_TYPE
                        and item.get('AudioPath', '').startswith('Combat/'))]
    if isinstance(value, dict):
        return {key: without_our_sounds(item) for key, item in value.items()}
    return value


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    skill_path = ROOT / "Assets/Res/Data/SkillDataTable.json"
    state_path = ROOT / "Assets/Res/Data/StateScriptDataTable.json"
    if CAST_GUID in state_path.read_text(encoding='utf-8-sig'):
        from validate_combat_audio import main as validate
        validate()
        print('Already configured and verified; no patch generated or applied.')
        return
    for path in [skill_path, state_path]:
        snapshot = OUT / ("before-" + path.name)
        if snapshot.exists() and snapshot.read_bytes() != path.read_bytes():
            # A prior multi-file apply was stopped after its first file. Accept
            # only our exactly identified additions, and remove the rejected
            # extra search in the replacement patch. Unknown edits fail closed.
            baseline = json.loads(snapshot.read_text(encoding='utf-8-sig'))
            live = json.loads(path.read_text(encoding='utf-8-sig'))
            if path == skill_path:
                detonate = next(row for row in live['Rows'] if row['Id'] == 5)
                if len(detonate['EffectChain']) == 3:
                    extra = detonate['EffectChain'][-1]
                    assert extra['$type'].endswith('AreaSearchEffectData, Assembly-CSharp')
                    assert extra['OnlyNearestTarget'] is True
                    assert len(extra['OnAfterSearch']) == 1
                    assert extra['OnAfterSearch'][0]['AudioPath'] == 'Combat/explosion_medium.wav'
                    del detonate['EffectChain'][-1]
                assert without_our_sounds(live) == baseline, 'Unknown concurrent skill edits'
            else:
                raise ValueError('State graph changed concurrently; inspect before proceeding')
        if not snapshot.exists(): shutil.copy2(path, snapshot)
    skills = json.loads((OUT/'before-SkillDataTable.json').read_text(encoding="utf-8-sig"))["Rows"]
    assert {s["Id"] for s in skills} == set(range(71))
    assert not any("SpawnSoundEffectData" in json.dumps(s) for s in skills), "Existing sounds require manual reconciliation"
    additions = {}
    def add(pointer, obj): additions.setdefault(tuple(pointer), []).append(obj)
    mapping = {i: key for i, key in enumerate(PLAYER)}
    for key, ids in FAMILIES.items():
        for i in ids:
            assert i not in mapping
            mapping[i] = key
    assert set(mapping) == set(range(71))
    report = []
    for index, row in enumerate(skills):
        skill_id = row['Id']
        root = ('Rows', index, 'EffectChain')
        chain = row['EffectChain']
        volume = .85 if skill_id < 19 else .65
        add(root, sound(mapping[skill_id], follow=True, volume=volume))
        report.append(dict(id=skill_id, name=row['NameKey'], release=mapping[skill_id],
                           release_only_placeholder=not bool(chain)))
        if skill_id in (0, 1):
            add(root+(0, 'OnDestoryEffects'), sound('explosion_small' if skill_id == 0 else 'explosion_medium'))
        elif skill_id == 2:
            add(root+(0, 'OnArrivalEffects'), sound('explosion_large'))
        elif skill_id == 4:
            # Once per field tick, NOT once per found target/random fire visual.
            add(root+(0, 'OnTickEffects'), sound('scorched_tick', volume=.55))
        elif skill_id == 5:
            # Use the existing hit branch without adding a search or changing
            # target selection. Each real detonation is quieter for multi-hit use.
            add(root+(0, 'OnAfterSearch'), sound('explosion_medium', volume=.4))
        elif skill_id in (49, 55, 67, 68):
            add(root+(0, 'OnCollisionEffects'), sound('arrow_hit', volume=.65))
        elif skill_id >= 19 and skill_id not in (34, 48, 56, 57, 70):
            assert 'OnAfterSearch' in chain[0]
            add(root+(0, 'OnAfterSearch'), sound('melee_hit', volume=.45))
    skill_patch = append_only_patch(skill_path, additions)
    states = json.loads(state_path.read_text(encoding="utf-8-sig"))['Rows']
    ri = next(i for i, row in enumerate(states) if row['Id'] == 2)
    gi = next(i for i, g in enumerate(states[ri]['Graphs']) if g['Name'] == 'SkillChain')
    graph = states[ri]['Graphs'][gi]
    timer = next(n for n in graph['Nodes'] if n['Guid'] == '8c7c2c43f1ed42dc8b3d06fd3619cacc')
    starts = [e for e in graph['Edges'] if e['InputNodeGuid'] == timer['Guid'] and e['InputPortName'] == 'Start']
    assert len(starts) == 1 and starts[0]['OutputPortName'] == 'True'
    node = dict(Type='ExecuteEffect', Guid=CAST_GUID, ExecutionTargets=5,
                EditorPosition={'x': timer['EditorPosition']['x'], 'y': timer['EditorPosition']['y']+440},
                OriginSource=1, Position={'Kind': 1, 'GetterKey': 'unit.transform.position', 'Inputs': []},
                SourceSkillId={'Kind': 1, 'GetterKey': 'player.skill.currentSkillId', 'Inputs': []},
                RepeatCount=1, Effects=[sound('cast_common', follow=True, volume=.7)])
    edge = dict(starts[0], InputNodeGuid=CAST_GUID, InputPortName='In')
    base = ('Rows', ri, 'Graphs', gi)
    state_patch = append_only_patch(state_path, {base+('Nodes',): [node], base+('Edges',): [edge]})
    (OUT/'integration.patch').write_text('*** Begin Patch\n'+skill_patch+state_patch+'*** End Patch\n', encoding='utf-8')
    (OUT/'integration.json').write_text(json.dumps(dict(skills=report, cast_graph=graph['Guid'],
        cast_node=CAST_GUID, cast_execution_targets='Standalone | Server; network Sound event to clients'),
        ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"Prepared append-only patches for {len(skills)} skills + common chant start; no runtime code changed.")


if __name__ == '__main__':
    main()
