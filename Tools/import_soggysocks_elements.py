"""Import only sprite/animation/controller assets from the user's local packages.

Build flat, uniquely named VFX prefabs using the project's existing Fireball
template and SpriteEffectAnimationAuthoring. No vendor scenes/scripts/settings
are imported. Existing files and GUIDs are never overwritten on a mismatch.
"""
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import tarfile
import uuid

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'output/elemental-skills-v1'
NS = uuid.UUID('4df39cd2-e704-4869-86aa-2d8b1da9223c')


def guid(text):
    return re.search(r'^guid: ([a-f0-9]{32})$', text, re.M).group(1)


def put(relative, data):
    path = ROOT/relative
    if isinstance(data, str): data = data.encode('utf-8')
    if path.exists():
        if path.read_bytes() != data: raise ValueError(f'Existing asset differs: {path}')
        return
    folders = []
    parent = path.parent
    while not parent.exists():
        folders.append(parent)
        parent = parent.parent
    for folder in reversed(folders):
        folder.mkdir()
        folder_meta = folder.with_name(folder.name+'.meta')
        if not folder_meta.exists():
            stable = uuid.uuid5(NS, folder.relative_to(ROOT).as_posix()).hex
            folder_meta.write_text(f'fileFormatVersion: 2\nguid: {stable}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    path.write_bytes(data)


def package_records(path):
    result = []
    with tarfile.open(path, 'r:gz') as archive:
        members = {m.name:m for m in archive.getmembers()}
        for member in members.values():
            if not member.name.endswith('/pathname') or not member.isfile(): continue
            source = archive.extractfile(member).read().decode('utf-8').strip()
            if source in ('Assets/SoggySocksAssets','Assets/SoggySocksAssets/VFX'): continue
            if '..' in PurePosixPath(source).parts or not source.startswith('Assets/SoggySocksAssets/VFX/'):
                raise ValueError(f'Unexpected vendor asset path: {source}')
            if Path(source).suffix.lower() not in ('.png','.anim','.controller','.prefab'): continue
            base = member.name.rsplit('/',1)[0]
            for name in (base+'/asset',base+'/asset.meta'):
                assert members[name].isfile(), 'Links/devices are not importable assets'
            result.append(dict(source=source, data=archive.extractfile(base+'/asset').read(),
                               meta=archive.extractfile(base+'/asset.meta').read().decode('utf-8')))
    return result


def main():
    template = (ROOT/'Assets/Res/Prefab/VFX/Fireball.prefab').read_text(encoding='utf-8')
    ppu = re.search(r'spritePixelsToUnits: ([\d.]+)', (ROOT/'Assets/Res/Sprites/VFX/proj_fireball_sheet.png.meta').read_text()).group(1)
    existing = {}
    for meta in (ROOT/'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(encoding='utf-8',errors='replace'),re.M)
        if match: existing[match.group(1)] = meta.with_suffix('').relative_to(ROOT).as_posix()
    all_assets, prefabs, textures = [], [], []
    for family in ('Water','Lightning','Earth'):
        packages = list((OUT/'packages').rglob(f'soggysocks_{family.lower()}*.unitypackage'))
        assert len(packages) == 1
        records = package_records(packages[0])
        marker = f'/{family}_VFX/'
        records = [r for r in records if marker in r['source']]
        by_guid = {guid(r['meta']):r for r in records}
        for record in records:
            relative = record['source'].split(marker,1)[1]
            suffix = Path(relative).suffix
            if suffix == '.prefab': continue
            destination = (f'Assets/Res/Sprites/VFX/{family}/' if suffix == '.png'
                           else f'Assets/Res/Animation/Effects/{family}/') + relative
            record['destination'] = destination
            asset_guid = guid(record['meta'])
            if asset_guid in existing and existing[asset_guid] != destination:
                raise ValueError(f'GUID collision {asset_guid}: {existing[asset_guid]}')
            meta = record['meta']
            if suffix == '.png':
                for pattern, replacement in [
                    (r'(spritePixelsToUnits:) [\d.]+',rf'\g<1> {ppu}'),
                    (r'(filterMode:) \d+',r'\1 0'), (r'(enableMipMap:) \d+',r'\1 0'),
                    (r'(textureCompression:) \d+',r'\1 0'),
                    (r'(alphaIsTransparency:) \d+',r'\1 1')]:
                    meta = re.sub(pattern,replacement,meta)
                # The 2100px rain strip exceeds Unity's default 2048 cap. Avoid
                # resampling pixel art and the resulting non-integer sprite PPU.
                size = max(Image.open(io.BytesIO(record['data'])).size)
                minimum_limit = 1 << (size - 1).bit_length()
                meta = re.sub(r'(maxTextureSize:) (\d+)',
                              lambda m: f'{m[1]} {max(int(m[2]), minimum_limit)}', meta)
                textures.append(record)
            put(destination+'.meta',meta)
            put(destination,record['data'])
            all_assets.append(dict(source=record['source'],path=destination,guid=asset_guid,
                                   sha256=hashlib.sha256(record['data']).hexdigest()))
        for record in records:
            if not record['source'].endswith('.prefab'): continue
            vendor = record['data'].decode('utf-8')
            controller_guid = re.search(r'm_Controller: \{fileID: \d+, guid: ([a-f0-9]+)',vendor).group(1)
            controller = by_guid[controller_guid]['data'].decode('utf-8')
            motions = re.findall(r'm_Motion: \{fileID: 7400000, guid: ([a-f0-9]+)',controller)
            assert len(set(motions)) == 1, record['source']
            clip_guid = motions[0]
            clip_record = by_guid[clip_guid]
            clip = clip_record['data'].decode('utf-8')
            sprite = re.search(r'm_Sprite: (\{fileID: -?\d+, guid: [a-f0-9]+, type: 3\})', vendor).group(1)
            name = Path(record['source']).stem.replace(' ','_')
            if not name.lower().startswith(family.lower()+'_'): name = family+'_'+name
            prefab_path = f'Assets/Res/Prefab/VFX/{name}.prefab'
            prefab = template.replace('m_Name: Fireball','m_Name: '+name)
            prefab = re.sub(r'm_Sprite: \{[^\n]+\}', 'm_Sprite: '+sprite,prefab)
            prefab = re.sub(r'LoopClip: \{[^\n]+\}',f'LoopClip: {{fileID: 7400000, guid: {clip_guid}, type: 2}}',prefab)
            prefab = re.sub(r'm_Controller: \{[^\n]+\}',f'm_Controller: {{fileID: 9100000, guid: {controller_guid}, type: 2}}',prefab)
            # Ground-plane effects draw below characters. All others use the same
            # material/layer as the existing fire VFX. Animator remains disabled.
            if 'Puddle' in name: prefab = prefab.replace('m_SortingOrder: 0','m_SortingOrder: -5')
            stable = uuid.uuid5(NS,prefab_path).hex
            put(prefab_path+'.meta',f'fileFormatVersion: 2\nguid: {stable}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
            put(prefab_path,prefab)
            duration = float(re.search(r'm_StopTime: ([\d.eE+-]+)',clip).group(1))
            prefabs.append(dict(name=name,path=prefab_path,guid=stable,source=record['source'],
                                clip=clip_record['destination'],clip_guid=clip_guid,duration=duration,
                                family=family))
    assert len({p['name'] for p in prefabs}) == len(prefabs)
    manifest = dict(assets=all_assets,prefabs=prefabs,method='Existing vendor pixels/clips; project fire prefab conventions',
                    sources=[dict(file=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                             for p in (OUT/'packages').rglob('*.unitypackage')])
    (OUT/'import-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    # Contact sheets only arrange existing pixels; no image generation or repainting.
    font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf',13)
    for family in ('Water','Lightning','Earth'):
        images = [t for t in textures if f'/{family}_VFX/' in t['source']]
        canvas = Image.new('RGB',(800,((len(images)+3)//4)*168),(27,34,41))
        draw = ImageDraw.Draw(canvas)
        for index, record in enumerate(images):
            meta = record['meta']
            rect = re.search(r'      rect:\s+serializedVersion: \d+\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)',meta)
            assert rect, record['source']
            x,y,w,h = (int(float(v)) for v in rect.groups())
            sheet = Image.open(io.BytesIO(record['data'])).convert('RGBA')
            # Pick a middle animation frame from the full known sprite metadata.
            rects = list(re.finditer(r'      rect:\s+serializedVersion: \d+\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)',meta))
            x,y,w,h = (int(float(v)) for v in rects[len(rects)//2].groups())
            frame = sheet.crop((x,sheet.height-y-h,x+w,sheet.height-y))
            box = frame.getbbox()
            if box: frame = frame.crop(box)
            scale = min(3,128/max(frame.size))
            frame = frame.resize((max(1,round(frame.width*scale)),max(1,round(frame.height*scale))),Image.Resampling.NEAREST)
            cx,cy = (index%4)*200,(index//4)*168
            canvas.paste(frame,(cx+(200-frame.width)//2,cy+5+(125-frame.height)//2),frame)
            label=Path(record['destination']).stem.replace('_sheet','')
            draw.text((cx+5,cy+135),label[:25],font=font,fill=(230,230,215))
        canvas.save(OUT/f'{family.lower()}-contact.png')
    # All existing skill icons for visual selection.
    canvas = Image.new('RGB',(12*96,8*96),(28,34,42))
    draw = ImageDraw.Draw(canvas)
    for fi,family in enumerate(('water','lightning','ice','fire')):
        for number in range(1,25):
            p=ROOT/f'Assets/Res/Sprites/Skill/{family}_skill_{number:02}.png'
            im=Image.open(p).convert('RGBA').resize((64,64),Image.Resampling.NEAREST)
            x=((number-1)%12)*96;y=(fi*2+(number-1)//12)*96
            canvas.paste(im,(x+16,y),im)
            draw.text((x+2,y+68),f'{family[:4]} {number:02}',font=font,fill='white')
    canvas.save(OUT/'existing-skill-icons.png')
    print(json.dumps(dict(imported_assets=len(all_assets),prefabs=len(prefabs),textures=len(textures),ppu=ppu)))


if __name__ == '__main__': main()
