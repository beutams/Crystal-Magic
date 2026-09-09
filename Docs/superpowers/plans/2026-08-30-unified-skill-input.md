# Unified Skill Input Implementation Plan（统一技能输入实施计划）

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**目标：** 主动技能统一提交位置与目标实体；玩家 State Script 根据技能输入类型提交自身或鼠标数据，怪物 State Script 直接提交数据。

**架构：** `SkillInputType` 仅作为数据表字段和 State Script Source 输出，不参与技能执行分派。`RequestSkill` 与 `RequestSkillWithAddition` 显式生成位置和实体请求；统一 `CommonSkill` 将请求直接复制到 `SkillContent` 后执行效果链。

**技术栈：** Unity Entities 1.0、C#、Newtonsoft JSON 数据表、State Script、自动生成 Registry。

**设计：** `Docs/superpowers/specs/2026-08-30-unified-skill-input-design.md`

## 全局约束

- `SkillInputType` 仅有 `None`、`Self`、`MousePosition`；怪物技能保持 `None`。
- 新的主动技能请求必须显式提供 `Float3 Position` 与 `Entity TargetEntity`；无实体目标使用 `Entity.Null`。
- `InputType` 只能通过 Source 供 SS 判断，技能执行层不得依据它选择行为。
- 旧 `skill.targetPosition` 与 `skill.targetEntity` 单位变量不得再作为主动技能请求输入。
- 生成的 Registry 必须与 `[FactoryKey]` 和 Unit Source 定义一致。
- 每个阶段结束后串行运行 `dotnet build Assembly-CSharp.csproj -nologo` 与 `dotnet build Assembly-CSharp-Editor.csproj -nologo`。

---

### 任务 1：技能输入类型及可查询 Source

**文件：**
- 修改：`Assets/Scripts/Game/Data/SkillData.cs`
- 修改：`Assets/Scripts/Game/World/WorldSkillDataComponent.cs`
- 修改：`Assets/Scripts/Game/Unit/Component/PlayerCurrentSkillAuthoring.cs`
- 修改：`Assets/Scripts/Game/Unit/Component/UnitSkillReleaseAuthoring.cs`
- 修改：`Assets/Scripts/Game/Unit/Source/UnitComponentSourceRegistry.cs`

**接口：**
- 产出：`SkillInputType`，`SkillData.InputType`，`WorldSkillInfo.InputType`。
- 产出：`world.skill.getInputType(SkillId)` 与 `player.skill.currentInputType`，返回 `UnitValueCategory.Number`。
- 产出：`unit.self.entity`，返回 `UnitValueCategory.Entity`。

- [ ] **步骤 1：定义数据枚举和世界技能镜像**

```csharp
public enum SkillInputType
{
    None = 0,
    Self = 1,
    MousePosition = 2,
}

public SkillInputType InputType;
```

在 `WorldSkillInfo(SkillData data)` 中复制 `data.InputType`。

- [ ] **步骤 2：发布技能输入类型 Source**

在 `WorldSkillSource` 为已有 `Skill ID` 参数定义新增：

```csharp
world.skill.getInputType(SkillId) -> UnitValue.FromInt((int)skill.InputType)
```

在 `PlayerCurrentSkillUtility` 添加 `TryGetCurrentInputType`；它通过玩家自身稳定的当前槽位解析技能 ID，再从 `WorldSkillDataComponent` 读取类型。`PlayerCurrentSkillSource` 发布无参 `player.skill.currentInputType`。

- [ ] **步骤 3：发布当前单位实体 Source**

在 `UnitSkillReleaseAuthoring.cs` 中增加与 `UnitSkillReleaseComponent` 对应的 Source，发布：

```csharp
unit.self.entity -> UnitValue.FromEntity(context.Entity)
```

在生成的 `UnitComponentSourceRegistry` 中注册该 Source；确保只有带 `UnitSkillReleaseAuthoring` 的单位图可使用它。

- [ ] **步骤 4：编译验证 Source 阶段**

依次运行：

```powershell
dotnet build Assembly-CSharp.csproj -nologo
dotnet build Assembly-CSharp-Editor.csproj -nologo
```

预期：两个程序集均为 0 error。

### 任务 2：显式统一请求输入

