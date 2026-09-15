# 联机战斗同步设计

本文定义大厅进入战斗后的服务器权威同步、客户端远端表现，以及客户端本地玩家预测/回滚方案。

本文不实现完整技能预测、P2P 回滚或客户端权威判定。战斗服务器是唯一权威；客户端发送操作、显示服务器状态。单机模式继续使用现有通用 Unit 系统，不受本文改动。

## 1. 核心结论

### 1.1 三种单位运行身份

| 位置 | 本地玩家 | 其他玩家、怪物、投射物 |
| --- | --- | --- |
| 服务器 Battle World | 全部运行权威战斗逻辑 | 全部运行权威战斗逻辑 |
| 客户端 Battle World | 只运行本地玩家的可预测逻辑 | 不运行战斗逻辑，只接收状态并表现 |
| 单机 World | 继续运行现有通用 Unit 系统 | 继续运行现有通用 Unit 系统 |

服务器计算所有玩家，不是为每名玩家创建一个 System。一个 ECS System 每个逻辑帧 Query 出房间的所有 `OnlineBattlePlayerTag` 实体并逐个处理。每个客户端则只让自己的 Player 命中该 System 的预测 Query。

### 1.2 联机 Player 不是 OOP 对象

联机 Player 的预测状态必须保留在 `IComponentData`、`DynamicBuffer` 中，计算由 `ISystem` 完成。

不创建持有私有状态的 `Player` 模拟类，也不由 `ClientBattleManager` 手写玩家行为。Manager 只保存网络帧队列、快照历史，并手动调度 ECS SystemGroup。

### 1.3 服务器权威、客户端预测的边界

第一阶段仅预测本地 Player 的移动和朝向。客户端不预测伤害、Buff、怪物 AI、掉落、交互、投射物命中和动态刚体推挤。它们以服务器状态帧或事件帧为准。

这样客户端可立即响应移动输入，同时不会为一个 Player 的回滚而重演怪物、远端玩家和整片物理世界。

### 1.4 身份与时间的统一约束

- `ulong accountId` 是 Player 的稳定身份。每个战斗房间内每个 `accountId` 至多对应一个 Player 战斗实体；该 Player 的网络目标 Id 直接使用 `accountId`，不再为 Player 使用本地自增 Id 或 `Guid`。
- 怪物、投射物、掉落物等非 Player 实体使用 Battle Server 分配的 `ulong battleEntityId`。它只在本次 `battleId` 内唯一，网络状态/事件以 `(battleId, battleEntityId)` 定位。
- 所有逻辑时间使用整数 `uint frame` 与固定 `fixedDeltaTime`。协议不以客户端 `Time.time` 作为权威时间。
- 客户端提交的输入帧只是一项请求，且每个输入包带单调递增 `inputSequence`。服务器验证身份与输入后，按规则放入未来帧，例如 `acceptedFrame = max(requestedFrame, serverFrame + inputLeadFrames)`，并以 `InputAck { inputSequence, acceptedFrame }` 回传实际排期。客户端若被改帧，必须把该输入在历史中的模拟位置重排并从最早受影响帧重演；不能假定请求帧一定被接受。拥有者状态帧另带 `LastAcceptedInputFrame`，用于确认可清理的连续历史。
- `B2C_StartFrame` 的 `startFrame` 必须是服务器当前帧之后留有初始化余量的未来帧。客户端收到后以该帧为本地时间基准；迟到客户端从服务器完整状态追到当前帧，不得把自己的本地零帧当成服务器零帧。

## 2. World、Tag 与系统组

### 2.1 计划中的 Tag

```csharp
OnlineBattlePlayerTag   // 该实体是联机战斗 Player
BattleSimulateTag       // 本 World 应运行战斗计算
BattleAuthorityTag      // 服务器权威实体
BattlePredictedTag      // 客户端本地预测实体
BattleRemoteTag         // 客户端仅表现的远端实体
```

约束：

- 服务器的全部可战斗单位拥有 `BattleSimulateTag + BattleAuthorityTag`。
- 服务器的 Player 额外拥有 `OnlineBattlePlayerTag`。
- 客户端本地 Player 拥有 `OnlineBattlePlayerTag + BattleSimulateTag + BattlePredictedTag`。
- 客户端远端 Player、怪物和服务器投射物拥有 `BattleRemoteTag`，不拥有 `BattleSimulateTag`。
- `BattleRemoteTag` 和 `BattleSimulateTag` 对同一客户端实体互斥。

