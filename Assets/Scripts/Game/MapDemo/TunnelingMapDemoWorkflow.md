# TunnelingMapDemo 正式生成流程说明

本文档只保留 `TunnelingMapDemo` 的正式生成链路。

不包含：
- 步进调试
- 骨架显示开关
- 分析覆盖层显示
- 调试坐标与调试方块
- 日志、Dump、颜色覆盖等观察性功能
- 任何只为定位问题而存在的辅助逻辑

关联脚本：
- `Assets/Scripts/Game/MapDemo/TunnelingMapDemo.cs`
- `Assets/Scripts/Game/MapDemo/DungeonMakerTunnelingGenerator.cs`
- `Assets/Scripts/Game/MapDemo/DungeonMakerTunnelingConfig.cs`

## 一、系统目标
`TunnelingMapDemo` 的正式目标不是单纯生成一张二维数组地图，而是生成一张已经具备玩法语义的完整地牢地图。

最终结果需要同时包含：
- 通路、房间、前厅组成的结构地图
- 死路裁剪后的最终可玩路径
- 起始房间
- 下一关房间
- 怪物分布
- 宝箱分布

正式流程分成四个阶段：
1. 原始地图生成
2. 质量筛选
3. 死路裁剪
4. 特殊房、怪物、宝箱落位

## 二、正式执行顺序
### 1. 原始地图阶段
入口：
- `GenerateDemoMap()`

职责：
- 校验并规范参数
- 以主种子生成候选地图
- 做质量筛选
- 保留一份“未裁剪、未落玩法内容”的原始地图

这一阶段不会做：
- 死路裁剪
- 起始房选择
- 下一关房选择
- 怪物生成
- 宝箱生成

### 2. 最终地图阶段
入口：
- `ApplyDemoDeadEndDeletions()`

职责：
1. 对原始地图执行死路裁剪
2. 在裁剪后的地图上选择特殊房
3. 在裁剪后的地图上生成怪物和宝箱

正式玩法结果必须基于“裁剪后的地图”，而不是原始地图。

## 三、核心数据结构
### 1. `DungeonMakerTunnelingResult`
这是正式链路中最核心的结果对象。

它包含：
- 最终地图格子数据
- 每个格子的来源标记
- 区域列表
- 骨架段列表
- 骨架连接列表
- 骨架挂点列表
- 统计数据

后续所有正式逻辑都围绕这份结果运行。

### 2. `DungeonMakerSquareData`
地图的基础格子类型。

正式流程重点使用：
- `CLOSED`
- `OPEN`
- `IR_OPEN`：房间格
- `IT_OPEN`：隧道格
- `IA_OPEN`：前厅格
- `MOB1 / MOB2 / MOB3`
- `TREAS1 / TREAS2 / TREAS3`
- `COLUMN`

所有正式玩法内容最后都必须回写到这一层。

### 3. `DungeonMakerRegion`
区域是对 tile 的语义分组。

当前正式使用的区域类型：
- `Corridor`
- `Room`
- `AnteRoom`

每个区域保留：
- `Id`
- `Kind`
- `TileIndices`
- `RoomSizeClass`
- `SpecialRoomRole`

作用：
- 让后续逻辑可以按区域处理，而不是每次重新扫描整张 tilemap 猜语义
- 让特殊房、怪物、宝箱都能基于区域规则落位

### 4. `DungeonMakerSkeletonSegment`
这是死路裁剪的核心结构。

每个骨架段记录：
- `Id`
- `BuilderId`
- `Start`
- `End`
- `OwnedTileIndices`

其中最关键的是：
- `OwnedTileIndices`

含义是：
- 该逻辑骨架段真正拥有哪一批走廊格

因此死路裁剪删除的不是“线”，而是“该骨架段所拥有的整条真实通路”。

### 5. `DungeonMakerSkeletonLink`
表示骨架段之间的逻辑连接关系。

它不是几何上的 tile 邻接，而是逻辑图上的边。

作用：
- 重建整张通路骨架图
- 作为死路裁剪时的图结构输入

