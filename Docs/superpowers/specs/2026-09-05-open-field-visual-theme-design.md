# 开放场视觉主题设计

> 状态：已确认的实施规格。旧视觉主题数据会直接废弃，不做迁移。

## 目标

将开放场的视觉效果从 MapTest 的色块预览，迁移到正式的 Open Field 地下城生成链路。地形外观、地皮变体、装饰和障碍物均按主题在游戏编辑器中配置；地图生成器只根据这些配置决定位置与写入的 Tile。

## 高度模型

| 地形类别 | 逻辑高度 | 通行 | 视觉职责 |
| --- | ---: | --- | --- |
| Void | 固定 `-1` | 阻挡 | 黑色深渊、悬崖墙、过渡层 |
| Ground | 固定 `0` | 可通行 | 选中的 Ground Style 的基础地皮与装饰 |
| Obstacle | `+1` 至 `+N` | 阻挡 | 障碍顶部、障碍墙、过渡层 |

- Void 不再有不同深度，所有 Void 格恒为 `-1`。
- Obstacle 保留离散高度；越高的障碍会绘制更多墙面段。
- MapTest 的 Organic Terraces 已改为固定 `-1` 的 Void。

## 主题视觉配置

正式主题配置是唯一视觉数据源。MapTest 以后可以读取它做预览，但不能拥有另一套配置。旧主题行不会迁移：`DungeonThemeDataTable.json` 最终是空表，等美术资源就绪后由编辑器新建主题。

### 地形层

`VoidVisual` 配置三张单图 RuleTile：

- 深渊底图：纯黑。
- Void 悬崖墙。
- Void 过渡图。

`ObstacleVisual` 也配置三张单图 RuleTile：

- 障碍顶部。
- 障碍墙。
- 障碍过渡图。

Void 的三种图共用一张 `Void Tilemap`；Obstacle 的三种图共用一张 `Obstacle Tilemap`。它们是单图 RuleTile，不是 15 格或 16 格的地形规则。生成器决定哪个 RuleTile 写入某一个 Tilemap 格，之后 Tilemap 会被烘焙。

一张 Tilemap 的同一网格坐标只能放一个 Tile。因此这三种地形图必须写到互斥的格子；或者让美术图像本身延伸到相邻格，不能先在同一格叠三张图再烘焙。

### Ground Style 列表

主题不再只有一张全局地皮，而是拥有 `GroundStyle` 列表。每个 Style 包含：

- 名称。
- 一个基础地皮 RuleTile（其内部通常是 15 格，但不由代码判断）。
- 多个装饰定义。
- 允许出现的障碍物定义。

生成时，所有高度为零的 Ground 会先被划分为若干 Style 区域。一个格子的 Style 决定它的基础 Tile、可以出现哪些装饰以及可以出现哪些障碍物。

所有 15 格、16 格或其他规格的图都只是 Unity `RuleTile` 资产。生成器只在选中的格子写入对应 RuleTile；最终应该显示哪张子图完全交给 Unity RuleTile 的邻居匹配。项目不能再维护一套自定义 15/16 格选图算法。

### 装饰定义

每条装饰定义从属于一个 Ground Style，包含：

- 一个 Unity `RuleTile`，不论其内部规则图数量是一张、15 张、16 张或其他。
- `R`：种子中心之间的采样半径。
- 最大蔓延次数。
- 没有按美术类型分类的开关；所有装饰都使用同一套扩张方式生成占格。

不存在 `点缀`、`15 格块` 或 `16 格条` 这类美术类别字段。生成器只决定哪些格子被占用、占用多少；种子数由所属地皮区域面积和 `R` 推导，`R` 越大则簇越少。最大蔓延为零时，簇立即停止，天然就是单格点缀。随后把该装饰的 RuleTile 写入已接受的格子，由 Unity 决定最终图形。

- 装饰渲染在基础地皮之上。
- 装饰不碰撞；碰撞只来自障碍物。
- 装饰不能蔓延到其他 Ground Style。
- 最大蔓延大于零的装饰沿用现有的一轮 2×2 支撑清理，移除孤点和一格宽伪影；最大蔓延为零的单格点缀明确不参与该清理。

### 障碍物定义

每个 Ground Style 从其允许的障碍物列表中选择。一个障碍物定义至少包含：

- 通过现有编辑器拖拽流程选择的源 Sprite；保存 Sprite 路径、名称和 UV。
- 占地的格宽和格高。
- 每个占地格一个布尔值的碰撞掩码。
- 生成权重、间距、最大数量、是否允许旋转/翻转，以及视觉排序锚点。

障碍物在地皮装饰之后生成。视觉占地允许有不碰撞的格，但每个标记为碰撞的格必须位于 Ground，并与 Void、Obstacle 地形保持一格间隔：它的八邻格也都必须是 Ground。这样碰撞格不会紧贴固定地形阻挡物。障碍物不再做生成后的全局洪泛连通修复。

## 渲染与碰撞顺序

1. Void 深渊底图。
2. Void 过渡与悬崖墙。
3. Ground Style 的基础地皮。
4. 地皮装饰。
5. Obstacle 过渡与墙面。
6. Obstacle 顶部。
7. 障碍物和角色；视觉排序锚点独立于其碰撞掩码。

## 从资源到游戏的完整流程

### 1. 在 Unity 中准备资源