### 2.2 联机 Player 专用 ECS 系统

增加独立的 `BattlePlayerSimulationSystemGroup`。它运行固定逻辑帧，成员按顺序为：

```text
BattlePlayerInputApplySystem
-> BattlePlayerStateSystem
-> BattlePlayerMoveSystem
-> BattlePlayerSkillSystem（后续）
-> BattlePlayerPostProcessSystem
```

这些 System 的 Query 必须含有：

```text
OnlineBattlePlayerTag + BattleSimulateTag
```

因此同一份 System 类型在服务器会处理全部 Player，在客户端只处理本地 Player。所有实际规则写在这些 ECS System 或它们的 Burst-friendly 静态 Utility 中，双端不各写一份实现。

现有通用系统仍可用于单机和服务器非 Player 单位。涉及 Player 行为的旧系统必须显式排除联机 Player，避免重复计算：

```text
UnitMoveSystem
StateScriptSystem
SkillReleaseSystem
PlayerEquipmentPropertySystem
以及所有会改写联机 Player 战斗状态的通用系统
```

服务器的怪物仍可暂时走 `UnitPerceptionSystem`、`BehaviorTreeSystem`、`StateScriptSystem` 和现有技能/效果系统。以后 Player 技能与怪物发生作用时，Player 专用 System 向服务器权威效果队列写入命令；真正修改目标、伤害、Buff 的执行仍只在服务器发生。

### 2.3 手动帧调度仍是 ECS

`OnUpdate` 并不妨碍回滚；只要 System 不依赖全局输入、存档或不可恢复 managed 状态，它就可以被重复更新。

```text
服务器 NetworkTimer 的第 F 帧
  -> Battle World 设置固定 TimeData(F, fixedDeltaTime)
  -> 更新 BattlePlayerSimulationSystemGroup
  -> 更新服务器怪物/世界逻辑组
  -> 收集状态与事件

客户端预测第 F 帧
  -> 写入 InputHistory[F] 到本地 PlayerBattleInputComponent
  -> Battle World 设置同一固定 TimeData(F, fixedDeltaTime)
  -> 更新 BattlePlayerSimulationSystemGroup
  -> 保存本地 Player 的帧末快照
```

回滚时客户端恢复一份旧快照，按顺序重写 `F + 1` 之后的输入，再重复更新同一个 `BattlePlayerSimulationSystemGroup`。不直接调用某个 System 的 `OnUpdate`，也不在回滚中更新表现组。

## 3. Player 数据与输入

### 3.1 PlayerBattleInputComponent

每个联机 Player 都带 `PlayerBattleInputComponent`。它表示“本次正在模拟的逻辑帧输入”，由帧分析/输入应用系统写入。

输入帧消息及客户端历史必须另有纯数据记录，例如 `BattleInputFrame`：

```text
Frame
Move
PointerWorldPosition
按住状态（Primary、Skill 等）
一次性按下动作（Interact、UseProp、切换链等）
SkillChainIndex、PropIndex
```

不能在回滚中重新读取 `InputComponent`：第 105 帧的输入必须仍是第 105 帧录制的值，即使玩家此刻已经松键。

`InputComponent` 只在客户端采集真实设备输入；它在每个渲染帧更新的状态被量化为下一待模拟逻辑帧的 `BattleInputFrame`。服务器只接受网络输入帧，绝不读取 Unity 输入。

### 3.2 PlayerDataComponent

每个联机 Player 带 `PlayerDataComponent`，作为该 Player 的战斗配装、背包、技能链和数据版本的唯一来源；战斗系统不再读取 `SaveDataComponent` 或全局 `WorldSkillDataComponent`。

为保证可复制、可同步和可回放：

- `PlayerDataComponent` 保存版本和固定标量；装备槽、背包、技能链等可变集合使用明确的 `DynamicBuffer` 元素。
- 客户端加入战斗时由本地存档构造 `C2B_SendUserData`；服务器验证、复制为权威 PlayerData，之后把权威版本同步给客户端。
- 修改配装、背包或技能链是服务器权威请求，必须指定从哪个服务器帧生效。第一阶段不预测此类修改。
- 输入和 PlayerData 的 Source 必须绑定到 `UnitSourceBindingContext.Entity` 对应的 Player，不再绑定全局 `WorldInputComponent` / `WorldSkillDataComponent`。

