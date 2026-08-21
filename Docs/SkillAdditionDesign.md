# Skill Addition, Buff, and Release Snapshot Design

## 1. Purpose and Boundaries

This document defines the target design for Skill Addition, Buff modifiers, State Script, and skill release.

The design has five fixed rules:

1. State Script ports transfer pulses only. Nodes do not pass a skill context through ports.
2. Unit property values are calculated from the current Buff list when read. They are not stored as a frame-wide aggregated cache.
3. Skill Addition modifiers are local to one player skill-chain submission. They never become unit Buff modifiers.
4. Unit Buff skill modifiers are persistent unit state and may affect every skill released by that unit.
5. The final immutable skill snapshot is created by `SkillReleaseSystem`, not by the State Script submit node.

The old `SkillCastTask` hook list, `SkillFollowupEffectData`, Followup runtime collection, Addition type enum, and central Addition behavior switch are outside the target design.

## 2. Modifier Layers

There are exactly two modifier inputs for one release. They are inputs to the same final `SkillModifierSet`; they are not two types of Addition.

```text
SubmittedAdditionModifiers
  Produced only by the player skill-chain State Script.
  Copied into one submitted release request.
  Applies only to that request.

PersistentUnitModifiers
  Built from the caster's current active Buffs at release time.
  May apply to every skill released by that caster.
```

The final release calculation is always:

```text
FinalModifiers = BuildPersistentSkillModifiers(caster)
FinalModifiers.Add(request.SubmittedAdditionModifiers)
```

This solves the scope problem:

```text
Player skill-chain request carrying Addition A
  -> receives Addition A's SubmittedAdditionModifiers.

Monster request, passive request, Buff Tick request, or generic script request
  -> SubmittedAdditionModifiers is empty.
  -> cannot receive Addition A's modifiers.
```

## 3. Player Current Skill Classes

### 3.1 `PlayerCurrentSkillAuthoring`

File target:

```text
Assets/Scripts/Game/Unit/Component/PlayerCurrentSkillAuthoring.cs
```

Contains exactly:

```text
PlayerCurrentSkillAuthoring : MonoBehaviour
PlayerCurrentSkillAuthoring.Baker : Baker<PlayerCurrentSkillAuthoring>
PlayerCurrentSkillComponent : IComponentData
PlayerCurrentSkillSource : UnitManagedComponentSource<PlayerCurrentSkillComponent>
```

The Baker adds one managed `PlayerCurrentSkillComponent` to the player Prefab. Non-player units do not receive this Authoring.

### 3.2 `PlayerCurrentSkillComponent`

```csharp
public sealed class PlayerCurrentSkillComponent : IComponentData
{
    public int CurrentSlotIndex = -1;
    public SkillModifierSet PendingAdditionModifiers = new();
}
```

Field meaning:

```text
CurrentSlotIndex
  Index into WorldSkillDataComponent.CurrentChainId.
  -1 means no current player skill-chain slot.

PendingAdditionModifiers
  Addition-only modifiers accumulated after selecting the current slot and before
  SubmitCurrentChainSkill copies them into one request.
  This is neither a Buff cache nor a final skill snapshot.
```

### 3.3 `PlayerCurrentSkillUtility`

File target:

```text
Assets/Scripts/Game/Skill/PlayerCurrentSkillUtility.cs
```

Required functions:

```csharp
public static bool TrySetCurrentSlotIndex(
    EntityManager entityManager,
    Entity entity,
    int slotIndex);

public static bool TryGetCurrentSlot(
    EntityManager entityManager,
    Entity entity,
    out WorldSkillChainSlotData slot);

public static bool TryGetCurrentSkillId(
    EntityManager entityManager,
    Entity entity,
    out int skillId);

public static bool TryGetCurrentAdditionId(
    EntityManager entityManager,
    Entity entity,
    out int additionId);

public static bool AddPendingAdditionModifier(
    EntityManager entityManager,
    Entity entity,
    SkillModifierEntry entry);

public static SkillModifierSet ConsumePendingAdditionModifiers(
    EntityManager entityManager,
    Entity entity);

public static void ClearPendingAdditionModifiers(
    EntityManager entityManager,
    Entity entity);

public static void ClearCurrentSlot(
    EntityManager entityManager,
    Entity entity);
```

