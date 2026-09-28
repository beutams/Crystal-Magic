# 山体资源：单草色、山腰判定与山顶接壤

> 历史记录，以下不是当前启用版本。现已换为用户确认的简洁 v1 样条及三态邻居规则，见 [当前山体规则](../MountainRuleTiles-v1/README.md)。本目录旧重建/验证入口已停用，避免覆盖新图集。

## 当前规则

- 山脚/山侧的草色固定为 `#89A043`，只启用一套资源。
- 暴露正面的投影最高格仍是山腰，不是山顶；最低格为山脚，中间为连续山腰。最低高度 1 格也至少保留一格山腰。
- 非暴露正面的内部位置才使用山顶。
- 山顶只有挨着山腰/山脚的边、角才做岩面过渡；旁边是山顶、草地、Void 或空白时，不产生草地过渡。完全没挨着岩面的山顶为纯色 `#B3995C`。
- 山体投影先画后方、再画前方。同格发生重叠时，前方山腰覆盖后方低山顶，而不是被它替换。

## 启用资源

`Assets/Res/Tile/Mountain/` 下：

- `MountainFoot_Grass1.asset`：47 张山脚切片。
- `MountainWall_Grass1.asset`：47 张山腰/侧面切片。
- `MountainTop_Grass1.asset`：47 张山顶切片，包含纯色内部和接壤边角。

总计 141 张 16×16 切片，图集均为 128×96；PPU 16、Point、无 mipmap/压缩。Grass1 只是稳定资源路径，Prairie 的三个 Ground Style 共用这套贴图。旧 Grass2/3 版本留在 `RetiredMultiGrass/`，不参与 Unity 导入。

`MountainRuleTile.Part` 区分 Foot / Wall / Summit。普通邻居 1/2 判断山体 Family；山顶专用邻居 3/4 分别判断相同 Family 的岩面（Foot/Wall）/非岩面。山顶掩码的 1 表示“不是岩面”，所以空白和草地不会误触发过渡；对角接触也有独立角图。

原始三张美术源图在 `../MountainCliff-v2/`。此次修改已有像素重建规则，保留岩色和现有山脚/山壁造型，未重新生成画风。共用的岩面接缝从相同像素采样，山顶不引入绿色边缘。

## 遮挡

继续保留上一轮局部山脚排序修复：竖向连续段按自身山脚高度排序，同山体同山脚高度合批，不把远处最低点用于整座山。规则在完整 Tilemap 上解析后再分组。角色恰好处在山脚线时优先显示。

## 验证和预览

```powershell
& 'Docs/Art/MountainComplete16/Build-Pixels.ps1'
& 'Docs/Art/MountainComplete16/Verify-Assets.ps1'
# 需要先运行托管测试，导出 Temp/MountainFootValidation/layout-preview.json
& 'Docs/Art/MountainComplete16/Preview-Layout.ps1'
```

已验证：768 个邻域组合、141 个 Sprite 引用、12 处共用配置路径、116,416 项山脚/山腰/山顶接缝像素检查。山顶纯色内部无绿色像素；所有接壤类型均存在对应岩面边角。

运行时与编辑器程序集编译通过。7 项纯数据测试覆盖：单列高度 1～4、连片平台的正面/内部分类、前高后低与前低后高的覆盖顺序、配色回退、不跨 Void 的配色扩散、局部山脚角色前后遮挡、竖列空隙与合批。

`Assets/Tests/Editor/MountainFootRuleTileTests.cs` 还提供 Unity 内的实际 Sprite 导入、Family 连接及 1,536 个山顶岩面/非岩面组合测试。
本轮 Unity 没有触发新的脚本自动导入，因此上述 AssetDatabase 测试未执行；7 项纯数据测试使用 Unity 附带 Mono 执行。尚未在运行中场景做走位验收。已生成的场景保存了解析后的 Tile Sprite，需要重新生成/进入地牢才能观察新布局和新边角规则。

`Runtime-Layout-Regression.png` 由真正的布局函数输出与当前 PNG/规则生成：左为低平台，中为高平台，右为凹角与高低台阶。它是离线布局验证，不是运行中游戏截图。
`Mountain-Grass1-assembled.png` 为另一个切片示意图。Grass2/3 预览只作历史留档。
