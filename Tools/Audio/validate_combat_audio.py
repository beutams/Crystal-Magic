"""Validate configured audio, event placement, and unchanged gameplay data."""
import copy
import hashlib
import json
from pathlib import Path
import re
import wave

import numpy as np

from configure_combat_audio import ROOT, OUT, SOUND_TYPE, CAST_GUID, PLAYER, FAMILIES, without_our_sounds


def sounds(value, path=()):
    if isinstance(value, dict):
        if value.get('$type') == SOUND_TYPE:
            yield path, value
        for key, child in value.items():
            yield from sounds(child, path+(key,))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from sounds(child, path+(index,))


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    skills = load(ROOT/'Assets/Res/Data/SkillDataTable.json')
    states = load(ROOT/'Assets/Res/Data/StateScriptDataTable.json')
    original_skills = load(OUT/'before-SkillDataTable.json')
    original_states = load(OUT/'before-StateScriptDataTable.json')
    assert without_our_sounds(skills) == original_skills, 'Gameplay fields/effect order changed'
    state_copy = copy.deepcopy(states)
    graph = next(g for r in state_copy['Rows'] if r['Id'] == 2 for g in r['Graphs'] if g['Name'] == 'SkillChain')
    node = next(n for n in graph['Nodes'] if n['Guid'] == CAST_GUID)
    assert node['Type'] == 'ExecuteEffect' and node['ExecutionTargets'] == 5 and node['RepeatCount'] == 1
    assert len(node['Effects']) == 1 and node['Effects'][0]['AudioPath'] == 'Combat/cast_common.wav'
    edges = [e for e in graph['Edges'] if e['InputNodeGuid'] == CAST_GUID]
    assert len(edges) == 1 and edges[0]['OutputPortName'] == 'True'
    assert any(e['OutputNodeGuid'] == edges[0]['OutputNodeGuid'] and e['OutputPortName'] == 'True'
               and e['InputNodeGuid'] == '8c7c2c43f1ed42dc8b3d06fd3619cacc'
               and e['InputPortName'] == 'Start' for e in graph['Edges'])
    graph['Nodes'].remove(node)
    graph['Edges'].remove(edges[0])
    assert state_copy == original_states, 'Existing state graph was changed'
    expected = dict(enumerate(PLAYER))
    for key, ids in FAMILIES.items():
        expected.update({i:key for i in ids})
    for row in skills['Rows']:
        root_sounds = [e for e in row['EffectChain'] if e.get('$type') == SOUND_TYPE]
        assert len(root_sounds) == 1, row['Id']
        assert root_sounds[0]['AudioPath'] == f"Combat/{expected[row['Id']]}.wav", row['Id']
        assert root_sounds[0]['FollowCaster'] is True
    by_id = {r['Id']:r for r in skills['Rows']}
    for skill_id, event, key in [(0,'OnDestoryEffects','small'), (1,'OnDestoryEffects','medium'),
                                 (2,'OnArrivalEffects','large')]:
        root = by_id[skill_id]['EffectChain'][0]
        nested = [(p,e) for p,e in sounds(root) if 'explosion_' in e['AudioPath']]
        assert len(nested) == 1 and nested[0][0][0] == event
        assert len(nested[0][0]) == 2, 'Explosion accidentally placed once per area target'
        assert nested[0][1]['AudioPath'] == f'Combat/explosion_{key}.wav'
        assert nested[0][1]['FollowCaster'] is False
        if skill_id < 2: assert root['TriggerDestroyEffectsOnMaxRange'] is False
    detonate = by_id[5]['EffectChain']
    assert len(detonate) == 2 and detonate[0]['OnAfterSearch'][-1]['AudioPath'] == 'Combat/explosion_medium.wav'
    assert detonate[0]['OnAfterSearch'][-1]['Volume'] == .4
    for skill_id in [49,55,67,68]:
        assert by_id[skill_id]['EffectChain'][0]['OnCollisionEffects'][-1]['AudioPath'] == 'Combat/arrow_hit.wav'
    all_nodes = list(sounds(skills)) + list(sounds(states))
    referenced = set()
    for pointer, effect in all_nodes:
        assert effect['AudioPath'].startswith('Combat/')
        assert effect['Channel'] == 1 and 0 < effect['Volume'] <= 1
        assert effect['Pitch'] == 1 and effect['DelaySeconds'] == 0
        assert 0 <= effect['SpatialBlend'] <= 1
        path = ROOT/'Assets/Res/Audio/SFX'/effect['AudioPath']
        assert path.is_file(), str(path)
        referenced.add(path.stem)
    manifest = load(OUT/'manifest.json')
    keys = {s['key'] for s in manifest['sounds']}
    assert referenced == keys, f'Unconfigured assets: {keys-referenced}'
    assert len({s['sha256'] for s in manifest['sounds']}) == len(keys)
    meta_guids = set()
    for entry in manifest['sounds']:
        path = ROOT/'Assets/Res/Audio/SFX'/entry['audio_path']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256']
        with wave.open(str(path), 'rb') as wav:
            assert (wav.getnchannels(),wav.getframerate(),wav.getsampwidth()) == (1,48000,2)
            pcm = np.frombuffer(wav.readframes(wav.getnframes()),dtype='<i2').astype(float)/32768
        assert 0.05 < len(pcm)/48000 < 2.5
        assert .005 < np.max(np.abs(pcm)) < .5 and np.isfinite(pcm).all()
        assert pcm[0] == pcm[-1] == 0
        meta = path.with_suffix('.wav.meta').read_text(encoding='utf-8')
        assert 'compressionFormat: 0' in meta and 'forceToMono: 1' in meta and 'normalize: 0' in meta
        guid = re.search(r'^guid: ([a-f0-9]{32})$', meta, re.M).group(1)
        assert guid not in meta_guids
        meta_guids.add(guid)
    for name, digest in manifest['source_sha256'].items():
        assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest() == digest, name
    result = dict(skills=71, player_skills=19, unit_skills=52, wavs=len(keys),
                  sound_nodes=len(all_nodes), gameplay_unchanged=True,
                  cast_start='one existing timer-start branch; standalone/server only',
                  placeholders_release_only=list(range(8,19)), all_assets_referenced=True,
                  source_assets_unchanged=True, pcm_checks_passed=True,
                  listening_test='Not performed; in-game balance requires audition',
                  network='Uses unchanged SpawnSoundEffect server presentation events; live multiplayer not tested')
    import_log = OUT/'unity-import.log'
    if import_log.exists():
        log = import_log.read_text(encoding='utf-8', errors='replace')
        assert 'UI_SFX_IMPORT_VALIDATED_41' in log and 'Application will terminate with return code 0' in log
        assert len(re.findall(r'UI_SFX_OK Assets/Res/Audio/SFX/Combat/', log)) == 41
        result['unity_import_and_decode_validated'] = 41
        result['pcm_import_no_normalization'] = True
    (OUT/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
