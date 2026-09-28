# 山体正面风格样条 v1

仅供确认山的画风，不导入 Unity，不修改现有 RuleTile、地牢判定、遮挡或主题配置。

## 交付

五张独立的 16×16 山体 PNG：

1. `01-Foot-16x16.png`：正面山脚；上方岩壁、下方约四分之一草地。
2. `02-Wall-16x16.png`：正面山腰，全幅竖直岩面。
3. `03-Summit-Front-16x16.png`：山腰和山顶的前缘转折；上方山顶，下方岩面。
4. `04-Summit-16x16.png`：纯色山顶，`#B3995C`。
5. `05-Summit-Back-16x16.png`：山顶另一侧边缘；轮廓外侧透明，显示场景原有背景，不再画草地。岩色轮廓保留，下方接纯色山顶。

`06-Grass-16x16.png` 沿用当前单草色山体图集里的 `#89A043` 色块：
`Assets/Res/Sprites/Dungeon/MountainFoot16/MountainFoot_Grass1.png`，从图片左上角坐标 `(112,80)` 复制 16×16 原像素。没有修改 Village 草地图集，也没有重新生成草地。

`Mountain-Front-16x96.png` 为六张图无间隔的竖向拼图。从上到下的文件编号为 **05 → 04 → 03 → 02 → 01 → 06**。

`Mountain-Front-preview-8x.png` 为整数 8 倍放大并保留透明通道；`Mountain-Front-labeled-preview.png` 为带名称的整数 6 倍放大。标注、背景、留白、透明示意棋盘格都只存在于带名称预览，不在原尺寸 PNG 中。

`Mountain-Front-native-tiles.zip` 包含五张单图、现有草地、16×96 合图及本说明。

## 生成方式与提示词

使用 **内置 image_gen**，不是 CLI/API fallback。按 imagegen 技能分别生成五张源图，参考用户提供的近直角棕色悬崖截图及现有草色；其他四张同时参考先生成的山腰，以统一岩面风格。

- `Prompts-Final.json` 保存五张图的完整提示词和参考路径；山腰初次生成使用前两张参考，其他四张增加山腰作为第三张参考。
- `Prompts.json` 保留添加山腰参考前的初始提示词。
- `Seam-Edit-Prompt.txt` 保存山腰边缘匹配的局部编辑提示词。
- `Back-Transparency-Edit-Prompt.txt` 保存背缘外侧透明化提示词，使用内置工具并设置 `transparent_background=true`。
- `Sources/` 保存五张生成源图及山腰边缘编辑后的源图。实际转换选用 `02-Wall-SeamMatched.png`；最初山腰源图也保留。

透明化生成结果曾错误地挖掉部分山顶，因此不能整张替换：`Sources/05-Summit-Back-AlphaProposal.png` 仅作为外侧透明遮罩来源。拼接时只在原图绿色区域采用它的透明通道，岩面与山顶全部保留原图像素，防止生成结果修改用户未要求改变的部分。

生成器输出为放大源图，不是原生 16×16。`Assemble.ps1` 对源图进行格心采样、统一色板映射和像素拷贝，得到实际 16×16 PNG，再拼成实际 16×96；没有程序绘制替代岩石纹理。

限定色板：岩色 `#A08043`、山顶/浅岩面 `#B3995C`、岩影 `#805B32`、深裂隙 `#514332`、草地 `#89A043`、接地阴影 `#647B36`。统一色板会去掉生成源图中轻微的颜色漂移和渐变。

## 核对结果

- 六张单图尺寸全部为 16×16，合图为 16×96，RGBA PNG。
- 背缘原先的 53 个绿色像素现在全部透明；其余 203 个山体像素与修改前完全一致。其他五张 tile 不变，无半透明边缘。
- 五条上下接缝共 80 对像素颜色相同。
- 山顶中间一整格全部为 `#B3995C`，无额外纹理。
- 山脚最后三行与现有草地图块全部为 `#89A043`。
- 已查看实际缩小后像素图与整数放大预览。这只是本列风格样条，未宣称完成左右平铺、转角图或游戏场景验收。

## 重建

```powershell
& 'Docs/Art/MountainStyleStrip-v1/Extract-Grass.ps1'
& 'Docs/Art/MountainStyleStrip-v1/Assemble.ps1'
& 'Docs/Art/MountainStyleStrip-v1/Make-Preview.ps1'
```