**文件：**
- 修改：`Assets/Scripts/Game/Unit/Component/UnitSkillReleaseAuthoring.cs`
- 修改：`Assets/Scripts/Game/Skill/SkillReleaseRequestUtility.cs`
- 修改：`Assets/Scripts/Game/Data/StateScript/StateScriptData.cs`
- 修改：`Assets/Scripts/Game/Unit/StateScript/Nodes/RequestSkillActionNode.cs`
- 修改：`Assets/Scripts/Game/Unit/StateScript/Nodes/RequestSkillWithAdditionActionNode.cs`
- 修改：`Assets/Scripts/Game/Unit/Editor/StateScriptNodeInspector.cs`
- 修改：`Assets/Scripts/Game/Skill/SkillAdditionAction.cs`

**接口：**
- 消费：任务 1 的 `Float3`、`Entity` Source。
- 产出：`SkillRequestInputData` 与 `SkillReleaseRequestUtility.Create(EntityManager, Entity, int, SkillModifierSet, float3, Entity)`。

- [ ] **步骤 1：为两个请求节点定义共享输入数据**

在 `StateScriptData.cs` 增加：

```csharp
[Serializable]
public sealed class SkillRequestInputData
{
    public ValueExpression Position = new() { Literal = UnitValue.FromFloat3(float3.zero) };
    public ValueExpression TargetEntity = new() { Literal = UnitValue.FromEntity(Entity.Null) };
}
```

将它加到 `RequestSkillActionNodeData` 与 `RequestSkillWithAdditionActionNodeData`，并在 `StateScriptInstanceData.EnsureValid` 中补齐旧数据的默认输入。

- [ ] **步骤 2：让请求工具只接受显式输入**

新增或替换工厂签名：

```csharp
public static SkillReleaseRequest Create(
    EntityManager entityManager,
    Entity entity,
    int skillId,
    SkillModifierSet extraModifiers,
    float3 targetPosition,
    Entity targetEntity)
```

该方法捕获原点与朝向，固定设置 `HasTargetPosition = true`，并用
`targetEntity != Entity.Null` 设置 `HasTargetEntity`。删除
`CaptureVariableTarget` 及对 `skill.targetPosition`、`skill.targetEntity` 的读取。

- [ ] **步骤 3：绑定并提交 RequestSkill 输入**

两个请求节点各自绑定 `SkillId`、`Input.Position`、`Input.TargetEntity` 表达式；分别验证 `Number`、`Float3`、`Entity` 分类。执行时把三个结果传给新的 `Create` 签名。节点检查器对两个节点显示相同的 `Position` 和 `Target Entity` 输入框。

- [ ] **步骤 4：迁移二次技能请求**

`ReplayCurrentSkillAdditionAction` 没有父技能 `SkillContent`，因此创建请求时
显式传入 `Context.Entity` 的 `LocalTransform.Position` 与 `Entity.Null`。它不再
读取单位变量，也不尝试从不存在的父上下文继承目标。

- [ ] **步骤 5：编译验证请求阶段**

依次运行两个 `dotnet build` 命令。预期：不存在旧 `Create` 调用、0 error。

### 任务 3：统一技能执行与投射物方向

**文件：**
- 创建：`Assets/Scripts/Game/Skill/Skills/CommonSkill.cs`
- 删除：`Assets/Scripts/Game/Skill/Skills/SelfSkill.cs`
- 删除：`Assets/Scripts/Game/Skill/Skills/PositionSkill.cs`
- 修改：`Assets/Scripts/Game/Skill/SkillRegistry.cs`
- 修改：`Assets/Scripts/Game/Skill/Effects/SpawnProjectileEffect.cs`
- 修改：`Assets/Scripts/Game/Skill/SkillChainResolver.cs`

**接口：**
- 消费：任务 2 填充的 `SkillReleaseRequest.TargetPosition` 与 `TargetEntity`。
- 产出：`CommonSkill` 是唯一主动技能运行时；`SkillContent` 统一携带显式位置与目标实体。

- [ ] **步骤 1：实现统一执行器**

```csharp
[FactoryKey(nameof(CommonSkill), 0, "Common Skill")]
public sealed class CommonSkill : Skill
{
    protected override bool BuildContext(in SkillReleaseRequest request, SkillContent context)
    {
        if (!request.HasTargetPosition)
            return false;

        SetPosition(context, true, request.TargetPosition);
        SetTargetEntity(context, request.HasTargetEntity, request.TargetEntity);
        return true;
    }
}
```

