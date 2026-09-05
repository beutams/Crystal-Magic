# 开放地牢障碍物多图层编辑器设计

## 目标

让一个开放地牢障碍物由多个可定位的 Sprite 组成。编辑器以障碍物 Footprint 为画布，设计者能将 Project 中的 Sprite 直接拖入任意格子，并在同一格使用多个可排序图层；碰撞仍由单独的格子遮罩决定。

## 数据模型

`OpenFieldObstacleData` 保留 `FootprintWidth`、`FootprintHeight`、生成权重、间距、旋转、翻转、视觉排序锚点与 `CollisionMask`。新增 `SpriteLayers`：按列表顺序由后向前渲染。

每个 `OpenFieldObstacleSpriteLayerData` 有名称与一组 `OpenFieldObstacleSpriteCellData`。单元包含 `X`、`Y` 和 `OpenFieldSpriteReferenceData`。一个图层在同一坐标最多有一个 Sprite；不同图层可以使用同一坐标，因此能够组合树干、树冠或前景遮挡。

不再使用 Unity 的向量结构保存主题 JSON。UV 继续使用已经引入的 `OpenFieldSpriteUvData`；障碍物视觉排序锚点改为仅含 `X/Y` 的数据结构，运行时再转换为 Unity 向量。

旧的单 `Sprite` 字段只作为兼容输入：`EnsureValid` 发现没有图层但存在旧 Sprite 时，自动创建第一个图层并将它放入 `(0, 0)`。新编辑器不再显示该字段。

## 编辑器交互

障碍物编辑区在 Footprint 尺寸与生成参数之后显示“Sprite 图层”。

每个图层有删除、上移、下移操作和一个与 Footprint 对应的格子画布。格子会显示已放 Sprite 的缩略图；将 Project 中的 Sprite 拖至格子可替换该图层该格的内容，清空按钮删除该格 Sprite。新增图层默认为空，新增图层渲染在已有图层前方。

改变 Footprint 不会删除超出新边界的已有 Sprite 单元；它们在画布与运行时中会被忽略，之后扩大 Footprint 会恢复显示。Collision Mask 仍以 Footprint 的每格开关编辑，与任意图层的 Sprite 存在与否无关。

## 生成与渲染

障碍物摆放、旋转、翻转、Footprint 占用和 Collision Mask 规则保持不变。一个摆放结果包含：

- 一组碰撞格，只生成一次 ECS 碰撞体；
- 所有有效图层单元的视觉生成项，每项带 Sprite、相对格坐标、图层顺序与摆放后的旋转/翻转。

场景数据构建器将每个有效视觉单元转换为一个世界坐标 Sprite 实体。运行时先按图层顺序创建视觉实体，并给后方到前方图层分配很小的固定深度偏移；同一障碍物的 `VisualSortAnchor` 仍决定它相对角色与其他障碍物的主要遮挡顺序。碰撞实体不会随视觉图层重复生成。

## 边界与兼容性

- 空图层、空 Sprite 和越出 Footprint 的单元都不会生成视觉实体。
- 旋转与翻转同时作用于视觉单元坐标和碰撞遮罩，因此组合图不会错位。
- 旧 JSON 的单 Sprite 障碍物会得到单层、单格的等价结果。
- 新 JSON 只含原始数值与资产路径/精灵名，不会再次触发 Newtonsoft 对 Unity 向量的循环序列化错误。

## 验收标准

- 可为一个障碍增加多个图层，并在每层的任意 Footprint 格子拖入 Sprite。
- 同一格的不同图层都可显示，且后添加的图层位于前方。
- 仅 Collision Mask 勾选的格子生成碰撞，与 Sprite 数量无关。
- 保存与重载主题不会报 JSON 序列化错误，且保留图层、格子位置、图层顺序和精灵引用。
- 单 Sprite 的旧障碍物在首次验证后仍能显示为一个图层。
