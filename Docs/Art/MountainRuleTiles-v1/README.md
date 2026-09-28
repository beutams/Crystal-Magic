# 已确认 v1 山体样条 → 完整 RuleTile

使用 `../MountainStyleStrip-v1/01…06-16x16.png`，没有使用被否定的 v2 裂纹细节版。按原像素扩展山脚、山腰、正面折沿、山顶内部和透明背缘；只为拼接统一边界像素，不添加随机纹理。

## 当前启用

Prairie 主题的主配置和三个 Ground Style 共用以下资产，原路径和资产 GUID 不变：

| RuleTile（`Assets/Res/Tile/Mountain/`） | 16×16 切片 | 图集尺寸 |
| --- | ---: | ---: |
| MountainFoot_Grass1.asset | 383 | 512×192 |
| MountainWall_Grass1.asset | 383 | 512×192 |
| MountainTop_Grass1.asset | 545 | 512×288 |

图集仍在 `Assets/Res/Sprites/Dungeon/MountainFoot16/` 与 `MountainUpper16/`。Point、PPU 16、无压缩/无 mipmap、二值透明。山脚草色固定 `#89A043`；山顶内部为 `#B3995C`；山顶外缘不画草，透明部分直接露出背景。

`MountainRuleTile` 区分外侧、岩壁、山顶三种邻居。每部分八方向共有 3^8 = 6561 种组合，烘焙后去重成上述切片。这样外轮廓与内凹岩壁接壤可以同时判断，而不只判断“是不是同一种 Tile”。山脚与山腰是同族岩壁：向下暴露时使用山脚，向上接山顶时使用折沿，其余为岩壁。

Inspector 中 `m_TilingRules` 的一条规则只注册八方向刷新依赖，实际图案由 `VariantSprites` 和完整的 `VariantLookup` 选择，并非只配了一条绘制规则。不要用普通 RuleTile Inspector 随意改这条依赖规则；调整源像素后统一重建三个资产。

遵循项目组件规范，未改动组件启动/资源生命周期，也未改动已有地牢投影与局部山脚遮挡排序。

## 预览与验证

- `Approved-style-plateau-preview.png`：五种部位拼成的平台。
- `Runtime-layout-preview.png`：现有地牢布局代码导出的低山、高山、凹角和高低交界组合；离线渲染，不是 Unity 运行截图。
- `verification.json`：1,311 个切片、19,683 个邻居组合、335,616 个保存后像素、12 处主题引用检查通过；7,558,272 对公共边缘像素检查通过。山脚透明接缝按约定草底色比较。
- 本轮运行时代码与 Editor 测试代码编译通过；7 项布局/局部山脚排序逻辑测试通过。
- 尚未在 Unity 内执行原生 Tilemap/导入检查或运行中场景验收。等 Unity 导入完后可执行 `Tools > Crystal Magic > Validate Mountain RuleTiles`，其中包含 4 项原生导入/刷新检查和 7 项布局检查；成功结果写入 `Temp/MountainFootValidation/editor-validation.json`。

已经生成的地牢对象持有解析后的 Sprite。切回 Unity、等导入编译完成，再重新进入或重新生成地牢，才会替换场景里的旧山体。

## 重建

从项目根目录使用 PowerShell 7：

```powershell
# 只重建 Docs 内的产物和预览，不覆盖启用资源
./Docs/Art/MountainRuleTiles-v1/Build.ps1 -VerifySeams

# 备份后安装到原有三个资源路径
./Docs/Art/MountainRuleTiles-v1/Build.ps1 -VerifySeams -Install

# 对实际安装的 PNG、切片元数据、RuleTile 和主题引用做离线核对
./Docs/Art/MountainRuleTiles-v1/Verify.ps1
```

若 `Temp/MountainFootValidation/layout-preview.json` 已被 Unity 清理，图集仍可重建，但运行布局预览保持原图；该 JSON 来自测试类 `ExportConnectedMountainPreview()` 的真实布局输出。平台预览不依赖 Temp。

`Before-install/` 保留本次替换前的三个 PNG、对应纹理元数据和 RuleTile 内容，不是整个项目/运行时代码的回滚快照。再次安装不会覆盖这个首次备份。`Generated/` 为本轮离线构建产物，Unity 不导入它。

v1 六张 PNG 已作为固定源保存；不要再运行旧 `Extract-Grass.ps1` 从现在的图集中重新取样。旧 MountainComplete16/MountainFoot16 重建入口已停用，避免用旧 47-mask 版本覆盖新资产。