`WorldStateSystem` 仍可在单机 World 中使用；它不能参与 Online Battle World 的 Player 模拟。

## 4. 本地 Player 预测与回滚

### 4.1 可独立重演的含义

一帧不是只凭 `Input[F]` 从零计算。速度、冷却、施法阶段等继承自上一帧。

可回滚的定义是：给定“第 `F - 1` 帧末快照 + `Input[F]` + 同一只读地图/配置”，能确定地重新得到第 `F` 帧末状态。

### 4.2 第一阶段的预测范围

```text
预测：移动方向、加速度、速度、位置、朝向、与静态地图的碰撞。
服务器权威：血蓝、伤害、Buff、控制、技能命中、怪物行为、掉落、交互、死亡。
```

控制和 Buff 不在客户端自行倒计时或结算；服务器同步其最终影响，例如移动锁、最终位置、动画状态。日后若某一控制效果必须立即影响本地移动，再将该效果的全部可快照状态纳入 Player 预测范围。

### 4.3 运动学移动约束

联机 Player 移动使用同一份 `BattlePlayerMoveSystem`：

```text
输入 -> 目标速度/加速度 -> 静态地图 ColliderCast -> 最终 Position
```

它不依赖 `PhysicsVelocity` 被 Unity Physics 在未来某个固定组积分，也不预测玩家推挤、怪物推挤、动态箱子或投射物刚体碰撞。服务器与客户端都查询相同种子生成的静态地牢碰撞体。

这意味着 Online Battle Player 不再使用当前 `UnitMoveSystem` 的动态物理路径；该系统仍可保留给单机单位。客户端远端实体必须不参与预测 Physics World，避免其本地碰撞污染本地 Player 轨迹。

Unity Physics 浮点误差仍可能存在，因此服务器校正是设计的一部分，不能依赖“绝对零误差”。

### 4.4 快照、校验与重放

客户端为本地 Player 保留有限窗口的帧末 `PredictionSnapshot`。第一阶段快照至少包含：

```text
LocalTransform
PlayerBattleInputComponent
移动状态（速度、方向、加速度相关状态）
UnitFacingComponent
所有预测 System 读写的冷却、动作、控制数据
```

服务器给本地拥有者的每份权威状态必须带：

```text
ServerFrame
LastAcceptedInputFrame
该 Player 在 ServerFrame 帧末的完整预测范围状态
```

不能只发送“变化的一个字段”给回滚器；回滚恢复点需要该帧完整可恢复的 Player 权威状态。可为远端节省带宽发送 delta，但本地拥有者的校正数据必须可重建完整状态。

客户端帧号与服务器帧号的对应关系由 `B2C_StartFrame`、服务器状态帧和 `LastAcceptedInputFrame` 维护；不能假定“本地刚模拟的第 100 帧”就是服务器此刻第 100 帧。客户端可以领先少量未来输入帧，但恢复/比较只针对带明确 `ServerFrame` 的权威基准。

处理权威第 `F` 帧的规则：

```text
不存在本地 F 快照：保存为基准，等待后续帧；不倒退到未知历史。
快照在位置、速度、朝向、动作状态的容差内一致：确认 F，清理更早历史。
不一致：恢复服务器 F 帧末状态，重放 F + 1 到当前预测帧的输入，重建快照。
```

浮点坐标/速度比较使用协议定义的量化值或明确容差，不能直接用任意 float 的逐位相等。

回滚只能修改逻辑组件；音效、VFX、UI、相机不可在重放时重复触发。预测表现事件必须可撤销或标记为本地临时效果，权威事件则按 EventId 去重消费。

### 4.5 状态图的限制

当前 `UnitStateScriptComponent` 持有可变的 managed `List<StateScriptRuntime>`。该内部运行状态无法通过普通 `IComponentData` 快照恢复。

因此它不能直接进入第一阶段的本地预测组。后续只有两种合法路线：

1. 把 Player 所需状态机重写为纯 ECS 状态组件和 System；或
2. 为每种 StateScriptRuntime/节点状态实现完整的 `CaptureState` / `RestoreState`，且捕获所有可变状态。

在完成其一以前，联机 Player 的移动/动作状态由新的 Player ECS 系统处理，不调用现有 `StateScriptSystem`。

## 5. 远端单位的客户端表现

远端实体没有 `BattleSimulateTag`，不运行任一现有战斗计算系统。客户端不外推；没有后一份快照时保持最后可用显示状态。