### 6. `DungeonMakerSkeletonAttachment`
表示某条骨架段是否挂着“有价值终点”。

当前最重要的有价值终点是：
- 房间

作用：
- 区分“最终通向房间的有效分支”
- 和“没有价值终点的无用支脉”

## 四、阶段一：原始地图生成
入口函数：
- `GenerateDemoMap()`

### 1. 参数规范化
首先调用：
- `ValidateParameters()`

作用：
- 修正非法或颠倒的区间
- 保证概率分母合法
- 保证尺寸参数处于合理范围

例如：
- 房间数区间会被规范为 `min <= max`
- 走廊怪物概率分母至少为 `1`
- 遭遇数量区间会被规范为合法范围

### 2. 主种子确定
规则：
- 如果 `_randomizeSeedOnGenerate = true`
  - 本次先随机生成一个新的主种子
- 否则
  - 使用 `_seed`

这个主种子是后续质量筛选的起点，不一定就是最终用于地图生成的种子。

### 3. 生成候选原图
调用：
- `GenerateQualifiedRawResult(...)`

作用：
- 从主种子派生多个候选种子
- 生成多个候选地图
- 挑出最符合目标的那一张原始地图

输出：
- `_lastRawResult`

注意：
- 到这里仍然只有结构图
- 还没有做死路裁剪和玩法落位

## 五、质量筛选算法
入口函数：
- `GenerateQualifiedRawResult(...)`

### 1. 目标
随机地牢会有波动，所以系统不会直接接受第一次结果，而是会在多个候选结果中挑一张更符合目标的图。

当前质量目标包括：
- 大房数量
- 中房数量
- 小房数量
- 可走格总量

### 2. 候选种子派生
函数：
- `DeriveCandidateSeed(masterSeed, attemptIndex)`

原理：
- 不是重新“随便随机一次”
- 而是把主种子和尝试序号做一次确定性混洗

作用：
- 同一个主种子
- 同一个配置
- 同一个尝试上限

始终得到相同的候选种子序列。

### 3. 候选地图生成
每个候选种子都会调用：
- `DungeonMakerTunnelingGenerator.Generate(candidateSeed, _config)`

得到一张完整原始地图。

### 4. 合格判定
函数：
- `IsMapQualified(stats)`

当前判定条件：
- `LargeRooms` 落在 `_largeRoomRange`
- `MediumRooms` 落在 `_mediumRoomRange`
- `SmallRooms` 落在 `_smallRoomRange`
- `WalkableTiles` 落在 `_walkableTileRange`

### 5. 评分回退
如果所有候选都不合格，则进入评分回退。

函数：
- `ScoreMapQuality(stats)`

原理：
- 计算当前地图与目标区间的偏差
- 偏差越大，分数越低
- 房间结构偏差权重大于可走格偏差

当前大致权重：
- 大房偏差 × 2000
- 中房偏差 × 1000
- 小房偏差 × 500
- 可走格偏差 × 1

最后选得分最高的候选原图。

## 六、底层挖掘算法原理
底层生成器位于：
- `DungeonMakerTunnelingGenerator`

它不是传统“整图一次性雕刻”，而是一个 Builder 驱动的增量挖掘系统。

### 1. Builder 模型
当前正式 Builder 只有两类：
- `Tunneler`
- `Roomie`

其中：
- `Tunneler` 负责挖主通路、决定分叉、拐弯、变宽、变窄、插入前厅、生成子 Builder
- `Roomie` 负责在某个连接点尝试长出一个矩形房间

### 2. 迭代模型
生成器不是“一个 Builder 一次挖完”，而是分迭代推进。

每一轮迭代的规则是：
- 只有 `Generation == ActiveGeneration` 的 Builder 会执行
- 每个存活 Builder 在该轮只执行一次 `StepAhead()`
- 这一轮结束后，`ActiveGeneration++`

因此：
- 子代不是在本轮立刻连续跑完
- 而是进入后续 generation 依次推进

