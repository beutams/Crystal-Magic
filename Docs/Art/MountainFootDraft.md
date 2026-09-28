# 山脚贴图试稿（Grass1）

当前只试做山脚，不接入地牢生成器或替换现有 RuleTile。

- 图集：`Assets/Res/Sprites/Dungeon/MountainFootDraft/MountainFoot_Grass1_16x16.png`。
- 图集 64×48，4 列 × 3 行；每块 16×16，共 12 块。
- Unity 导入配置：Sprite Multiple，PPU 16，Point，关闭 mipmap 和纹理压缩；已写入 12 个切片。
- 草地底色：现有 Grass1 / g1_5 的 #A0AE44。Grass2、Grass3 需要分别适配草地颜色。
- 山体颜色来自现有 tiles_dungeon_320、336、343：#8C7562、#694A48、#3D3334、#4D403F、#755E54、#856A56、#26233A。
- 直边：外部 4 像素草地，内部 12 像素岩石。凸角为两条外部草地带的并集，凹角为 4×4 草地缺口。
- 同类接缝使用一致的像素模板：草地 #A0AE44、岩石接口 #8C7562、接触线 #4D403F。
- 岩石接口是后续侧壁制作的颜色约定；并未保证直接拼接现有未修改山壁的任意边。
- 无透明区域：本次是匹配 Grass1 的实色试稿。

## 图集顺序（从左至右、从上至下）

| 行 | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| 直边 | N | E | S | W |
| 凸角 | Outer_NW | Outer_NE | Outer_SE | Outer_SW |
| 凹角 | Inner_NW | Inner_NE | Inner_SE | Inner_SW |

方向表示草地所在的一侧/角落。

## 验证范围

矩形与 L 形拼接样图的所有横向、纵向邻接边逐像素校验，共 7552 对，RGB 完全相同。内部岩石使用原图参考填充，仅用于边界测试，不代表新山顶。12 个基础块尚不覆盖所有随机地图形态（例如孤立格和多个凹角组合）。Unity 导入设置已保存，尚未在运行中的地牢里验证。

## 生成记录

使用内置 ImageGen，参考项目原有草地与岩石。生成后进行最近邻采样、原图调色板量化及接缝约束整理。

### 首次提示词

Use case: stylized-concept
Asset type: one production pixel-art mountain-foot tilesheet for a Unity tilemap, one coherent atlas, NOT a presentation.
Reference image 1: first three swatches are actual game grass colors. USE ONLY THE FIRST olive grass swatch (#A0AE44). Last three swatches are the exact existing game's brown rock shapes, scale, palette and shadow style; match these closely.
Primary request: create a mountain-foot transition atlas, consisting of exactly 12 square tile cells in a precise 4-column by 3-row grid. The entire output is the atlas. No gaps, labels, dividers, margins or extra panels.
Logical resolution 64x48 pixels (12 tiles each 16x16), displayed as a perfectly nearest-neighbor enlarged image at 1024x768. Coarse authentic 16x16 pixel art. Sparse broad irregular brown stone faces and chunky dark cracks like the supplied rocks. No fine grain or small decorative stones.
Exact row-major tile arrangement:
Row 1: NORTH straight edge, EAST straight edge, SOUTH straight edge, WEST straight edge.
NORTH: top 4 logical pixel rows grass, bottom 12 rock. EAST: right 4 columns grass, left 12 rock. SOUTH: bottom 4 rows grass, top 12 rock. WEST: left 4 columns grass, right 12 rock.
Row 2: convex outer corners NW, NE, SE, SW. The indicated two outside edges have a 4-pixel-wide grass band; all remaining area rock. Example NW: grass in top four rows OR left four columns, rock in lower-right twelve-by-twelve area. Other convex corners analogously.
Row 3: concave inner corners NW, NE, SE, SW. Only the indicated 4x4 outer corner is grass; remaining tile is rock. Example NW: grass only where top four rows AND left four columns overlap, rock elsewhere.
Edges and seams are critical: grass is EXACTLY flat solid #A0AE44, no speckles, tufts, soil or shading anywhere in grass. Adjacent tile cuts through rock must have an identical calm rock brown #8C7562 edge baseline; keep dark cracks mainly inside rock faces, not randomly across cell edges. Grass-rock transition must use a narrow dark brown pixel seam, not white highlights.
Rock palette ONLY #8C7562 #694A48 #3D3334 #4D403F #755E54 #856A56 #26233A. All tiles share identical RGB values. Rock-free boundary portions preserve flat grass. Do not introduce a summit plateau or another material.
Opaque rectangular atlas. No text, no numbers, no borders, no perspective/isometric diamonds, no 3D rendering, no lighting gradients, no blur, no antialiasing, no white outlines.

### 岩块尺度调整提示词

Edit target image 1: the generated 4 by 3 mountain-foot tile atlas. Reference image 2: existing game color and rock reference (three grass swatches then three 16x16 rocky tiles).
Change ONLY the rock texture scale and detail density in image 1. Preserve its 4x3 atlas cell arrangement, grass-side orientations and convex/concave corner meanings exactly.
The output will be reduced to 16x16 pixels per cell. Each tile must contain ONE or at most TWO broad chunky rock faces, each roughly 8 to 12 logical pixels wide, like the existing rock swatches. Remove all little pebbles, small cracks, speckles and granularity. Prefer large quiet solid color patches with a few one-logical-pixel-wide dark stepped crevices. Visibly much simpler, larger rock faces than the current target. No smooth rendering, gradients, grain or tiny details. Actual old-school 16x16 pixel art enlarged with nearest-neighbor.
Grass stays uniform #A0AE44; rock uses only #8C7562 #694A48 #3D3334 #4D403F #755E54 #856A56 #26233A.
Keep matching tile-edge colors, no outlines around separate grid cells, no labels, no spacing, no new materials, no scene, no summit. Opaque edge-to-edge atlas with exact 4:3 aspect ratio.