### 5.1 状态与事件分离

```text
状态帧（可覆盖、可重发）：
位置、朝向、动画状态、血蓝、可见 Buff、当前投射物状态、宝箱/门/出口状态。

事件帧（只消费一次）：
技能开始、命中、投射物出生/销毁、VFX、音效、伤害数字、死亡、掉落、交互结果。
```

每个事件必须带 `EventId` 与服务器帧。客户端保留已消费 EventId 的窗口，避免补包、重连或重复状态帧造成重复爆炸、音效和伤害数字。

### 5.2 远端表现系统

```text
RemoteStateApplySystem
  网络包到达时创建/销毁实体，写入权威状态与两份 Transform 快照。

RemoteTransformInterpolationSystem
  每个渲染帧以 `renderFrame = latestServerFrame - interpolationDelayFrames`
  在该时刻前后的两份已知快照之间插值位置和朝向；不做外推。没有后一份快照时保持前一状态。

RemotePresentationEventSystem
  按 EventId 消费服务器一次性事件，生成纯表现投射物、特效、音效和反馈。

BattlePresentationSystemGroup
  每个渲染帧运行动画、排序、特效跟随、VFX 寿命和本地迷雾显示。
```

现有 `UnitAnimationSystem`、`UnitSpriteRendererSortingSystem`、`EffectVisualFollowSystem`、`SpriteEffectAnimationSystem`、`VfxLifetimeSystem` 可以归入 `BattlePresentationSystemGroup`。它们不能继续在会被补帧/回滚多次更新的逻辑 SystemGroup 中运行。

远端不运行 `UnitMoveSystem`、`UnitControlSystem`、`UnitBuffSystem`、`UnitRecoverySystem`、`UnitPerceptionSystem`、`BehaviorTreeSystem`、`StateScriptSystem`、技能执行系统、掉落/死亡/交互逻辑。

当前 `UnitAnimationSystem` 通过 `UnitStateScriptComponent.UnitDataId` 查询动画 Profile。远端不初始化状态图后，需要增加纯展示数据来源（例如 `UnitPresentationDataComponent.UnitDataId`），让动画系统不依赖远端 StateScriptRuntime。

`DungeonFogOfWarSystem` 是本地显示逻辑，仍在客户端运行，但必须只使用本地 Player 作为开图来源，不能从 Player 阵营中任取第一个实体。

## 6. 战斗开始至帧同步的完整流程

```text
Lobby 房主 Start
  -> Lobby 将房间数据、楼层、种子、玩家 accountId 交给 Battle
  -> Battle 创建 BattleRoom 与 Battle World，生成同种子静态地图/碰撞
  -> Battle 为每位 Player 创建短时 ticket，Lobby 转发 L2C_StartTicket

客户端连接 Battle
  -> C2B_EnterBattle(ticket)
  <- B2C_EnterBattleResult(battleId、楼层、种子)
  -> 客户端切换战斗场景并按种子生成静态地图
  -> C2B_SendUserData(CharacterData，客户端数据版本)

Battle 收到 PlayerData
  -> 验证并创建实际 PlayerDungeon ECS 实体，而非空 Entity
  -> Player 网络 Id 使用 accountId；加 NetworkIdentity、OnlineBattlePlayerTag、PlayerData 等组件
  <- 向已初始化客户端发送 B2C_CreateBattleUnit

客户端收到每个单位
  -> 创建本地/远端对应实体，绑定网络 Id
  -> 本地 Player 添加 BattlePredictedTag；其他单位添加 BattleRemoteTag
  -> 全部所需单位与静态地图准备完成后发送 C2B_BattleReady

Battle 收到全部 C2B_BattleReady
  -> 校验地图/配置版本，初始化每个 Player 的权威状态
  <- B2C_StartFrame(startFrame、fixedDeltaTime、inputLeadFrames、初始完整状态)
  -> 服务器和客户端从同一逻辑帧开始
```

`C2B_BattleReady` 与 `B2C_StartFrame` 尚未存在；它们是避免一部分客户端已开始模拟、另一部分仍在加载/创建实体的必要握手。

## 7. 当前代码复核与缺口

下列问题意味着当前代码尚不能跑通本文方案，需按实施顺序补齐：

