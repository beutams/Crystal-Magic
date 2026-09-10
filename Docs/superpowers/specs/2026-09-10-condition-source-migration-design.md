# 条件 Source 全量迁移设计

## 目标

移除旧的标量 `ISource` 条件体系，使所有条件只通过新版的
`ConditionConfig.Inputs`、`ValueExpression` 和 `UnitSource` 求值。行为树、
State Script、技能附加、技能效果、范围搜索、投射物碰撞，以及对应的技能、
Buff、道具与效果图编辑器，全部使用同一套类型化 Source 机制。

迁移完成后，`ConditionConfig` 不再包含 `SourceType`、`SourceParam` 或
`CompareValue`；`ComparatorFactory` 不再注册或构造 `ISource`，也不再提供旧的
实体/EntityManager 条件构造重载。

## 非目标

- 不改变比较器的 `ConditionType`、比较算子或值运算定义。
- 不为本次迁移添加地形碰撞、阵营层、技能冷却或新的目标选择机制。
- 不重写 State Script、行为树或效果图的控制流；它们仅切换到统一条件模型。
- 不保留运行时兼容分支或旧字段的反序列化回退。旧条件数据必须在仓库中迁移。

## 现状与迁移范围

State Script、行为树和 `SkillAdditionEventDispatcher` 已通过
`UnitSourceAccessTable : IComparatorValueResolver` 构造新版 Comparator。其余旧链路
仍包括：

- `SourceContext`、`ISource`、`ComparatorFactory.RegisterSource` 与旧的
  `BuildComparator(List<ConditionConfig>, Entity, EntityManager, ...)`；
- 五个旧实现：`UnitBuffStackSource`、`UnitHealthRatioSource`、
  `UnitIsControlledSource`、`UnitIsCastingSource`、`UnitIsEnemySource`；
- `SkillExecutor` 以及圆形、扇形、链式、前向矩形搜索效果的旧条件调用；
- Skill、Buff、Prop 和 Effect Graph 的旧条件编辑表单；
- `SkillDataTable.json` 中火球的两条旧敌对条件，以及
  `StateScriptDataTable.json` 中遗留的空旧字段。

## 统一条件模型

`ConditionConfig` 的唯一条件输入为：

```text
ConditionType + CompareType + Inputs[]
```

每个输入是一个 `ValueExpression`，可为类型化字面量、`GetterKey` Source，或值运算。
`ComparatorFactory.BuildComparator(IReadOnlyList<ConditionConfig>,
IComparatorValueResolver)` 是唯一的运行时条件构造入口。无效 Source、错误参数数量
或类型不匹配必须使该条件构造失败，调用方将整个条件组视为不通过并记录既有错误日志。

### 旧 Source 的替代关系

| 删除的旧 Source | 新版表达式 |
| --- | --- |
| `UnitBuffStackSource` | `unit.buffs.stackCount(BuffId)` |
| `UnitHealthRatioSource` | `unit.vitality.currentHealthPercentage` |
| `UnitIsControlledSource` | `unit.control.hasControl` |
| `UnitIsCastingSource` | `unit.variables.getBool("casting")` |
| `UnitIsEnemySource` | `unit.faction.isEnemyTo(effect.context.originEntity)` |

其中前三项已经由现有 UnitSource 提供；不再为旧的“待释放请求数量”语义新增
`unit.skill` Source。State Script 的 `casting` 变量才是当前施法状态的权威来源。

## 效果条件上下文

技能效果的条件不能只使用某个单位的常驻 `UnitSourceAccessTable`：同一单位可在同一帧
作为不同技能实例的目标，施法者等上下文必须随 `SkillContent` 传递，不能写入常驻表。

新增一个仅供效果条件使用的 Resolver。它包装“被判断单位”的
`UnitSourceRuntimeComponent.Table`，并在该表之上提供下列只读、动态 Source：

| Key | 类型 | 无对应上下文时 |
| --- | --- | --- |
| `effect.context.originEntity` | `Entity` | 返回 `None` |
| `effect.context.targetEntity` | `Entity` | 返回 `None` |
| `effect.context.otherEntity` | `Entity` | 返回 `None` |
| `effect.context.position` | `Float3` | 返回 `None` |
| `effect.context.triggerValue` | `Number` | 返回 `0` |

名称可在实现时集中为常量，但 JSON 中必须使用以上稳定键。Resolver 先查询动态键，
再委托到被判断单位的 Table。被判断单位不存在、没有 Source Table，或条件要求但没有
对应上下文时，条件不通过；不得退回旧 Source。

### 阵营 Source

`UnitFactionSource` 新增参数化 getter：

```text
unit.faction.isEnemyTo(OtherEntity: Entity) -> Bool
```