Rules for these functions:

```text
TrySetCurrentSlotIndex
  Validates that the index exists in the selected WorldSkillDataComponent chain.
  Clears PendingAdditionModifiers before assigning a different slot.

TryGetCurrentSlot / TryGetCurrentSkillId / TryGetCurrentAdditionId
  Derive values from WorldSkillDataComponent.CurrentChainId + CurrentSlotIndex.
  SkillId and AdditionId are never stored separately on the player.

AddPendingAdditionModifier
  Appends one Addition-only entry.

ConsumePendingAdditionModifiers
  Returns a clone of the current set, then replaces PendingAdditionModifiers with
  a new empty set. It is called only by SubmitCurrentChainSkill.

ClearCurrentSlot
  Sets CurrentSlotIndex to -1 and clears the pending set. It is used by the
  configured State Script cleanup path when the player skill flow ends.
```

### 3.4 `PlayerCurrentSkillSource`

Required getters:

```text
player.skill.currentSlotIndex                 -> Number
player.skill.currentSkillId                   -> Number
player.skill.currentAdditionId                -> Number
player.skill.hasCurrentSkill                  -> Bool
```

Required setters:

```text
player.skill.currentSlotIndex.set(slotIndex)

player.skill.pendingAdditionModifiers.add(
  channel,
  factor,
  bonus)
```

`channel` is a numeric `SkillModifierChannel` value. The Source validates that it is a valid editable channel. `factor` and `bonus` are Numbers.

There is deliberately no graph-visible setter for consuming or clearing pending modifiers. Only the dedicated current-chain submit node may consume them, preventing another generic graph branch from stealing modifiers intended for the selected skill.

## 4. State Script Addition Nodes

### 4.1 `AdditionStateScriptNodeData`

File target:

```text
Assets/Scripts/Game/Data/StateScript/AdditionStateScriptNodeData.cs
```

```csharp
[FactoryKey("Addition")]
public sealed class AdditionStateScriptNodeData : StateStateScriptNodeData
{
    public string EventName;
}
```

The Data class contains only the event name. It does not contain an Addition ID, an action list, input parameters, or a type enum.

### 4.2 `AdditionStateScriptNode`

File target:

```text
Assets/Scripts/Game/Unit/StateScript/Nodes/AdditionStateScriptNode.cs
```

```csharp
[FactoryKey("Addition")]
public sealed class AdditionStateScriptNode : StateScriptStateNode
{
    private readonly AdditionStateScriptNodeData _data;
    private readonly List<SkillAdditionAction> _runningActions;

    protected override bool OnBind(out string error);
    protected override void OnActivate();
    protected override void OnUpdate();
    protected override void OnComplete();
    protected override void OnAbort();
    protected override void OnStop();
    protected override void OnDeactivate();
}
```

Port layout comes from `StateScriptStateNode` and is fixed:

```text
Inputs
  Start
  Abort

Outputs
  OnStart
  OnTick
  OnComplete
  OnAbort
  OnStop
```

Method responsibilities:

```text
OnBind
  Validates EventName is not empty.
  Validates the owner can read player.skill.currentSlotIndex.

OnActivate
  Calls SkillAdditionEventDispatcher.CreateActions(Runtime, _data.EventName).
  Starts every returned action in configured order.
  Keeps only actions that report Running in _runningActions.
  Calls Complete immediately when no action remains running.

OnUpdate
  Ticks every action in _runningActions.
  Removes completed actions.
  Calls Complete when the list becomes empty.

OnAbort / OnStop
  Calls Stop on every remaining action.

OnDeactivate
  Clears _runningActions. It never removes Buffs automatically.
```

An Addition node's completion is only its own execution lifecycle. Buff removal is always an explicitly configured Effect in another callback or graph event. If no remove-Buff Effect is configured, that Buff remains according to its normal duration and stack rules.

### 4.3 `SubmitCurrentChainSkillActionNode`

Only player skill-chain graphs may submit Addition-local modifiers. This requires a dedicated node instead of changing the generic `RequestSkillActionNode`.

File targets:

```text
Assets/Scripts/Game/Data/StateScript/SubmitCurrentChainSkillActionNodeData.cs
Assets/Scripts/Game/Unit/StateScript/Nodes/SubmitCurrentChainSkillActionNode.cs
```

```csharp
[FactoryKey("SubmitCurrentChainSkill")]
public sealed class SubmitCurrentChainSkillActionNodeData : ActionStateScriptNodeData
{
}

[FactoryKey("SubmitCurrentChainSkill")]
public sealed class SubmitCurrentChainSkillActionNode : StateScriptActionNode
{
    protected override bool OnBind(out string error);
    private void Submit();
    private SkillReleaseRequest CreateRequest(
        EntityManager entityManager,
        Entity entity,
        int skillId,
        SkillModifierSet submittedAdditionModifiers);
}
```

Ports:

```text
Input:  In
Output: Out
```

`Submit()` performs this exact sequence:

```text
1. Resolve SkillId from PlayerCurrentSkillUtility.TryGetCurrentSkillId.
2. ConsumePendingAdditionModifiers.
3. Create one raw SkillReleaseRequest with that copied set in
   SubmittedAdditionModifiers.
4. Append the request to UnitSkillReleaseComponent.PendingRequests.
5. Pulse Out.
```

It never reads the unit's persistent Buff modifiers and never calls `SkillResolver.Resolve`.

### 4.4 `RequestSkillActionNode`

The existing generic `RequestSkillActionNode` remains for direct skill requests from monsters, passives, Buff triggers, and generic State Script graphs.

It keeps its configurable `SkillId` expression and produces:

```csharp
new SkillReleaseRequest
{
    SkillId = skillId,
    OriginEntity = entity,
    SubmittedAdditionModifiers = new SkillModifierSet(),
}
```

It never consumes `PlayerCurrentSkillComponent.PendingAdditionModifiers`. This is the mechanical guarantee that Addition-local modifiers cannot leak into a monster, passive, or Buff-triggered skill.

## 5. Skill Addition Data and Actions

### 5.1 Data Classes

File target:

```text
Assets/Scripts/Game/Data/SkillAddition/
```

```csharp
public sealed class SkillAdditionData : DataRow
{
    public string NameKey;
    public string DescriptionKey;
    public string IconPath;
    public List<SkillAdditionCallbackData> Callbacks = new();
}

public sealed class SkillAdditionCallbackData
{
    public string EventName;
    public List<ConditionConfig> Conditions = new();

    [SerializeReference]
    public SkillAdditionActionData[] Actions = Array.Empty<SkillAdditionActionData>();
}

[Serializable]
public abstract class SkillAdditionActionData
{
}
```

`SkillAdditionData` has no `StaticModifiers` field. An Addition's modifier contribution is always expressed as an Action placed on a named event.

### 5.2 Runtime Base and Dispatcher

File target:

```text
Assets/Scripts/Game/Skill/Addition/
```

```csharp
public abstract class SkillAdditionAction
{
    public abstract SkillAdditionActionStatus Start();
    public virtual void Tick(float deltaTime) { }
    public virtual void Stop() { }
}

public enum SkillAdditionActionStatus
{
    Completed,
    Running,
}

public static class SkillAdditionEventDispatcher
{
    public static List<SkillAdditionAction> CreateActions(
        StateScriptRuntime runtime,
        string eventName);

    private static void CollectEquippedAdditionActions(
        StateScriptRuntime runtime,
        string eventName,
        List<SkillAdditionAction> output);

    private static void CollectGrantedAdditionActions(
        StateScriptRuntime runtime,
        string eventName,
        List<SkillAdditionAction> output);

    private static bool PassConditions(
        StateScriptRuntime runtime,
        List<ConditionConfig> conditions);
}
```

`CreateActions` collects matching callbacks in this order:

```text
1. The Addition ID derived from the current player chain and slot.
2. Additions granted by active SkillAdditionGrantBuffData Buffs on the owner.
```

For every matching callback, it evaluates `Conditions` through the existing comparator path and creates concrete runtime actions through the generated factory. It does not retain a long-lived `SkillAdditionRuntime` instance.

The `SkillAdditionActionStatus` enum is action lifecycle state, not an extensibility type selector. Concrete action behavior remains inheritance plus factory registration.

