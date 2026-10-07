"""Contact sheet made exclusively from the existing project icon/sprite pixels."""
import json
from pathlib import Path
import re
from PIL import Image, ImageDraw, ImageFont
from configure_elemental_skills import build

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/elemental-skills-v1'
manifest=json.loads((OUT/'import-manifest.json').read_text(encoding='utf-8'))
skills=json.loads((ROOT/'Assets/Res/Data/SkillDataTable.json').read_text(encoding='utf-8-sig'))['Rows']
prefabs={p['name']:p for p in manifest['prefabs']}
assets={a['guid']:a for a in manifest['assets']}
names=['Water_Projectile','Water_Puddle','Water_Bubble_Projectile_2','Water_Heal',
       'Water_Projectile_Large','Water_Rain','Water_Bubble_Sphere','Lightning_Projectile_3_Y',
       'Lightning_Orb_Y','Lightning_Orb_Y','Lightning_Thunderbolt_Y','Earth_Spike']
canvas=Image.new('RGB',(1056,784),(26,31,39)); draw=ImageDraw.Draw(canvas)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
small=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',13)
draw.text((22,12),'水 / 雷技能素材配置预览（全部为现有像素素材）',font=font,fill='#e4dcc1')
for index,name in enumerate(names):
    cx=16+(index%3)*348; cy=54+(index//3)*180
    draw.rounded_rectangle((cx,cy,cx+332,cy+165),radius=8,fill=(40,47,58))
    p=prefabs[name]; clip=(ROOT/p['clip']).read_text()
    texture_guid=re.search(r'value: \{fileID: -?\d+, guid: ([a-f0-9]+)',clip)[1]
    a=assets[texture_guid]; sheet=Image.open(ROOT/a['path']).convert('RGBA')
    meta=(ROOT/(a['path']+'.meta')).read_text()
    rects=re.findall(r'      rect:\s+serializedVersion: \d+\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)',meta)
    x,y,w,h=map(lambda v:int(float(v)),rects[len(rects)//2])
    frame=sheet.crop((x,sheet.height-y-h,x+w,sheet.height-y)); box=frame.getbbox()
    if box: frame=frame.crop(box)
    scale=max(1,min(3,int(100/max(frame.size))))
    frame=frame.resize((frame.width*scale,frame.height*scale),Image.Resampling.NEAREST)
    canvas.paste(frame,(cx+200-frame.width//2,cy+73-frame.height//2),frame)
    if index<11:
        row=next(s for s in skills if s['Id']==index+8)
        icon=Image.open(ROOT/row['IconPath']).convert('RGBA').resize((64,64),Image.Resampling.NEAREST)
        canvas.paste(icon,(cx+14,cy+42),icon)
        draw.text((cx+12,cy+9),f"{row['Id']}  {row['NameKey']}",font=font,fill='#f0e8d0')
        draw.text((cx+12,cy+117),Path(row['IconPath']).name,font=small,fill='#abb4bf')
    else:
        draw.text((cx+12,cy+9),'地系：已导入待配置',font=font,fill='#f0e8d0')
    draw.text((cx+12,cy+140),name,font=small,fill='#9dd2df')
canvas.save(OUT/'configured-skills-preview.png')
print(OUT/'configured-skills-preview.png')
