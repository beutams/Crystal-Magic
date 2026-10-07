"""Prepare an append-only patch from Unity-validated animation tracks."""
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/elemental-skills-v1'
result=ET.parse(OUT/'unity-tests.xml').getroot()
assert result.attrib['result']=='Passed' and result.attrib['passed']=='4'
path=ROOT/'Assets/Res/Data/UnitAnimationFrameLibrary.asset'
export=ROOT/'output/Diagnostics/house-frame-validation/UnityProject/Assets/Res/ElementalValidationFrames.asset'
original=path.read_text(encoding='utf-8-sig')
generated=export.read_text(encoding='utf-8-sig')
tracks=generated[generated.index('  - ClipPath:'):]
expected={p['clip'] for p in json.loads((OUT/'import-manifest.json').read_text())['prefabs']}
actual=set(re.findall(r'^  - ClipPath: (.+)$',tracks,re.M))
assert actual==expected and len(actual)==52
existing=set(re.findall(r'^  - ClipPath: (.+)$',original,re.M))
assert not actual & existing, 'Tracks already present; do not append duplicates'
snapshot=OUT/'before/UnitAnimationFrameLibrary.asset'
if not snapshot.exists(): shutil.copy2(path,snapshot)
# The whole unique last track is context, to prevent repeated duration/frame
# lines elsewhere in this asset from accidentally matching the append patch.
context=original[original.rindex('  - ClipPath:'):]
patch='*** Begin Patch\n*** Update File: Assets/Res/Data/UnitAnimationFrameLibrary.asset\n@@\n'
patch+=''.join(' '+line+'\n' for line in context.splitlines())
patch+=''.join('+'+line+'\n' for line in tracks.splitlines())
patch+='*** End of File\n*** End Patch\n'
(OUT/'frames.patch').write_text(patch,encoding='utf-8')
print(f'Prepared 52 tracks, preserving {len(existing)} existing tracks and their indices.')