### 5.3 `ModifyCurrentSkillAdditionAction`

```csharp
public sealed class SkillModifierExpressionEntry
{
    public SkillModifierChannel Channel;
    public ComparatorValueExpression FactorExpression;
    public ComparatorValueExpression BonusExpression;
}

[FactoryKey("ModifyCurrentSkill")]
public sealed class ModifyCurrentSkillAdditionActionData : SkillAdditionActionData
{
    public List<SkillModifierExpressionEntry> Entries = new();
}

[FactoryKey("ModifyCurrentSkill")]
public sealed class ModifyCurrentSkillAdditionAction : SkillAdditionAction
{
    public override SkillAdditionActionStatus Start();
}
```

`Start()` resolves every expression with the owning `StateScriptRuntime.Sources`, then calls:

```text
player.skill.pendingAdditionModifiers.add(channel, factor, bonus)
```

It returns `Completed` immediately. It can bind only when `player.skill.hasCurrentSkill` is available. It never writes a Buff and never writes a unit-wide persistent skill modifier.

### 5.4 `SetSourceValueAdditionAction`

```csharp
[FactoryKey("SetSourceValue")]
public sealed class SetSourceValueAdditionActionData : SkillAdditionActionData
{
    public string SetterKey;
    public List<ComparatorValueExpression> ArgumentExpressions = new();
}

[FactoryKey("SetSourceValue")]
public sealed class SetSourceValueAdditionAction : SkillAdditionAction
{
    public override SkillAdditionActionStatus Start();
}
```

`Start()` evaluates `ArgumentExpressions`, calls `UnitSourceAccessTable.TrySet(SetterKey, values)`, and returns `Completed`.

### 5.5 `ExecuteEffectsAdditionAction`

```csharp
[FactoryKey("ExecuteEffects")]
public sealed class ExecuteEffectsAdditionActionData : SkillAdditionActionData
{
    [SerializeReference]
    public EffectData[] Effects = Array.Empty<EffectData>();
}

[FactoryKey("ExecuteEffects")]
public sealed class ExecuteEffectsAdditionAction : SkillAdditionAction
{
    public override SkillAdditionActionStatus Start();
}
```

This action forwards its effects to the normal lower effect execution path. Applying a Buff, removing a Buff, changing stacks, damage, healing, and health cost remain EffectData behavior; they do not gain separate Addition-specific runtime types.

### 5.6 `ReplayCurrentSkillAdditionAction`

```csharp
[FactoryKey("ReplayCurrentSkill")]
public sealed class ReplayCurrentSkillAdditionActionData : SkillAdditionActionData
{
    public ComparatorValueExpression ExtraCastCountExpression;
    public ComparatorValueExpression IntervalSecondsExpression;
}

[FactoryKey("ReplayCurrentSkill")]
public sealed class ReplayCurrentSkillAdditionAction : SkillAdditionAction
{
    private int _remainingCastCount;
    private float _remainingInterval;

    public override SkillAdditionActionStatus Start();
    public override void Tick(float deltaTime);
    public override void Stop();
    private void SubmitReplay();
}
```

`SubmitReplay()` reads the current Skill ID through `PlayerCurrentSkillUtility.TryGetCurrentSkillId` and creates a raw `SkillReleaseRequest` with an empty `SubmittedAdditionModifiers` set. A replay does not redispatch its Addition event and cannot recursively double cast.

If an Addition needs a replay-specific modifier, its callback configures `ModifyCurrentSkill` before a separate explicit current-chain submit path. Replay itself does not introduce a second hidden modifier lane.

## 6. Followup Through Buff Grants

The old Followup data and runtime system is replaced by a normal Buff type that grants ordinary Addition IDs while active.

```csharp
public sealed class SkillAdditionGrantBuffData : BuffData
{
    public List<int> GrantedAdditionIds = new();
}
```

While this Buff exists, `SkillAdditionEventDispatcher.CollectGrantedAdditionActions` reads its IDs and includes their matching callbacks.

There is no separate consume-rule hierarchy, Followup stack counter, or Followup-specific modifier storage. A callback reads Buff stacks and unit variables through Source, then uses normal Effects and SetSourceValue actions to mutate them.

