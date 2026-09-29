# 山地概念图拆分试稿 v1

基于 `../MountainConcept-20260929-v1/Mountain-Concept-v1.png`，使用 imagegen 技能及内置 image_gen 将完整山体重排为连续的正面素材，再进行固定位置切片、最近邻采样和统一色板转换。原始概念图的弯曲轮廓不能直接等格切成通用 RuleTile。

## 交付

- `Mountain-Tiles-16-ContactSheet.png`：48 张 16×16 切片的带间隔预览，整数 6 倍放大。
- `Mountain-Assembled-16-vs-32.png`：16×16 与 32×32 在相同显示尺寸下的拼接对比，两侧均为整数倍放大。
- `Mountain-Horizontal-Repeat-Inspection.png`：完整正面横向重复两次，用于观察重复点。
- `Mountain-Master-128x96.png`：实际 16×16 切片对应的无间隔图集。
- `Native16/`：48 张独立 16×16 PNG，沿用现有游戏的像素规格。
- `Mountain-Master-256x192.png` 与 `Native32/`：相同内容的 32×32 细节对照，不代表已更改游戏规格。
- `Sources/Mountain-Front-Generated.png`：内置生成工具输出的原图。
- `Prompt.md`：完整生成提示词和输入参考。
- `Build.ps1`：可重建切片、图集和预览的转换脚本。
- `verification-16.json`、`verification-32.json`：实际保存文件的尺寸、重构、色板、透明核对结果。

## 分区与摆放

图集为 8 列 × 6 行。A–H 是固定横向顺序的相邻切片，**不是可自由随机互换的变体**。从上到下为：

1. `05-Summit-Back`：山顶背缘，外部透明。
2. `04-Summit`：山顶内部，低对比风化纹理。
3. `03-Summit-Front`：山顶前缘与岩壁转折。
4. `02-Wall-Upper`：上段岩壁。
5. `02-Wall-Lower`：下段岩壁。
6. `01-Foot`：山脚岩面与草地接触。

源图实际为 1448×1086，生成的材质分区未严格遵守提示词中的网格。按可见轮廓测定的纵向切点为 118、230、420、550、727、884、975；每一段归一化成一行，再切 8 列。顶部多余透明区及底部多余草地被裁掉。

岩面造型来自生成图片。转换仅做切片、格心采样、14 色色板量化、二值透明处理，以及把山脚最后 3 个逻辑像素行统一为现有草色 `#89A043`；没有程序随机绘制岩石纹理。32×32 版本对应最后 6 行草色。

## 核对与限制

- 两个尺寸版本各 48 张切片；从磁盘重新加载，按顺序重构与各自图集逐像素一致。
- 所有不透明像素均在同一组 14 色色板内；透明通道只有 0/255，背缘透明被保留。
- 已查看实际原生切片的整数放大图及拼回预览。16×16 保留主要岩面与裂隙，32×32 保留更多细纹。
- 图集最左/最右边缘不完全相同，未做无缝周期修复。JSON 中的边缘不相等数只是检查数据，不等同于每一处都是视觉接缝错误。
- 尚未制作侧边、凸角、凹角或可任意增高的岩壁连接规则，也未验证切片自由随机互换。
- 这是正面拆分试稿，不是完整可安装的 RuleTile 集。游戏资源、主题配置、Unity 导入设置及运行代码均未改动。

## 重建

使用 Windows PowerShell 5.1（脚本内的 System.Drawing 编译引用按 .NET Framework 配置）：

```powershell
& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -File 'Docs\Art\MountainConceptSplit-v1\Build.ps1'
```