它以当前绑定单位为左侧、参数实体为右侧，调用既有 `UnitFactionUtility.IsEnemy`。
两个实体任一不存在或没有阵营组件时返回 `false`。因此，目标单位上的火球筛选写为：

```text
IsTrue(
  unit.faction.isEnemyTo(
    effect.context.originEntity
  )
)
```

该形式保留“相对于施法者”的敌我语义，不会把 Player、Friend、Enemy、Boss 的枚举值
简单相等/不等比较误当作阵营关系。

## 运行时求值

新增技能条件工具，负责从 `SkillContent` 与要判断的 Entity 创建上述 Resolver，并调用
唯一的 ComparatorFactory 新入口。

- `SkillExecutor` 的顶层效果条件仍以 `TargetEntity` 优先、否则 `OriginEntity` 为
  被判断对象；仅替换其构造 Comparator 的方式。
- Area、Cone、Chain、ForwardRect 搜索效果继续以每个候选单位为被判断对象；
  `TargetConditions` 用同一个工具求值。
- `SkillProjectileSystem` 使用同一个工具处理新的投射物“碰撞目标条件”。条件不通过
  的候选单位被跳过，系统继续选择最近的合格单位；空列表保持现有的非自身命中行为。
- 已使用 `runtime.Sources` 的行为树、State Script、技能附加不改变运行时调用方式。

该工具不缓存跨技能实例的动态 Resolver；条件可在 Effect 执行时安全读取当前上下文。

## 数据与投射物

`SpawnProjectileEffectData` 增加 `CollisionTargetConditions`（`List<ConditionConfig>`），
并在运行时副本、投射物 Spawn Request 与 Payload 中逐层传递。它与
`OnCollisionEffects`、`OnDestroyEffects` 独立：先筛选碰撞候选，再执行现有碰撞/销毁链。

火球数据迁移为三处同语义的新版敌对条件：

1. 投射物 `CollisionTargetConditions`；
2. 爆炸 Area Search 的 `TargetConditions`；
3. 爆炸 Damage 的 `Conditions`。

迁移不会更改用户已经修正的燃烧 Buff 配置。

## 编辑器

新增共享的编辑器条件列表控件，内部复用 `StateScriptValueExpressionDrawer` 的新版
表达式绘制能力，不得继续显示旧 Source 下拉、`Source Param` 或阈值专用控件。

效果条件编辑器 Schema 由全部可用 UnitSource 加上上文的 `effect.context.*` 键组成。
它用于技能、技能附加、Buff 与道具的效果条件；State Script 与行为树仍使用各自按预制体
限制的 Schema。

下列编辑面全部接入共享控件：

- `SkillEditorWindow`：技能自身、所有效果条件、`TargetConditions` 与新的
  `CollisionTargetConditions`；
- `EffectGraphInspector`：效果自身条件，以及所有 `List<ConditionConfig>` 字段，
  因而范围目标条件可见且可编辑；
- `BuffEditorWindow` 与 `PropEditorWindow`：所有效果条件和搜索目标条件。

编辑器新建条件时根据所选比较器参数生成类型匹配的 `Inputs`；保存后不得生成旧字段。

## 删除与数据清理

移除 `ConditionConfig` 的三个旧序列化字段，以及 `ISource`、`SourceContext`、旧
Source Factory 注册/生成逻辑、五个旧 Source 实现和所有旧编辑器状态字段。保留
`UnitValue`、`ValueExpression`、`IParameterizedUnitValueGetter`、
`IComparatorValueResolver` 等同文件中的新版基础类型。

清理 `SkillDataTable.json` 与 `StateScriptDataTable.json` 的旧字段。状态图原本已经有
新版 `Inputs`，所以只做字段删除，不改条件语义。迁移结束后，仓库中不得存在
`SourceType`、`SourceParam`、`CompareValue`、`ISource`、`SourceContext` 或
`RegisterSource` 的有效引用。

## 验证

1. 编译 `Crystal-Magic.sln`，确认旧接口删除后无残留引用。
2. 递归搜索上述旧字段/类型，排除设计文档中的历史说明后必须为零。
3. 反序列化并检查两张被迁移 JSON 表：火球三个条件均有 `Inputs`，State Script
   的条件语义不变且无旧字段。
4. 在 Skill、Buff、Prop 编辑器和 Effect Graph 中创建/编辑一个新版条件，确认可选
   Source、参数与嵌套表达式均正确保存。
5. 运行时验证：火球略过友方、命中最近敌方、爆炸仅作用敌方；没有合格碰撞单位时仍按
   原配置在最大距离处消失。

## 实施边界

实现会避开工作区已有的无关改动。尤其是当前已修改的 `SkillProjectileSystem.cs` 只会在
候选命中筛选的位置作最小合并；不会覆盖其余未提交内容。
