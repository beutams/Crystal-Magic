"""Validate configured references and unchanged unrelated rows; stage pixel assets for Unity validation."""
import copy
import json
from pathlib import Path
import re
import shutil
import sys
import uuid
import xml.etree.ElementTree as ET
from configure_elemental_skills import build

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'output/elemental-skills-v1'
TEST = ROOT/'output/Diagnostics/house-frame-validation/UnityProject'


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values(): yield from walk(child)
    elif isinstance(value, list):
        for child in value: yield from walk(child)


def main():
    skill_path = ROOT/'Assets/Res/Data/SkillDataTable.json'
    buff_path = ROOT/'Assets/Res/Data/BuffDataTable.json'
    skills = json.loads(skill_path.read_text(encoding='utf-8-sig'))['Rows']
    baseline = json.loads((OUT/'before/SkillDataTable.json').read_text(encoding='utf-8-sig'))['Rows']
    buffs = json.loads(buff_path.read_text(encoding='utf-8-sig'))['Rows']
    old_buffs = json.loads((OUT/'before/BuffDataTable.json').read_text(encoding='utf-8-sig'))['Rows']
    mechanics, icons = build()
    assert len(skills)==len(baseline)==71
    for row,before in zip(skills,baseline):
        id=row['Id']
        assert id==before['Id']
        expected=copy.deepcopy(before)
        if id in icons:
            family,number=icons[id].split('_')
            expected['IconPath']=f'Assets/Res/Sprites/Skill/{family}_skill_{number}.png'
        if id in mechanics:
            expected['EffectChain']=mechanics[id]+before['EffectChain']
            expected['InputType']=1 if id in (11,14) else 2
            if id==11: expected['MpCost']=0
        assert row==expected, f'Unexpected skill changes: {id}'
        if id<=18:
            assert (ROOT/row['IconPath']).is_file(), row['IconPath']
        for obj in walk(row):
            for field,folder,suffix in [('VfxPrefabName','Prefab/VFX','.prefab'),
                                        ('VisualPrefabName','Prefab/VFX','.prefab'),
                                        ('ProjectilePrefabName','Prefab/Projectile','.prefab'),
                                        ('AudioPath','Audio/SFX','')]:
                if obj.get(field):
                    path=ROOT/'Assets/Res'/folder/(obj[field]+suffix)
                    assert path.is_file(), str(path)
            if '$type' in obj:
                assert not obj['$type'].endswith('EffectData'), 'Assembly suffix missing'
            if 'BuffId' in obj:
                assert any(b['Id']==obj['BuffId'] for b in buffs)
    assert len(buffs)==len(old_buffs)
    for buff,before in zip(buffs,old_buffs):
        expected=copy.deepcopy(before)
        if buff['Id']==2: expected.update(CanStack=True,MaxStacks=5)
        if buff['Id']==4:
            expected['PropertyModifiers']=[{'Channel':0,'Factor':-.3,'Bonus':0},
                                           {'Channel':9,'Factor':-.25,'Bonus':0}]
        assert buff==expected
    manifest=json.loads((OUT/'import-manifest.json').read_text(encoding='utf-8'))
    assert len(manifest['assets'])==156 and len(manifest['prefabs'])==52
    guid_paths={}
    for meta in (ROOT/'Assets').rglob('*.meta'):
        match=re.search(r'^guid: (\w+)$',meta.read_text(encoding='utf-8-sig'),re.M)
        if match: guid_paths.setdefault(match[1],[]).append(meta)
    for obj in manifest['assets']+manifest['prefabs']:
        path=ROOT/obj['path']
        assert path.is_file()
        meta=Path(str(path)+'.meta')
        guid=re.search(r'^guid: (\w+)$',meta.read_text(encoding='utf-8-sig'),re.M)[1]
        assert len(guid_paths[guid])==1, guid_paths[guid]
    report={'skills_configured':11,'player_icons_valid':19,'unrelated_unit_skills_preserved':52,
            'new_vfx_prefabs':52,'new_png_sheets':52,'new_animation_clips':52,
            'wet_max_stacks':5,'repeat_hit_interval_seconds':.5,
            'gameplay_tested_in_play_mode':False}
    tests=OUT/'unity-tests.xml'
    if tests.exists():
        result=ET.parse(tests).getroot()
        assert result.attrib['result']=='Passed' and result.attrib['passed']=='4'
        report['unity_editmode_tests_passed']=4
    frame_snapshot=OUT/'before/UnitAnimationFrameLibrary.asset'
    if frame_snapshot.exists():
        frames=(ROOT/'Assets/Res/Data/UnitAnimationFrameLibrary.asset').read_text(encoding='utf-8-sig')
        before=frame_snapshot.read_text(encoding='utf-8-sig')
        assert frames.startswith(before), 'Previously saved animation tracks changed'
        paths=re.findall(r'^  - ClipPath: (.+)$',frames,re.M)
        assert len(paths)==len(set(paths))==400
        assert {p['clip'] for p in manifest['prefabs']} <= set(paths)
        report['runtime_animation_tracks']=400
        report['original_animation_tracks_preserved']=348
    (OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))
    if '--stage-unity' not in sys.argv: return
    # Isolated import harness: no vendor scripts, scenes or project settings.
    for obj in manifest['assets']+manifest['prefabs']:
        src=ROOT/obj['path']; dest=TEST/obj['path']
        dest.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(src,dest); shutil.copy2(str(src)+'.meta',str(dest)+'.meta')
    for file in ['Assets/Scripts/Game/Component/SpriteEffectAnimationAuthoring.cs',
                 'Assets/Scripts/Game/Data/UnitAnimationFrameLibrary.cs']:
        src=ROOT/file; dest=TEST/'Assets/Runtime'/src.name
        shutil.copy2(src,dest); shutil.copy2(str(src)+'.meta',str(dest)+'.meta')
    # This prefix is copied verbatim: the actual production cooldown implementation,
    # without unrelated projectile payload structs and their game-only dependencies.
    runtime=(ROOT/'Assets/Scripts/Game/Component/SkillProjectileRuntime.cs').read_text(encoding='utf-8-sig')
    (TEST/'Assets/Runtime/SkillProjectileHitHistory.cs').write_text(
        runtime.split('public struct SkillProjectileConditionInstructionElement')[0],encoding='utf-8')
    for file in ['Assets/Tests/Editor/SkillProjectileRepeatHitTests.cs',
                 'Assets/Tests/Editor/ElementalVfxImportTests.cs']:
        src=ROOT/file; dest=TEST/'Assets/Editor'/src.name
        shutil.copy2(src,dest)
        meta=Path(str(src)+'.meta')
        if not meta.exists():
            meta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n',encoding='utf-8')
        shutil.copy2(meta,str(dest)+'.meta')
    print('Staged isolated Unity import and hit-history tests.')


if __name__=='__main__': main()