```text
Example: three-use followup

Condition
  unit.buffs.stackCount(followupBuffId) > 0

Actions
  ModifyCurrentSkill(...)
  ExecuteEffects(ChangeBuffStack(followupBuffId, -1))
```

The Buff ceases to grant Addition IDs when it expires, is removed, or reaches zero stack. No dispatcher cleanup action is required.

## 7. Buff Classes and Live Unit Properties

### 7.1 `UnitBuffRuntimeComponent`

File target:

```text
Assets/Scripts/Game/Unit/Component/UnitBuffRuntimeAuthoring.cs
```

```csharp
public sealed class UnitBuffRuntimeComponent : IComponentData
{
    public List<UnitBuffRuntimeEntry> Buffs = new();
}

public sealed class UnitBuffRuntimeEntry
{
    public int BuffId = -1;
    public float RemainingTime = -1f;
    public int StackCount = 1;
    public bool HasOriginEntity;
    public Entity OriginEntity = Entity.Null;
    public int SourceSkillId = -1;
    public List<PropertyModifierEntry> PropertyModifiers = new();
    public List<SkillModifierEntry> SkillModifiers = new();
    public List<BuffTriggerRuntimeEntry> TriggerEntries = new();
}
```

`UnitBuffRuntimeEntry` is the sole stored state of an active Buff. It stores definitions and lifetime data, not calculated attack, defense, health, movement, or skill Modifier totals.

### 7.2 `UnitBuffUtility`

Required mutation functions:

```csharp
public static bool Apply(
    EntityManager entityManager,
    Entity target,
    int buffId,
    float durationSeconds,
    int stackCount,
    Entity originEntity,
    int sourceSkillId);

public static bool Remove(
    EntityManager entityManager,
    Entity target,
    int buffId);

public static bool ChangeStack(
    EntityManager entityManager,
    Entity target,
    int buffId,
    int stackDelta);

public static int GetStackCount(
    EntityManager entityManager,
    Entity target,
    int buffId);
```

`Apply`, `Remove`, and `ChangeStack` modify the Buff list immediately. They do not invoke periodic Tick behavior, consume a frame of duration, execute hooks, or aggregate modifiers into Component fields.

The existing Apply-Buff, Remove-Buff, and Change-Buff-Stack Effect runtimes call these functions. The Buff Source setters call these functions too; no Source setter edits `Buffs` directly.

### 7.3 `UnitBuffSystem`

`UnitBuffSystem.OnUpdate()` has only Buff lifecycle responsibilities:

```csharp
private void UpdateBuffEntries(
    Entity entity,
    UnitBuffRuntimeComponent runtimeComponent,
    PendingEffectExecutionQueueComponent effectExecutionQueue,
    float deltaTime);
```

It performs:

```text
1. Decrement finite RemainingTime.
2. Remove expired or zero-stack entries.
3. Enqueue periodic Buff Tick effects.
```

It no longer calls `BuildPropertyModifiers`, `BuildSkillModifiers`, `UnitModifierUtility.ApplyRuntimePropertyModifiers`, or `UnitSkillModifierUtility.AddRuntimeModifiers`.

### 7.4 `UnitModifierResolver`

File target:

```text
Assets/Scripts/Game/Unit/Utility/UnitModifierResolver.cs
```

```csharp
public static class UnitModifierResolver
{
    public static float GetMoveSpeed(EntityManager entityManager, Entity entity);
    public static float GetMaxHealth(EntityManager entityManager, Entity entity);
    public static float GetDefense(EntityManager entityManager, Entity entity);
    public static float GetAttackPower(EntityManager entityManager, Entity entity);
    public static float GetSkillRange(EntityManager entityManager, Entity entity);
    public static float GetMaxMp(EntityManager entityManager, Entity entity);
    public static float GetHealthRegen(EntityManager entityManager, Entity entity);
    public static float GetMpRegen(EntityManager entityManager, Entity entity);
    public static float GetChantSpeedBonus(EntityManager entityManager, Entity entity);
    public static float GetElementPower(
        EntityManager entityManager,
        Entity entity,
        ElementType elementType);

    public static SkillModifierSet BuildPersistentSkillModifiers(
        EntityManager entityManager,
        Entity entity);

    private static ModifierValue GetPropertyModifier(
        EntityManager entityManager,
        Entity entity,
        PropertyModifierChannel channel);
}
```