1. 按项目格尺寸切图；使用 Point 过滤和一致的 Pixels Per Unit。
2. 为 Void 的底图/墙/过渡，以及 Obstacle 的顶/墙/过渡各建一个单图 RuleTile。每种基础地皮和装饰也各建一个 RuleTile，内部规则图数量不限。
3. 在 Unity RuleTile 检视器或 Tile Palette 中配置每个基础地皮和装饰的邻居规则。
4. 准备每个障碍物的 Sprite（或预制体），确定它的格占地和逐格碰撞掩码。

### 2. 在 Open Field 主题编辑器中配置

编辑器为六张地形 RuleTile 提供对象选择器，然后可以添加 Ground Style。每个 Style 再配置基础 RuleTile、装饰条目和允许的障碍物。

数据不直接保存 Unity 编辑器对象引用，而是保存可在运行时加载的资源路径。对象选择器选中 Tile、RuleTile 或 Sprite 后，把资产路径写入 JSON。运行时由 `ResourceComponent` 按地下城场景 owner 加载、引用计数并释放这些资源。

### 3. 生成语义地图

1. 正式的 Open Field 地形生成器生成地形场。
2. 它写入高度格：Void 恒为 `-1`，Ground 恒为 `0`，Obstacle 为正整数台阶。
3. 校验可走区域，放置出生点、出口、兴趣点和内容；若必须的点无法互相到达，则换种子重试。

当前 `OpenFieldDungeonLayout` 只保存 `Void`、`Ground`、`Obstacle` 三个类别；必须扩展为保存高度整数，才能在正式游戏中绘制多段 Obstacle 墙面。

### 4. 解析 Tilemap，再烘焙地形

正式场景构建器创建临时的运行时 `Grid` 与 Tilemap：

1. 一张包含 Void 底图、过渡和墙格的 Void Tilemap。
2. 一张基础地皮 Tilemap 和一张地皮装饰 Tilemap。
3. 一张包含 Obstacle 过渡、墙和顶部格的 Obstacle Tilemap。

构建器将所选 RuleTile 写入对应临时 Tilemap 格。Unity 的 RuleTile 邻居计算负责解析实际 Sprite；代码不参与 15/16 格选图。

烘焙阶段只读取这些已经解析出的 Sprite，合成为运行时网格所需的 Back 与 Top 两张纹理，不再自行选 RuleTile 图。合成遵循已经采用的垂直高度投影：`x` 不偏移，正高度向上偏移；Void 向下一阶；一个暴露的 Obstacle 边会按正高度的每一阶绘制一段墙。临时 Grid 随后释放；实际 play 场景只保留两张烘焙层和可动态排序的障碍物。

### 5. 填充地皮与装饰

1. 找出四方向连通的零高度 Ground 区域。
2. 放置 Ground Style 种子，并以每轮每个种子最多扩一格的公平方式扩张，直到每个 Ground 格都有 Style。
3. 对 Style 结果做一轮无 2×2 支撑清理，去掉细长尖端。
4. 将每个 Style 的基础 RuleTile 写到基础地皮 Tilemap。
5. 对每条装饰：按 `R` 和 Style 面积计算种子数，只在所属 Style 内紧凑蔓延到最大次数，保留成功占格，再将 RuleTile 写到装饰 Tilemap。

### 6. 填充障碍物

1. 从该 Style 的允许障碍列表选择候选。
2. 检验碰撞掩码格的地图边界、已预留格，以及与 Void/Obstacle 的一格间隔。
3. 生成拖入的 Sprite 视觉，并只为碰撞掩码中为真的格生成碰撞。
4. 不做全局连通修复；间距和局部地形间隔规则负责避免其出现在地形狭道中。

### 7. 构建最终可玩场景

1. 为 Void 与 Obstacle 语义格生成地形碰撞。
2. 再添加障碍物自身的逐格碰撞。
3. 延用现有运行时场景/ECS 链路生成玩家、出口、遭遇和宝箱。
4. 角色与障碍物按视觉排序锚点排序，该排序不依赖其碰撞占地。

## 必须完成的渲染迁移

当前正式链路用 `OpenFieldDungeonSceneDataBuilder.ResolveTerrainTile` 从自定义 3×3 格中挑一个 Sprite，并由 `DungeonTileVisualBuilder` 把 Sprite quad 批量合成网格。地形部分将改为以上的临时 Tilemap 解析与烘焙流程。玩家、怪物、出口、宝箱及非 Tile 环境对象的既有 ECS 生成保持不变。

## 必须修改的集成点

- `OpenFieldDungeonVisualData`：以地形层数据和 Ground Style 列表替换固定的 Void/Ground/Obstacle 3×3 格。
- `DungeonEditorWindow.OpenField`：提供新的列表、对象选择器与碰撞掩码编辑器。
- `OpenFieldDungeonSceneDataBuilder`：从主题配置选择地形、地皮、装饰和障碍，输出运行时场景数据。
- 运行时场景渲染器：解析临时 Tilemap，将 Unity 已选好的 Sprite 烘焙进 Back/Top 网格层，并从障碍物掩码生成碰撞。
- `OpenFieldMapTestDemo`：以后可选地读取同一份主题配置预览；不能重新成为一套平行视觉配置。

## 明确不迁移的内容

- `DungeonThemeDataTable.json` 重写为 `Rows` 为空的表。
- MapTest 持久化高度图缓存不再需要，会被删除。
- 旧的固定 3×3 地形数据、预览窗口和自定义选图器随迁移删除。
- 原始 Sprite 与 RuleTile 美术资产不删除。