这保证了整张地牢是“逐步生长”的，而不是深度优先一次性钻到底。

### 3. `Tunneler` 的状态
每个 `Tunneler` 主要包含这些逻辑状态：
- `Location`：当前头部位置
- `Forward`：当前朝向
- `_tunnelWidth`：当前通路半宽
- `_stepLength`：本轮计划向前挖多长
- `Age`
- `MaxAge`
- `Generation`
- `_intDirection`：内部偏向方向
- `BuilderId`

它本质上表示：
- 一个会逐轮前进、会老化、会派生子代的通路生长体

### 4. `Tunneler.StepAhead()` 的正式顺序
每个 `Tunneler` 的一次动作大致分为下面几步。

#### 4.1 存活与代次检查
先检查：
- 当前是否已经退休
- 当前代次是否等于 `ActiveGeneration`

如果不是当前激活代，不执行。

#### 4.2 年龄推进
执行时会先增长 `Age`。

如果：
- `Age >= MaxAge`

则该 Builder 退休，不再继续挖掘。

这保证每个 Builder 的生长长度有上限，不会无限延伸。

#### 4.3 前方空间探测
函数：
- `FrontFree(...)`

探测内容包括：
- `frontFree`
- `leftFree`
- `rightFree`

含义是：
- 当前朝向前方还有多少连续可用空间
- 左右两侧还有多少空间可供派生或长房

这是本轮所有决策的基础。

#### 4.4 本轮房间目标尺寸计算
`Tunneler` 本轮会先决定：
- 如果要在侧边长房，偏向生成哪种大小
- 如果要在分叉处长房，偏向生成哪种大小

这里分别使用配置中的：
- `RoomSizeProbS`
- `RoomSizeProbB`

它们会按照当前 `_tunnelWidth` 选取一组概率，然后随机得到：
- `Small`
- `Medium`
- `Large`

也就是说：
- 宽隧道和窄隧道派生房间时，概率分布是不同的

#### 4.5 `Roomie` 派生延迟计算
不是每次想长房就立即长。

系统会参考：
- `BabyDelayProbsRoomie`

根据当前宽度决定：
- 这个房间子代要不要延迟若干 generation 才真正开始执行

作用：
- 打散房间出现节奏
- 避免一个分支上的房间过密

#### 4.6 收尾逻辑判定
如果出现以下条件之一：
- `frontFree < 2 * _stepLength`
- `Age == MaxAge - 1`

则该 `Tunneler` 会进入“收尾逻辑”而不是普通前进。

收尾逻辑中会继续检查：
- `openAhead`
- `guaranteedClosedAhead`
- `roomAhead`

这一步的目标是：
- 尽量让分支自然结束
- 必要时再派生一个补救分支
- 或者在最后时机长出房间

#### 4.7 正常隧道挖掘
如果不进入收尾逻辑，则执行：
- `BuildTunnel(...)`

作用：
- 把当前这一段直线通路 carve 到地图里
- 同时登记该段真实拥有的 tile
- 同时生成红色逻辑骨架段

正式版本里，地图通路主要来源于这里。

### 5. `Tunneler` 的收尾规则
这一块最容易被忽略，但它决定了很多支路末端的形态。

#### 5.1 前方已经有房间
如果前方直接遇到房间：
- 当前版本不再额外生成门
- 而是直接把最后这段通路挖进去或并到房间边缘

含义：
- 房间和走廊的连接不再依赖门 tile
- 而是依赖几何相接和房间挂点关系

#### 5.2 前方是保证封闭区域，且当前隧道很窄
系统可能会派生：
- `Redirect` 型 `Tunneler`

作用：
- 让这条支脉临终前再试一次转向逃逸
- 避免太多分支直接顶墙死亡

#### 5.3 最后一次长房机会
如果当前 Builder 已接近寿命终点，但仍然满足房间条件：
- 会尝试向前派生 `Roomie`

也就是说：
- 即使主分支快结束了，也会争取再产出一个房间