| 范围 | 当前状态 | 必须补齐 |
| --- | --- | --- |
| Battle World | `ClientBattleManager.battleWorld` 未实际用于创建单位；当前创建使用 `World.DefaultGameObjectInjectionWorld` | 客户端、服务器各自明确持有并使用 Battle World，销毁时完整释放 |
| Player 实体 | `ServerBattleManager.OnGetUserData` 创建空 Entity，仅加 `NetworkIdentityComponent` | 用 `EntitySpawnRegistryUtility.TryInstantiateUnit(..., "PlayerDungeon", ...)` 创建实际实体，再配置联机组件 |
| 身份 | 当前 `NetworkIdentityComponent.id`、`BattleUnitData.unitId` 使用 `Guid` | Player 改以 `ulong accountId` 标识；非 Player 采用 Battle Server 分配的 `ulong battleEntityId` |
| 本地/远端区分 | 当前仅有 `NetworkPlayerComponent` | 增加本文 Tag，并让 System Query 以 Tag 筛选 |
| 帧驱动 | `ClientBattleManager.OnFixedUpdate` 未接入 Timer；`FixedUpdateManual`、`RollBack`、`CheckConsistency` 为空 | NetworkTimer 驱动固定帧、输入/快照历史、状态接收、校验、恢复和重放 |
| 网络状态协议 | `NetworkStateData` 只有 `unitId`、`frame`、`hasChecked`；`NetworkEventDictionary.Handle` 为空 | 定义 InputFrame、拥有者权威 PlayerState、远端 StateSnapshot、EventFrame 与序列化/注册/Handler |
| 帧接纳与时钟 | 当前没有 input lead、输入确认或起帧时钟规则 | 服务端未来帧接纳、回传 `LastAcceptedInputFrame`；StartFrame 必须为未来帧，远端按延迟帧插值 |
| 战斗起帧 | 当前所有 `C2B_SendUserData` 收齐后仅设置 `room.started = true` | 补 C2B_BattleReady、B2C_StartFrame、初始完整状态与服务器 Timer 启动 |
| 静态地图一致性 | 仅传递 seed，尚未保证 Battle World 的地图与碰撞构建完成 | 服务器和客户端按 seed 在起帧前构建相同静态碰撞；失败不得 StartFrame |
| 数据来源 | `WorldStateSystem` 仍读取 `InputComponent`、`SaveDataComponent`、`WorldSkillDataComponent` | Online Battle Player 改读 PlayerBattleInput / PlayerData；单机路径保留 |
| 状态图 | 运行时为 managed `StateScriptRuntime` | 第一阶段将联机 Player 排除；后续再做纯 ECS 状态机或可快照 Runtime |
| 表现组 | 动画、VFX 仍在逻辑 `UnitExecutionSystemGroup` | 拆入渲染帧 `BattlePresentationSystemGroup`；回滚不更新它们 |
| 物理 | 当前 Player 带非运动学 `PhysicsMassOverride`，`UnitMoveSystem` 写 `PhysicsVelocity` | Online Battle Player 使用静态碰撞查询的运动学移动；远端不进入预测物理 |

## 8. 可行性结论与实施顺序

方案可以跑通，但前提是先完成“联机 Player 与当前单机通用逻辑隔离”。不能先在旧 `UnitMoveSystem` 或 `StateScriptSystem` 上叠加回滚。

推荐拆为七个可独立验收的实施阶段：

### 阶段 1：战斗入口与起帧握手

修改 `ServerBattleManager`、`ClientBattleManager`、`BattleRoom` 和 Battle 协议。

```text
Player 使用 ulong accountId 作为身份
Battle 创建实际 Battle World 和 PlayerDungeon 实体
客户端在自己的 Battle World 创建实体，不再使用 Default World
补 C2B_BattleReady / B2C_StartFrame
服务器等待所有客户端、地图和静态碰撞准备完成后才起帧
```

验收：四个客户端都能进入相同 seed 的空战斗地图，所有客户端都已生成四个 Player，且收到同一 `StartFrame`。本阶段不要求玩家移动。

### 阶段 2：联机实体身份与 System 隔离

新增 Battle Tag、`PlayerBattleInputComponent`、`PlayerDataComponent` 及其必要 Buffer；改造现有 System Query。

```text
服务器：所有战斗实体 = BattleSimulateTag + BattleAuthorityTag
客户端本地 Player = BattleSimulateTag + BattlePredictedTag
客户端远端实体 = BattleRemoteTag
旧 UnitMove / StateScript / Skill 等系统明确排除 OnlineBattlePlayerTag
```