`ModifierValue` is a small private value type:

```csharp
private readonly struct ModifierValue
{
    public readonly float Factor;
    public readonly float Bonus;

    public float Apply(float baseValue);
}
```

`GetPropertyModifier` traverses only the current active Buff entries and only entries matching the requested `PropertyModifierChannel`. It uses the existing additive formula:

```text
FactorSum += entry.Factor * stackCount
Bonus     += entry.Bonus * stackCount

final = baseValue * max(minimumFactor, max(0, 1 + FactorSum)) + Bonus
```

`BuildPersistentSkillModifiers` traverses the current Buff list and returns a newly built local `SkillModifierSet`. The returned set is used only to create one release snapshot.

There is no `Dirty` field, no Modifier refresh System, and no persistent `PropertyModifierSet` or `SkillModifierSet` cache in the target design.

### 7.5 Component and Source Changes

The target unit Components store base and current state only. They do not store Buff-derived factors and bonuses:

```text
UnitMoveComponent
  Keeps base speed, direction, and movement state.
  Removes SpeedFactor and SpeedBonus.

UnitVitalityComponent
  Keeps base max health, base offsets, CurrentHealth, base regeneration, and base defense.
  Removes HealthFactor, HealthBonus, HealthRegenFactor, HealthRegenBonus,
  DefenseFactor, and DefenseBonus.

UnitAttackComponent
  Keeps base attack, range, and chant-speed values plus equipment offsets.
  Removes AttackFactor, AttackBonus, RangeFactor, RangeBonus,
  ChantSpeedFactor, and ChantSpeedBonus.

UnitManaComponent
  Keeps base max MP, CurrentMana, and base regeneration values.
  Removes MpFactor, MpBonus, MpRegenFactor, and MpRegenBonus.

UnitElementComponent
  Stores only intrinsic element data. Buff-provided element power is read through
  UnitModifierResolver.GetElementPower.
```

`UnitSourceDefinitionBuilder<TComponent>` gains a contextual get delegate and registration method:

```csharp
public delegate UnitValue ContextualComponentGetter<TComponent>(
    in UnitSourceBindingContext context,
    in TComponent component,
    UnitValue[] parameters);

public void AddContextGet(
    string key,
    UnitValueCategory returnType,
    IReadOnlyList<ComparatorParameterDefinition> parameters,
    ContextualComponentGetter<TComponent> getter);
```

Final-value Sources use `AddContextGet` and call `UnitModifierResolver`:

```text
unit.move.realMoveSpeed
unit.vitality.realMaxHealth
unit.vitality.realHealthRegenPerSecond
unit.vitality.realDefense
unit.vitality.currentHealthPercentage
unit.attack.realAttackPower
unit.attack.realSkillRange
unit.attack.realChantSpeedBonus
unit.attack.chantDurationMultiplier
unit.mana.realMaxMp
unit.mana.realMpRegenPerSecond
unit.mana.currentManaPercentage
unit.element.waterPower
unit.element.firePower
unit.element.lightningPower
unit.element.windPower
unit.skillModifier.getMpCost
```

Raw values such as current health, current mana, Buff count, Buff stack count, base attack, and base defense remain ordinary component getters.

Gameplay systems use the resolver too:

```text
UnitMoveSystem              -> GetMoveSpeed
UnitRecoverySystem          -> GetHealthRegen / GetMpRegen
DamageEffect                -> GetAttackPower(origin) / GetDefense(target)
HealEffect                  -> GetAttackPower(origin) / GetMaxHealth(target)
RestoreManaEffect           -> GetAttackPower(origin) / GetMaxMp(target)
HealthCostEffect            -> GetMaxHealth(origin)
```

## 8. Release Request and Snapshot Classes

### 8.1 `UnitSkillReleaseComponent`

File target:

```text
Assets/Scripts/Game/Unit/Component/UnitSkillReleaseAuthoring.cs
```