#### 5.4 `LastChanceTunneler`
在一些特定封堵场景下，会生成“最后机会”子隧道：
- 左侧
- 右侧
- 前侧组合

作用：
- 在分支死亡前尝试再开一个小方向
- 看能不能把支路导到可用区域

这些都是明确的“子 Tunneler 创建规则”，不是后补几何连接。

### 6. 变宽、变窄与前厅
正常挖完一段之后，`Tunneler` 会决定下一段结构。

#### 6.1 变宽概率
使用：
- `SizeUpProb`

根据当前宽度决定：
- 下一轮是否尝试把 `_tunnelWidth + 1`

#### 6.2 变窄概率
使用：
- `SizeDownProb`

根据当前宽度决定：
- 下一轮是否尝试把 `_tunnelWidth - 1`

#### 6.3 插入前厅
如果本轮需要从大通路过渡、或配置允许，会探测：
- `smallAnteRoomPossible`
- `largeAnteRoomPossible`

若满足条件，就调用：
- `BuildAnteRoom(...)`

作用：
- 在大隧道转窄、转向或派生前，先插一个过渡空间

前厅的意义：
- 视觉和空间上作为缓冲
- 也是后续怪物落位的一类区域

#### 6.4 变宽与前厅的关系
如果系统想变宽，但前方不具备必要的前厅空间：
- 这次变宽会被取消

也就是说：
- 不是抽到了变宽就一定能生效
- 还要通过空间探测

### 7. 直行、拐弯与双生分支
在挖完当前段之后，系统会决定下一步结构。

#### 7.1 是否尝试拐弯
会结合：
- 前方空间
- 左右空间
- 当前内部偏向方向 `_intDirection`

来判断：
- 继续直行
- 向左转
- 向右转

#### 7.2 是否双生
如果本轮是转向结构，使用：
- `_turnDoubleSpawnProb`

如果本轮是直行结构，使用：
- `_straightDoubleSpawnProb`

来决定：
- 本轮除了主方向之外，是否再额外派生一个子 Builder

这就是“同一轮一个母 Builder 分出多个孩子”的主要来源。

### 8. 子代生成规则
这部分是正式算法里最关键的生成规则。

#### 8.1 `Tunneler` 会在哪些情况下生成子 `Tunneler`
主要包括：
- 转向时，额外保留另一个方向的分支
- 直行时，在左右侧额外派生支脉
- 收尾时触发 `Redirect`
- 收尾时触发 `LastChanceTunneler`

只要发生这些“母生子”的操作，逻辑上就视为一条父子连接。

#### 8.2 `Tunneler` 会在哪些情况下生成 `Roomie`
主要包括：
- 直行后想在左右侧长房
- 转向点附近想长房
- 收尾阶段的最后长房机会

也就是说：
- `Roomie` 不是独立游走型 Builder
- 它总是由某个 `Tunneler` 派生出来

#### 8.3 子代并不会立即执行
子代创建后会带有自己的 `Generation`。

只有等到：
- `ActiveGeneration == child.Generation`

它才会在自己的那一轮执行 `StepAhead()`。

所以：
- 母代本轮产生的多个子代
- 会在后续 generation 逐步展开

#### 8.4 子代参数不是照搬父代
函数：
- `AdjustChildTunnelParameters(...)`

规则大致是：
- 若这次是变宽分支
  - `width + 1`
  - `stepLength + 2`
- 若这次是变窄分支
  - `width - 1`
  - `stepLength - 2`
  - 且宽度、长度都有下限保护

这保证了：
- 子分支和父分支可以逐渐长成不同尺度

### 9. `Roomie` 的正式规则
`Roomie` 只负责“从一个连接口尝试长出房间”。

#### 9.1 执行前提
它会先检查：
- 当前 generation 是否激活
- 当前房间类型是否还需要更多数量
- 自己是否还没退休

#### 9.2 房间尺寸探测
它会根据：
- 目标房间大小类别
- `RoomAspectRatio`
- 最小、最大房间面积

探测前方是否能放下一个矩形房间。

