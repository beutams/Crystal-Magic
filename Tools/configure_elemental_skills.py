"""Prepare a reviewable patch for the water/lightning prototype; never overwrite live tables."""
import copy
import difflib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/elemental-skills-v1'
sys.path.insert(0, str(ROOT / 'Tools/Audio'))
from configure_combat_audio import index_json


def effect(kind, **values):
    return {'$type': f'CrystalMagic.Game.Data.Effects.{kind}EffectData, Assembly-CSharp',
            **values, 'Conditions': []}


def getter(key, inputs=None):
    return {'Kind': 1, 'SourceTarget': 0, 'GetterKey': key, 'Inputs': inputs or []}


ENEMY = {'ConditionType': 0, 'CompareType': 'IsTrue',
         'Inputs': [getter('unit.faction.isEnemyTo', [getter('effect.context.originEntity')])]}
WET = {'ConditionType': 0, 'CompareType': 'IsTrue',
       'Inputs': [getter('unit.buffs.has', [{'Kind': 0, 'Literal': {'Type': 2, 'Int': 2}}])]}
ZERO = {'x': 0.0, 'y': 0.0, 'z': 0.0}


def damage(coef, element, flat=0):
    return effect('Damage', TargetSource=0, ValueSource=0, DamageCoefficient=coef,
                  FlatDamageBonus=flat, Element=element)


def buff(id=2, duration=8, stacks=1):
    return effect('ApplyBuff', BuffId=id, TargetSource=0, DurationSeconds=duration,
                  StackCount=stacks, OnlyOncePerPersistentEffect=False)


def remove_wet(all_stacks=False):
    return effect('RemoveBuff', BuffId=2, RemoveAllStacks=all_stacks, RemoveStackCount=1)


def vfx(name, duration, scale=1):
    return effect('SpawnVfx', VfxPrefabName=name, Duration=duration, Scale=scale,
                  SpawnOffset=ZERO, AlignToCasterForward=False)


def follow(name, duration, scale=1, caster=False):
    return effect('SpawnFollowVfx', VfxPrefabName=name, FollowTarget=0 if caster else 1,
                  Duration=duration, Scale=scale, SpawnOffset=ZERO, AlignToTargetForward=False)


def area(radius, children, filters=None):
    return effect('AreaSearch', Radius=radius, CenterOffset=ZERO,
                  TargetConditions=[ENEMY] if filters is None else filters,
                  OnlyNearestTarget=False, OnAfterSearch=children)


def persistent(duration, tick, start, ticks, end=None):
    # None: duration must match the independently spawned field visual even with element bonuses.
    return effect('Persistent', Element=0, TotalDuration=duration, TickIntervalSeconds=tick,
                  PlacementCount=1, PlacementRadius=0, ValidatePlacementPosition=False,
                  PlacementClearanceRadius=0, PlacementValidationAttempts=32,
                  OnStartEffects=start, OnTickEffects=ticks, OnEndEffects=end or [])


def projectile(name, speed, range, radius, hit, end=None, pierce=False, repeat=0, scale=1):
    return effect('SpawnProjectile', ProjectilePrefabName='Projectile', VisualPrefabName=name,
                  VisualScale=scale, VisualOffset=ZERO, Speed=speed, MaxRange=range,
                  SpawnOffsetDistance=.5, HitRadius=radius, CanPierce=pierce,
                  RepeatHitIntervalSeconds=repeat, CollisionTargetConditions=[ENEMY],
                  TriggerDestroyEffectsOnMaxRange=True, OnCollisionEffects=hit,
                  OnDestoryEffects=end or [])


def read_wet(children, modifiers=None):
    return effect('ReadBuffStack', BuffId=2, PerStackModifiers=modifiers or [], OnAfterRead=children)


def mod(channel, bonus=0, factor=0):
    return {'Channel': channel, 'Factor': factor, 'Bonus': bonus}


def conditional(obj, condition=WET):
    obj['Conditions'] = [copy.deepcopy(condition)]
    return obj