```csharp
public sealed class UnitSkillReleaseComponent : IComponentData
{
    public List<SkillReleaseRequest> PendingRequests = new();
}
```

It is a queue of raw submitted requests. It stores no final resolved Skill data.

### 8.2 `SkillReleaseRequest`

```csharp
public sealed class SkillReleaseRequest
{
    public int SkillId = -1;
    public Entity OriginEntity = Entity.Null;
    public float3 OriginPosition;
    public float2 OriginFacing = new(1f, 0f);
    public bool HasTargetEntity;
    public Entity TargetEntity = Entity.Null;
    public bool HasTargetPosition;
    public float3 TargetPosition;
    public SkillModifierSet SubmittedAdditionModifiers = new();
}
```

This class is a submission record, not a final snapshot. It deliberately removes these current fields:

```text
HasElementSnapshot
ElementSnapshot
ModifierSnapshot
```

The request captures only request-level information that must survive from submit time to release time, such as selected Skill ID and configured target information. It stores an owned clone of `SubmittedAdditionModifiers` because those modifiers belong to this one submit operation.

### 8.3 `SkillReleaseSnapshotUtility`

File target:

```text
Assets/Scripts/Game/Skill/SkillReleaseSnapshotUtility.cs
```

```csharp
public static class SkillReleaseSnapshotUtility
{
    public static bool TryCreate(
        EntityManager entityManager,
        SkillReleaseRequest request,
        out ResolvedSkillData resolvedSkill);

    private static SkillModifierSet BuildFinalModifiers(
        EntityManager entityManager,
        SkillReleaseRequest request);

    private static UnitElementComponent CaptureElementState(
        EntityManager entityManager,
        Entity entity);
}
```

`TryCreate` performs this exact sequence:

```text
1. Read SkillData by request.SkillId.
2. Build persistent unit Skill modifiers through
   UnitModifierResolver.BuildPersistentSkillModifiers(request.OriginEntity).
3. Add request.SubmittedAdditionModifiers.
4. Capture element values through UnitModifierResolver at this release moment.
5. Call SkillResolver.Resolve with the final modifier set and captured element values.
6. Return the immutable ResolvedSkillData.
```

`ResolvedSkillData` is the final data snapshot for one release. Its `MpCost` and `EffectChain` have already incorporated final modifiers. The execution layer does not recalculate those values from the caster after this point.

`SkillAnalysisUtility.TryAnalyzeSkill` is replaced by `SkillReleaseSnapshotUtility.TryCreate`.

### 8.4 `SkillReleaseSystem`

```csharp
public partial class SkillReleaseSystem : SystemBase
{
    protected override void OnUpdate();
}
```

For each queued request, `OnUpdate()` does only:

```text
1. Remove the next raw SkillReleaseRequest from PendingRequests.
2. Call SkillReleaseSnapshotUtility.TryCreate.
3. Pass the raw request and returned ResolvedSkillData to SkillReleaseUtility.TryExecute.
```

The release system is shared by player, monster, passive, Buff-triggered, and script requests. It does not need to know whether submitted Addition modifiers are empty; merging an empty set is harmless.

## 9. Effect Ordering Rules

`SkillExecutor.ExecuteEffects` runs the already-resolved `EffectData[]` in order.

```text
ApplyBuff(Attack +100) -> Damage
  ApplyBuff updates the caster's Buff list.
  Damage calls UnitModifierResolver.GetAttackPower.
  Damage sees the new +100 attack.

ApplyBuff(Defense +100) -> Damage
  ApplyBuff updates the target Buff list.
  Damage calls UnitModifierResolver.GetDefense.
  Damage sees the new +100 defense.

ApplyBuff(Skill Damage +50%) -> Damage
  The release snapshot was created before this Effect chain began.
  The current DamageEffectData coefficient is unchanged.
  The Skill Damage Buff may affect a later new SkillReleaseRequest.
```

This distinction is intentional:

```text
PropertyModifier
  Live unit state. Later Effects read it when they calculate a final attribute.

SkillModifier
  Captured once by SkillReleaseSnapshotUtility for one release.
```

If a future skill requires a mid-chain SkillModifier Buff to affect its later EffectData parameters, it must explicitly submit a new sub-skill request after applying the Buff. The first version has no implicit re-snapshot node.