#### 9.3 成功时的行为
成功后会：
- 把矩形区域 carve 成 `IR_OPEN`
- 入口格也写成 `IR_OPEN`
- 注册一个 `Room` 区域
- 给区域写入 `RoomSizeClass`
- 给父骨架段挂上“有价值房间”标记

这一步很关键，因为后续死路裁剪是否保留一条分支，取决于它是否最终长出了房间。

#### 9.4 失败时的行为
如果房间放不下：
- 该 `Roomie` 就退休
- 不会再继续变成别的类型

所以房间生成是一次性的空间尝试，而不是多轮重新规划。

## 七、阶段二：死路裁剪
入口：
- `ApplyDemoDeadEndDeletions()`

核心函数：
- `ProcessDeadEndsOnly(...)`

### 1. 为什么要先裁路再落玩法
如果先放怪、箱子、特殊房，再裁路，会出现：
- 怪和箱子落在最终被删除的分支里
- 起始房或下一关房落在最终不可达位置

因此正式顺序必须是：
1. 先裁死路
2. 再落玩法内容

### 2. 当前死路算法不是哪些方案
当前正式逻辑不是：
- 单格 `neighborCount <= 1` 递归剥离
- BFS 可达性保留

原因：
- 单格剥离不适合宽走廊
- BFS 只能回答“是否连到锚点”，不能稳定表达“无价值支脉”

### 3. 当前正式算法
当前使用：
- 基于逻辑骨架树的整段修枝

### 4. 构图方式
把通路结构抽象成骨架图：
- 节点：`DungeonMakerSkeletonSegment`
- 边：`DungeonMakerSkeletonLink`
- 价值挂点：`DungeonMakerSkeletonAttachment`

### 5. 修枝规则
规则是：
- 如果一条骨架段是叶子
- 并且没有房间价值挂点
- 就视为无用支脉，删除

删除后：
- 它的父段可能变成新的叶子
- 继续递归检查
- 直到骨架图稳定

本质上就是：
- 一棵生成骨架树上的逆向修枝

### 6. 为什么能删除宽于 1 的通路
因为每条骨架段都记录了：
- `OwnedTileIndices`

所以真正删除时：
- 删除的是整条骨架段所拥有的全部走廊格

而不是只删一条中心线。

## 八、阶段三：特殊房选择
函数：
- `FinalizeGeneratedLayout(...)`
- `AssignSpecialRooms(...)`

### 1. 选择时机
特殊房必须在死路裁剪后再选。

原因：
- 只有裁剪后的地图才是最终保留下来的结构

### 2. 当前规则
从最终 `Room` 区域里：
- 随机选一个 `Small` 房间作为 `Start`
- 随机选一个 `Large` 房间作为 `NextLevel`

### 3. 随机原理
使用：
- `new System.Random(result.Seed ^ 常量)`

保证：
- 同一张最终地图
- 每次都会选中同样的起始房和终点房

### 4. 回写方式
结果写回：
- `DungeonMakerRegion.SpecialRoomRole`

当前角色：
- `None`
- `Start`
- `NextLevel`

### 5. 后续扩展
其他功能房目前未实现，代码中保留了 `TODO`，以后可以继续从：
- 小房
- 中房
- 大房
- 前厅

中按配置挑选。

## 九、阶段四：怪物与宝箱生成
函数：
- `AssignEncounterMarkers(...)`

### 1. 执行时机
怪物和宝箱始终在：
- 死路裁剪之后
- 特殊房选择之后

这样才能保证：
- 起始房规则已知
- 路径结构已经稳定

### 2. 走廊规则
走廊只生成：
- `MOB1`

生成方式：
- 逐格独立概率
- 概率为 `1 / _corridorLevel1SpawnChanceDenominator`

### 3. 前厅规则
前厅生成：
- `MOB1 / MOB2`

数量：
- 从 `_anteRoomMonsterCountRange` 内随机