`SkillChainResolver.Resolve` 保持将 `RuntimeType` 快照到 `ResolvedSkillData`，但所有主动技能数据使用 `CommonSkill`；输入类型只由 SS 使用。

- [ ] **步骤 2：移除旧的输入专用执行器并同步 Registry**

删除 `SelfSkill` 与 `PositionSkill`，将生成的 `SkillRegistry` 改为只注册
`CommonSkill`，并确保 `DefaultSkillRuntimeTypeKey` 为 `CommonSkill`。

- [ ] **步骤 3：禁止投射物向右回退**

把 `SpawnProjectileEffect.GetProjectileDirection` 改为可失败的方向解析：
当 `context.HasPosition` 为假，或目标点与发射点距离接近 0 时，记录技能 ID
并直接返回，不调用 `SkillProjectileSpawnQueue.Enqueue`。保留
`SkillProjectileSpawnSystem.CreateRotation` 与视觉跟随旋转逻辑。

- [ ] **步骤 4：编译验证执行阶段**

依次运行两个 `dotnet build` 命令。预期：`CommonSkill` 可由工厂创建，旧运行时无引用，0 error。

### 任务 4：数据表和玩家 State Script 迁移

**文件：**
- 修改：`Assets/Res/Data/SkillDataTable.json`
- 修改：`Assets/Res/Data/StateScriptDataTable.json`

**接口：**
- 消费：任务 1 的 `player.skill.currentInputType`、`unit.self.entity` 与任务 2 的 RequestSkill 输入结构。
- 产出：火球使用 `MousePosition`；玩家图可按当前技能类型提交统一输入。

- [ ] **步骤 1：迁移技能数据**

将已有主动技能的 `RuntimeType` 改为 `CommonSkill`。将火球术的 `InputType` 设置为 `MousePosition`；未指定的怪物技能保持 `None`。保持 JSON UTF-8 BOM 和已有效果链内容不变。

- [ ] **步骤 2：迁移现有请求节点数据**

为每一个 `RequestSkill` 和 `RequestSkillWithAddition` 节点补入
`SkillRequestInputData`。非玩家节点使用其原点位置和 `Entity.Null` 作为明确的
默认值，避免旧变量残留。

- [ ] **步骤 3：重接玩家图分支**

在玩家技能链中、原 `RequestSkill` 之前加入对
`player.skill.currentInputType` 的两个 `Compare` 分支：

```text
Self == 1
  -> RequestSkill(player.skill.currentSkillId,
                  unit.transform.position,
                  unit.self.entity)

MousePosition == 2
  -> RequestSkill(player.skill.currentSkillId,
                  world.input.pointerWorldPosition,
                  Entity.Null)
```

两个请求节点保留原本后续输出连线；移除旧的单一路径，确保每次只能提交一次请求。

- [ ] **步骤 4：验证数据文件与编译**

检查两个 JSON 文件仍为 UTF-8 BOM，解析后没有丢失已有节点或效果。依次运行两个
`dotnet build` 命令，预期 0 error。

### 任务 5：运行时验收

**文件：**
- 修改：`Docs/superpowers/specs/2026-08-30-unified-skill-input-design.md`（仅在实现与设计不一致时同步修正）

**接口：**
- 消费：任务 1 至任务 4 的完整迁移结果。

- [ ] **步骤 1：静态检查**

运行：

```powershell
rg -n -S 'skill\.targetPosition|skill\.targetEntity' Assets/Scripts/Game
rg -n -S 'SelfSkill|PositionSkill' Assets/Scripts
```

预期：主动技能请求路径没有旧变量读取或旧输入专用执行器引用。

- [ ] **步骤 2：Play Mode 手工验证**

在玩家技能链装备火球，按住鼠标左键并分别指向右、上、左、下。每次火球应从角色
朝本次鼠标位置飞行，图片同步旋转；技能链循环再次开始时重新采样鼠标位置。

将一个技能配置为 `Self` 后释放。效果上下文应使用角色当前位置和角色实体，且不依赖鼠标位置。

为怪物图填写明确位置与 `Entity.Null`，验证可释放；填写目标实体时，验证该实体进入
`SkillContent.TargetEntity`。

- [ ] **步骤 3：最终编译与差异检查**

串行运行两个 `dotnet build` 命令，再运行：

```powershell
git diff --check -- Assets/Scripts/Game Assets/Res/Data Docs/superpowers
```

预期：两个程序集均为 0 error；本次修改范围内没有空白错误。