## 10. Configured Buff Removal

Addition nodes never track or remove Buffs automatically.

```text
BeforeCast callback
  ExecuteEffects(ApplyBuff: Invulnerable)

AbortCast callback
  ExecuteEffects(RemoveBuff: Invulnerable)

EndCast callback
  ExecuteEffects(RemoveBuff: Invulnerable)
```

If a removal action is not configured, the Buff remains until its own duration, another Effect, or another Source setter removes it. This is a data and graph decision, not hidden Addition cleanup logic.

When a Buff must remain through the actual execution phase, its remove action must be configured on a graph event that occurs after the desired gameplay boundary. A synchronous `SubmitCurrentChainSkill` only queues a request; it is not itself an execution-complete signal.

## 11. Example Player Chain

```text
Entry
  -> player.skill.currentSlotIndex.set(selectedSlot)
  -> Addition("BeforeCast")
  -> ChantTimer

ChantTimer.OnComplete
  -> Addition("BeforeSubmit")
  -> SubmitCurrentChainSkill
  -> Addition("AfterSubmit")

ChantTimer.OnAbort
  -> Addition("AbortCast")
```

Example Addition configuration:

```text
Damage Addition
  BeforeSubmit
    ModifyCurrentSkill(
      Damage FactorExpression = unit.variables.getNumber("damageBonus"),
      FlatDamage BonusExpression = 10)

Invulnerability Addition
  BeforeCast
    ExecuteEffects(ApplyBuff: Invulnerable)

  AbortCast
    ExecuteEffects(RemoveBuff: Invulnerable)

  EndCast
    ExecuteEffects(RemoveBuff: Invulnerable)

Double Cast Addition
  AfterSubmit
    ReplayCurrentSkill(
      ExtraCastCountExpression = 1,
      IntervalSecondsExpression = 0.1)
```

Only the `Damage Addition` writes `PendingAdditionModifiers`, and only `SubmitCurrentChainSkill` copies that data to the original submitted request. The Double Cast action submits a separate request with an empty Addition modifier set unless its graph deliberately creates another current-chain submission first.

## 12. Target File Layout

```text
Assets/Scripts/Game/Data/SkillAddition/
  SkillAdditionData.cs
  SkillAdditionCallbackData.cs
  SkillAdditionActionData.cs
  Actions/
    ModifyCurrentSkillAdditionActionData.cs
    SetSourceValueAdditionActionData.cs
    ExecuteEffectsAdditionActionData.cs
    ReplayCurrentSkillAdditionActionData.cs
  SkillAdditionGrantBuffData.cs

Assets/Scripts/Game/Skill/Addition/
  SkillAdditionAction.cs
  SkillAdditionEventDispatcher.cs
  Actions/
    ModifyCurrentSkillAdditionAction.cs
    SetSourceValueAdditionAction.cs
    ExecuteEffectsAdditionAction.cs
    ReplayCurrentSkillAdditionAction.cs

Assets/Scripts/Game/Skill/
  PlayerCurrentSkillUtility.cs
  SkillReleaseSnapshotUtility.cs

Assets/Scripts/Game/Unit/Component/
  PlayerCurrentSkillAuthoring.cs
  UnitSkillReleaseAuthoring.cs
  UnitBuffRuntimeAuthoring.cs

Assets/Scripts/Game/Unit/Utility/
  UnitBuffUtility.cs
  UnitModifierResolver.cs

Assets/Scripts/Game/Unit/StateScript/Nodes/
  AdditionStateScriptNode.cs
  SubmitCurrentChainSkillActionNode.cs

Assets/Scripts/Game/Data/StateScript/
  AdditionStateScriptNodeData.cs
  SubmitCurrentChainSkillActionNodeData.cs
```

## 13. Explicit Non-Goals for the First Version

```text
No automatic Buff cleanup by Addition nodes.
No persistent aggregate Modifier cache.
No implicit re-snapshot during one Effect chain.
No Followup-specific consume-rule or modifier hierarchy.
No Addition type enum, action type enum, or central action switch.
No addition modifier leakage into generic RequestSkill, monster, passive, or Buff Tick releases.
```