### 4. 小房规则
小房：
- 固定生成一个 `TREAS1`
- 若该房间是 `Start`，不生成怪物
- 否则生成 `MOB1 / MOB2`
- 数量从 `_smallRoomMonsterCountRange` 内随机

### 5. 中房规则
中房：
- 固定生成一个 `TREAS2`
- 生成 `MOB1 / MOB2`
- 数量从 `_mediumRoomMonsterCountRange` 内随机

### 6. 大房规则
大房：
- 固定生成一个 `TREAS3`
- 生成 `MOB1 / MOB2 / MOB3`
- 数量从 `_largeRoomMonsterCountRange` 内随机

### 7. 放置原则
所有怪物与宝箱都遵循：
- 从候选格列表中随机抽取
- 抽中过的格子从候选集中移除

所以：
- 同一区域内不会互相重叠

## 十、正式参数说明
以下只列真正影响正式结果的参数。

### 1. `TunnelingMapDemo` 参数
#### 基础生成
- `_seed`
  - 手动指定主种子
- `_randomizeSeedOnGenerate`
  - 每次生成前是否先随机主种子
- `_cellSize`
  - 预览与坐标映射用单格尺寸，不影响拓扑生成
- `_config`
  - 底层生成器配置对象

#### 质量筛选
- `_requireQualifiedMap`
  - 是否启用多候选质量筛选
- `_maxQualificationAttempts`
  - 最多尝试多少个候选种子
- `_largeRoomRange`
  - 目标大房数量区间
- `_mediumRoomRange`
  - 目标中房数量区间
- `_smallRoomRange`
  - 目标小房数量区间
- `_walkableTileRange`
  - 目标可走格总量区间

#### 死路裁剪
- `_pruneDeadEnds`
  - 是否启用正式死路裁剪

#### 遭遇规则
- `_corridorLevel1SpawnChanceDenominator`
  - 走廊 `MOB1` 概率分母，`100` 表示 `1/100`
- `_anteRoomMonsterCountRange`
  - 前厅怪物数量区间
- `_smallRoomMonsterCountRange`
  - 小房怪物数量区间
- `_mediumRoomMonsterCountRange`
  - 中房怪物数量区间
- `_largeRoomMonsterCountRange`
  - 大房怪物数量区间

### 2. `DungeonMakerTunnelingConfig` 主要参数
#### 地图尺寸
- `MapRows`
- `MapColumns`

决定原始数组大小。

#### Builder 初始参数
- `Tunnelers`
  - 初始 `Tunneler` 列表
  - 决定开局从哪里开始、朝哪里走、初始宽度和步长是多少

#### 房间数量上限
- `MaxSmallDungeonRooms`
- `MaxMediumDungeonRooms`
- `MaxLargeDungeonRooms`

#### 房间面积
- `MinSmallRoomSize`
- `MinMediumRoomSize`
- `MinLargeRoomSize`
- `MaxRoomSize`

#### 房间形状
- `RoomAspectRatio`

#### 房间尺寸概率
- `RoomSizeProbS`
  - 侧向派生房间的尺寸概率
- `RoomSizeProbB`
  - 分叉位置派生房间的尺寸概率

#### 走廊尺寸变化
- `SizeUpProb`
- `SizeDownProb`

#### 子代生成节奏
- `BabyDelayProbsRoomie`
- `GenSpeedUpOnAnteRoom`

#### 双生分支概率
- `StraightDoubleSpawnProb`
- `TurnDoubleSpawnProb`

#### 前厅相关
- `AnteRoomProb`
- `ColumnsInTunnels`

## 十一、正式结果总结
当前正式地图的完整生成顺序是：

1. 规范参数
2. 确定主种子
3. 派生候选种子并生成候选原图
4. 按目标房间数与可走格数筛选出最佳原图
5. 对原图执行基于骨架树的死路裁剪
6. 在裁剪后的最终房间里选择：
   - 一个起始小房
   - 一个下一关大房
7. 在裁剪后的最终地图上生成怪物与宝箱
8. 输出最终可玩的 DEMO 地图

这就是当前版本真正参与结果生成的正式链路。
