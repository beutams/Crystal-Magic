# 统一技能输入设计

## 目标

所有主动技能只接收同一种释放数据：一个世界坐标和一个可选的目标实体。
技能执行层不解释技能输入类型；玩家 State Script 根据技能数据中配置的类型
组装释放数据，怪物 State Script 则直接填写释放数据。

## 技能数据

在 `SkillData.cs` 中增加 `SkillInputType`：

```csharp
public enum SkillInputType
{
    None = 0,
    Self = 1,
    MousePosition = 2,
}
```

`SkillData` 增加 `InputType` 字段，默认值为 `None`。

- `None` 是怪物技能的常规值，不触发任何自动输入映射。
- `Self` 与 `MousePosition` 只由玩家的 State Script 解释。
- 不包含 `SelectedTarget` 或朝向类输入类型。

`WorldSkillInfo` 同步保存 `SkillData.InputType`，让 State Script 可以通过
Source 读取类型，而无需直接读取数据表。

## 统一释放数据

`SkillReleaseRequest` 仍然是唯一的技能释放请求对象。它已有的目标坐标和目标
实体字段成为所有主动技能的统一输入：

- `TargetPosition` 必须由 `RequestSkill` 提交。
- 无实体目标时，`TargetEntity` 提交为 `Entity.Null`。
- 现有的 `HasTargetPosition`、`HasTargetEntity` 保留为内部兼容字段；新的请求
  一律设置 `HasTargetPosition`，只有目标实体不是 `Entity.Null` 时才设置
  `HasTargetEntity`。

新增共用的 `SkillRequestInputData`，其中包含 `Position`（`Float3`）与
`TargetEntity`（`Entity`）表达式。

`RequestSkillActionNodeData` 包含 `SkillId` 与该输入数据。
`RequestSkillWithAdditionActionNodeData` 也使用同一份输入数据，不能再创建
隐式目标的请求。节点检查器显示以下三个字段：

```text
Skill ID
Position
Target Entity
```

`TargetEntity` 默认是 `Entity.Null`；`Position` 必须始终填写。

两个请求节点都同时计算三个表达式，再调用新的显式
`SkillReleaseRequestUtility.Create` 重载，传入位置与目标实体。它们不再读取
`skill.targetPosition` 与 `skill.targetEntity` 单位变量。

所有其他请求发起方也必须使用显式重载。`ReplayCurrentSkillAdditionAction`
没有父技能 `SkillContent`，因此重放时显式提交当前释放单位的
`LocalTransform.Position` 与 `Entity.Null`；它绝不读取隐式目标变量。

## 技能执行

以一个统一的主动技能执行器取代输入专用的 `SelfSkill` 与 `PositionSkill` 行为。
该执行器把请求数据直接复制到 `SkillContent` 后执行效果链：

- `SkillContent.Position` 始终使用提交的位置。
- 只有请求目标不是 `Entity.Null` 时，`SkillContent.TargetEntity` 才是有效目标。
- `OriginEntity` 与原点位置仍然用于标识释放者。

现有 `RuntimeType` 与技能注册表不再决定释放是自身技能还是位置技能。迁移时将
所有主动技能数据改为统一执行器，再删除不再使用的输入专用运行时类和注册项。

`SpawnProjectileEffect` 只根据 `context.Position - spawnPosition` 计算方向。
错误请求不能再静默回退为向右发射。

## State Script Source

增加以下访问器：

- `world.skill.getInputType(SkillId)`：返回任意已加载技能的 `SkillInputType`
  数值。
- `player.skill.currentInputType`：通过当前单位自己的
  `PlayerCurrentSkillComponent` 找到当前技能，再返回其输入类型。它不读取全局
  当前技能链，因此咏唱期间切换全局栏位不会改变该单位稳定的链与槽位选择。
- `unit.self.entity`：返回当前 State Script 所属单位实体，使自身技能能显式提交
  自身作为 `TargetEntity`。

## 玩家与怪物图

玩家 State Script 在当前 `RequestSkill` 前增加一次类型判断。所有分支都调用
同一种请求节点：

| `player.skill.currentInputType` | `Position` 表达式 | `Target Entity` 表达式 |
| --- | --- | --- |
| `Self` | `unit.transform.position` | `unit.self.entity` |
| `MousePosition` | `world.input.pointerWorldPosition` | `Entity.Null` |

玩家技能若为 `None`，不会进入这些自动分支。当前玩家技能数据应配置为 `Self` 或
`MousePosition`。

怪物图不读取 `InputType`。每个怪物 `RequestSkill` 直接填写所需的位置与可选
目标实体。例如向前发射投射物时，SS 先计算怪物前方的坐标，再把该坐标传入。

## 错误处理与迁移

- `RequestSkill` 绑定时，位置表达式不是 `Float3` 或目标表达式不是 `Entity`
  会直接失败并显示错误。
- 运行时出现无效位置请求时不执行，并记录技能 ID 与来源节点；不能再向右发射。
- 旧的 `RequestSkill` 与 `RequestSkillWithAddition` 节点迁移为“原点位置 + 空
  目标”。之后再把玩家图改为 `Self` 与 `MousePosition` 分支。
- 旧 `SkillData` 没有序列化 `InputType` 时自动读取为 `None`。

## 验证

1. 编译两个 C# 程序集。
2. 运行时验证 `MousePosition` 火球向鼠标移动，且图片随轨迹旋转。
3. 验证 `Self` 技能的位置与目标实体都是释放者。
4. 验证怪物可以直接提交任意位置与可选目标实体，无需查询 `InputType`。
5. 验证不完整请求不会再默认生成一个向右飞行的投射物。