def build():
    splash = vfx('Water_Impact_Water', 2/3, 1.25)
    pool_tick = area(2.5, [buff(11, 1.2), buff()])
    rain_tick = area(3, [buff()], filters=[])  # Rain wets allies as well, per the description.
    knock = effect('Knockback', Force=5, DurationSeconds=.2)
    freeze_read = read_wet([
        buff(3, .5), effect('Stun', DurationSeconds=.5),
        follow('Water_Bubble_Sphere', .5, .8),
        damage(0, 1), remove_wet(True),
    ], [mod(101, 12), mod(104, .3), mod(402, .3), mod(400, .3)])
    chain = effect('ChainSearch', Radius=4, MaxJumps=3, TargetConditions=[ENEMY, WET],
                   OnAfterSearch=[vfx('Lightning_Bolt_Y', .4, .8), remove_wet(True), damage(1.5, 3)])
    field_tick = area(3, [
        conditional(effect('Stun', DurationSeconds=.6)),
        conditional(follow('Lightning_Status_Paralyzed_Y', .5, .75)),
        conditional(remove_wet()), damage(.35, 3),
        vfx('Lightning_Impact_1_Y', .3, .65),
    ])
    dry = {**WET, 'CompareType': 'IsFalse'}
    orb_hit = [
        conditional(damage(.7, 3)), conditional(damage(.35, 3), dry),
        conditional(remove_wet()), vfx('Lightning_Impact_2_Y', .25, .65),
    ]
    mechanics = {
        8: [projectile('Water_Projectile', 11, 16, .65, [],
                       [splash, area(1.5, [buff(), damage(1, 1)])])],
        9: [persistent(5, 1, [vfx('Water_Puddle', 5, 1.6), pool_tick], [pool_tick],
                       [vfx('Water_Puddle_End', .25, 1.6)])],
        10: [projectile('Water_Bubble_Projectile_2', 3, 12, .8,
                        [buff(), knock, damage(.7, 1), vfx('Water_Bubble_Pop', .25, .8)],
                        [vfx('Water_Bubble_Projectile_2_End', .25)], pierce=True)],
        11: [effect('RestoreMana', ManaRestoreCoefficient=0, FlatManaRestoreBonus=100),
             follow('Water_Heal', 4/3, 1, caster=True)],
        12: [effect('SpawnLineVfx', VfxPrefabName='Water_Projectile_Large', Length=8,
                    SegmentSpacing=.8, OriginOffsetDistance=.5, Duration=.3333333,
                    Scale=.8, AlignToLineDirection=True),
             effect('ForwardRectSearch', Length=8, Width=1.5, OriginOffsetDistance=.5,
                    TargetConditions=[ENEMY], OnAfterSearch=[buff(), knock, damage(.9, 1),
                    vfx('Water_Impact_Water_2', 5/12, .7)])],
        13: [persistent(6, 1, [vfx('Water_Rain', 6, 2), rain_tick], [rain_tick])],
        14: [area(4, [freeze_read], filters=[ENEMY, WET])],
        15: [projectile('Lightning_Projectile_3_Y', 24, 18, .6,
                        [vfx('Lightning_Impact_1_Y', .5), chain, damage(1, 3)])],
        16: [persistent(4, .5, [vfx('Lightning_Orb_Y', 4, 2.4), field_tick], [field_tick],
                        [vfx('Lightning_Orb_End_Y', .25, 2.4)])],
        17: [projectile('Lightning_Orb_Y', 2, 12, 1.2, orb_hit,
                        [vfx('Lightning_Orb_End_Y', .25)], pierce=True, repeat=.5)],
        18: [vfx('Lightning_Thunderbolt_Y', 5/6, 1.8),
             area(2.5, [buff(4, 3), follow('Lightning_Status_Paralyzed_Y', .75, .8),
                        damage(2.2, 3)])],
    }
    icon = {6:'fire_06', 8:'water_15', 9:'water_07', 10:'water_16', 11:'water_18',
            12:'water_01', 13:'water_10', 14:'ice_08', 15:'lightning_03',
            16:'lightning_16', 17:'lightning_07', 18:'lightning_09'}
    return mechanics, icon


def patch_values(path, replacements):
    original = (OUT/'before'/path.name).read_text(encoding='utf-8-sig')
    current = path.read_text(encoding='utf-8-sig')
    assert current == original, f'Concurrent changes: {path}'
    spans = index_json(original)
    edits = []
    for pointer, value in replacements.items():
        begin, end = spans[pointer]
        indent = len(original[original.rfind('\n', 0, begin)+1:begin])
        # Values live after a field name: indentation is the containing line's leading spaces.
        line = original[original.rfind('\n', 0, begin)+1:begin]
        indent = len(line)-len(line.lstrip(' '))
        encoded = json.dumps(value, ensure_ascii=False, indent=2)
        encoded = encoded.replace('\n', '\n'+' '*indent)
        edits.append((begin, end, encoded))
    updated = original
    for begin, end, encoded in sorted(edits, reverse=True):
        updated = updated[:begin]+encoded+updated[end:]
    json.loads(updated)
    diff = list(difflib.unified_diff(current.splitlines(), updated.splitlines(), n=4, lineterm=''))
    return f'*** Update File: {path.relative_to(ROOT).as_posix()}\n'+''.join(
        ('@@' if line.startswith('@@') else line)+'\n' for line in diff[2:])


def main():
    skills_path = ROOT/'Assets/Res/Data/SkillDataTable.json'
    buffs_path = ROOT/'Assets/Res/Data/BuffDataTable.json'
    skills = json.loads(skills_path.read_text(encoding='utf-8-sig'))['Rows']
    buffs = json.loads(buffs_path.read_text(encoding='utf-8-sig'))['Rows']
    mechanics, icons = build()
    changes = {}
    for index, row in enumerate(skills):
        id = row['Id']
        if id in icons:
            family, number = icons[id].split('_')
            icon = f'Assets/Res/Sprites/Skill/{family}_skill_{number}.png'
            assert (ROOT/icon).is_file()
            changes['Rows',index,'IconPath'] = icon
        if id not in mechanics:
            continue
        assert len(row['EffectChain']) == 1 and 'SpawnSound' in row['EffectChain'][0]['$type']
        changes['Rows',index,'EffectChain'] = mechanics[id]+row['EffectChain']
        changes['Rows',index,'InputType'] = 1 if id in (11,14) else 2
        if id == 11:
            changes['Rows',index,'MpCost'] = 0
    buff_changes = {}
    for index,row in enumerate(buffs):
        if row['Id'] == 2:
            buff_changes['Rows',index,'CanStack'] = True
            buff_changes['Rows',index,'MaxStacks'] = 5
        if row['Id'] == 4:
            buff_changes['Rows',index,'PropertyModifiers'] = [mod(0,factor=-.3), mod(9,factor=-.25)]
    patch = '*** Begin Patch\n'+patch_values(skills_path, changes)+patch_values(buffs_path,buff_changes)+'*** End Patch\n'
    (OUT/'configure.patch').write_text(patch,encoding='utf-8')
    print(f'Prepared {len(changes)} skill field changes and {len(buff_changes)} buff field changes.')


if __name__ == '__main__':
    main()