验收：在客户端调试 Query 中，只有本地 Player 会命中未来的 Player 模拟组；远端 Player 和怪物不会命中。单机 World 的现有行为不变。

### 阶段 3：固定帧时钟与协议骨架

新增 `BattleInputFrame`、`InputAck`、拥有者权威 PlayerState、远端 StateSnapshot 和 EventFrame；由 `NetworkTimer` 驱动服务器及客户端的逻辑帧。

```text
客户端 InputComponent -> BattleInputFrame -> C2B
服务器 FrameAnalyse -> acceptedFrame -> InputAck
服务器按固定帧收集 PlayerState / EventFrame -> B2C
```

验收：服务器可以记录四名玩家每个帧的输入，客户端能看到自己的输入被确认到哪一个服务器帧。此阶段允许尚未用输入驱动移动。

### 阶段 4：共享的服务器 Player 移动模拟

新增 `BattlePlayerSimulationSystemGroup`、输入应用和运动学移动 System。服务器和客户端引用同一组 System 类型；服务器先实际运行它，客户端暂时只接收结果。

```text
输入 -> PlayerBattleInputComponent
-> 加速度/速度/朝向
-> 静态地图 ColliderCast
-> LocalTransform
```

验收：服务器的四个 Player 都能按各自输入移动、撞墙、停止；同一输入在独立服务器/客户端 World 上重演 N 帧后结果处于量化容差内。

### 阶段 5：远端状态与纯表现

实现 `RemoteStateApplySystem`、`RemoteTransformInterpolationSystem`、`RemotePresentationEventSystem` 和 `BattlePresentationSystemGroup`；移动动画、血条、特效跟随与排序改在渲染帧运行。

验收：A 本地移动时，B 只能通过服务器快照平滑看到 A；B 不运行 A 的移动、AI、伤害或 Physics。断线/重包不重复播放事件。

### 阶段 6：本地移动预测、校验与回滚

客户端开始运行阶段 4 的同一 Player 模拟组，但仅作用于本地 Player；实现输入历史、帧末快照、权威状态比较、恢复和重放。

```text
预测 F
-> 保存 Snapshot[F]
-> 收到服务器 F
-> 一致则确认；不一致则恢复 F 并重放 F + 1..当前帧
```

验收：本地移动无需等待服务器回包；故意篡改本地位置/速度后，客户端能校正并继续响应后续已记录输入。回滚期间动画、音效和 VFX 不加速或重复。

### 阶段 7：逐项扩大战斗范围

按一个能力一个闭环的方式加入 PlayerData 变更、控制、施法阶段、技能、投射物和交互。每一项都必须同时定义输入、预测状态、服务器权威状态、快照、校验、回滚及表现事件；无法满足时保持服务器权威、客户端仅表现。

验收：每项能力在网络延迟、输入改帧、重连补包下都通过第 9 节验证基线后，才加入下一项。

非目标：阶段 1 至 6 不实现完整状态图回滚、远端单位预测、动态刚体推挤预测、伤害预测或 P2P 全世界回滚。

在第 5 步完成之前，服务器仍然是唯一会造成伤害、死亡、Buff 和世界变化的一端；客户端预测只改善本地移动手感，不影响权威结算。

## 9. 验证基线

每扩大一次预测范围，都先通过对应验证，不能只观察“看起来能移动”。

1. **同输入一致性：** 在同一张静态地图、同一固定帧长下，服务器与客户端对连续 N 帧相同 `BattleInputFrame` 的移动结果，在协议量化精度内一致。
2. **权威校正：** 人为修改客户端第 F 帧的位置或速度；收到服务器第 F 帧状态后，客户端必须恢复并重演，且第 `F + 1` 之后仍响应已保存输入。
3. **输入改帧：** 人为让服务器将一条输入排到更晚帧；客户端收到 `InputAck` 后必须重排历史，不得在旧帧和新帧各执行一次。
4. **双客户端：** A 的本地 Player 预测移动，B 只通过状态快照插值看到 A；B 的世界中 A 不命中任何战斗模拟 Query。
5. **事件去重：** 重发同一服务器 EventFrame 或在重连后补发，音效、VFX、伤害数字、死亡和掉落各只消费一次。
6. **起帧安全：** 任意一个客户端未 `BattleReady` 时，服务器不得启动逻辑帧；地图/碰撞构建失败时必须拒绝 Ready，而不是开始后校正。
