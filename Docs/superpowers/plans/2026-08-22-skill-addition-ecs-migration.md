# Skill Addition ECS Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace legacy Skill Addition/Followup/CastTask behavior with request-local extra modifiers, release-time snapshots, live Buff property resolution, and StateScript Addition actions.

**Architecture:** A raw `SkillReleaseRequest` owns its `ExtraModifiers`; `SkillReleaseSystem` combines them with Buff-derived persistent modifiers when creating `ResolvedSkillData`. Player StateScript code owns a stable chain-and-slot selection and can submit its pending extra modifiers through `RequestSkillWithAddition`; no existing graph JSON is changed. Buffs stay as managed active state and `UnitModifierResolver` becomes the single final-value reader.

**Tech Stack:** Unity Entities managed `IComponentData`, C#, NUnit EditMode tests, Newtonsoft JSON data tables, generated StateScript registries, Unity Editor data windows.

**Spec:** `Docs/superpowers/specs/2026-08-22-skill-addition-ecs-design.md`

## Global Constraints

- Do not modify `Assets/Res/Data/StateScriptDataTable.json`.
- Do not retain legacy Followup, SkillCastTask, or unit-wide runtime skill-modifier compatibility code.
- `ExtraModifiers` are owned by exactly one `SkillReleaseRequest`; every write clones the supplied set.
- `PlayerCurrentSkillComponent` identifies a skill by its stored `CurrentChainId` and `CurrentSlotIndex`, never by the mutable world current-chain ID.
- Buff-derived final values are read through `UnitModifierResolver`; no Buff system writes component factor/bonus caches.
- New managed ECS code is permitted where it owns lists, strings, action objects, or managed `SkillModifierSet`; isolate it from Burst jobs.
- Preserve the `SkillAdditionDataTable` asset path but replace its rows with an empty new-schema table.

---

### Task 1: Add request-local extra modifiers and release-time snapshot creation

**Files:**
- Create: `Assets/Scripts/Game/Skill/SkillReleaseRequestUtility.cs`
- Create: `Assets/Scripts/Game/Skill/SkillReleaseSnapshotUtility.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitSkillReleaseAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/StateScript/Nodes/RequestSkillActionNode.cs`
- Modify: `Assets/Scripts/Game/Unit/System/SkillReleaseSystem.cs`
- Delete: `Assets/Scripts/Game/Skill/SkillAnalysisUtility.cs`
- Test: `Assets/Tests/Editor/SkillReleaseRequestTests.cs`

**Interfaces:**
- Produces `SkillReleaseRequestUtility.Create(EntityManager, Entity, int, SkillModifierSet)` which captures request target/origin data and clones the fourth argument into `request.ExtraModifiers`.
- Produces `SkillReleaseSnapshotUtility.TryCreate(EntityManager, SkillReleaseRequest, out ResolvedSkillData)`.
- `SkillReleaseRequest` contains `SkillModifierSet ExtraModifiers`; remove `HasElementSnapshot`, `ElementSnapshot`, and `ModifierSnapshot`.

- [ ] **Step 1: Write request-ownership tests**

```csharp
[Test]
public void RequestUtility_ClonesExtraModifiers()
{
    SkillModifierSet supplied = new();
    supplied.Add(new SkillModifierEntry { Channel = SkillModifierChannel.Damage, Bonus = 10f });
    SkillReleaseRequest request = SkillReleaseRequestUtility.Create(_entityManager, _entity, 1, supplied);
    supplied.Add(new SkillModifierEntry { Channel = SkillModifierChannel.Damage, Bonus = 5f });
    Assert.That(request.ExtraModifiers.GetBonus(SkillModifierChannel.Damage), Is.EqualTo(10f));
}
```

- [ ] **Step 2: Implement raw request creation and compile**

`RequestSkillActionNode` calls the utility with `new SkillModifierSet()`. The utility moves the existing transform/facing/variable-target capture code out of the node and never reads element or unit modifier components.

Run: `dotnet build "Crystal Magic.sln" -nologo`

- [ ] **Step 3: Implement release snapshot creation**

```csharp
SkillModifierSet finalModifiers = UnitModifierResolver.BuildPersistentSkillModifiers(
    entityManager, request.OriginEntity);
finalModifiers.Add(request.ExtraModifiers);
resolvedSkill = SkillResolver.Resolve(baseSkill, finalModifiers, CaptureElementState(entityManager, request.OriginEntity));
```

Change `SkillReleaseSystem` to use this utility before `SkillReleaseUtility.TryExecute`.

- [ ] **Step 4: Run EditMode request tests and build**

Run: Unity Test Runner, `UnitReleaseRequestTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
feat: create skill snapshots at release time
```

### Task 2: Add contextual Source getter and setter support

**Files:**
- Modify: `Assets/Scripts/Game/Unit/Source/UnitComponentSource.cs`
- Modify: `Assets/Tests/Editor/UnitRuntimeBehaviorTests.cs`

**Interfaces:**
- Produces `ContextualComponentGetter<TComponent>` and `ContextualComponentSetter<TComponent>` delegates with `in UnitSourceBindingContext`.
- Produces `UnitSourceDefinitionBuilder<T>.AddContextGet(...)` and `AddContextSet(...)`.
- Existing `AddGet` and `AddSet` signatures remain source-compatible.

- [ ] **Step 1: Write schema/binding tests for contextual delegates**

```csharp
builder.AddContextGet("test.context.entity", UnitValueCategory.Number,
    (in UnitSourceBindingContext context, in UnitMoveComponent _, UnitValue[] _) =>
        UnitValue.FromInt(context.Entity.Index));
```

Assert that a bound source can return the entity index and that a contextual setter receives the same entity.

- [ ] **Step 2: Implement dual invocation definitions**

Store either the existing delegate or contextual delegate on each internal get/set definition. In both `UnitComponentSource<T>` and `UnitManagedComponentSource<T>`, invoke the contextual delegate with the captured bind context; do not change ordinary source behavior.

- [ ] **Step 3: Run the existing source tests and build**

Run: Unity Test Runner, `UnitRuntimeBehaviorTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 4: Commit**

```text
feat: add contextual unit source accessors
```

### Task 3: Introduce stable player current-skill ECS state

**Files:**
- Create: `Assets/Scripts/Game/Unit/Component/PlayerCurrentSkillAuthoring.cs`
- Create: `Assets/Scripts/Game/Skill/PlayerCurrentSkillUtility.cs`
- Modify: player unit prefab(s) containing `UnitStateScriptAuthoring` and player faction data
- Modify: `Assets/Scripts/Game/Unit/Source/UnitComponentSourceRegistry.cs`
- Test: `Assets/Tests/Editor/PlayerCurrentSkillUtilityTests.cs`

**Interfaces:**
- `TrySetCurrentChainSlot(EntityManager, Entity, int chainId, int slotIndex)` validates `WorldSkillDataComponent.TryGetChainSlot` and clears pending extra modifiers only when either identifier changes.
- `TryGetCurrentSlot`, `TryGetCurrentSkillId`, `TryGetCurrentAdditionId`, `AddPendingExtraModifier`, `ConsumePendingExtraModifiers`, and `ClearCurrentChainSlot` all use stored IDs.
- `PlayerCurrentSkillSource` exposes `player.skill.currentChainId`, `player.skill.currentSlotIndex`, `player.skill.currentSkillId`, `player.skill.currentAdditionId`, `player.skill.hasCurrentSkill`, `player.skill.currentChainSlot.set(chainId, slotIndex)`, and `player.skill.pendingExtraModifiers.add(channel, factor, bonus)`.

- [ ] **Step 1: Write utility tests for stable chain ownership**

Create two chains in a `WorldSkillDataComponent`, select chain 0/slot 0, change `WorldSkillDataComponent.CurrentChainId` to 1, and assert that `TryGetCurrentSkillId` still returns chain 0's skill. Assert that changing the selected pair clears pending modifiers and consuming returns an independent clone.

- [ ] **Step 2: Implement authoring, utility, and contextual Source**

Use `AddComponentObject` for the managed component. The atomic two-number Source setter must reject non-integers and invalid slots. The modifier setter must validate `SkillModifierChannelUtility.IsInternalChannel(channel) == false`.

- [ ] **Step 3: Attach authoring only to player prefab(s)**

Locate prefab references by the `UnitFactionAuthoring` and `UnitStateScriptAuthoring` script GUIDs. Add the authoring only to player-prefab YAML; do not change StateScript JSON.

- [ ] **Step 4: Run player utility/source tests and build**

Run: Unity Test Runner, `PlayerCurrentSkillUtilityTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
feat: track stable player chain skill selection
```

### Task 4: Convert Buff mutation to utilities and live resolver reads

**Files:**
- Create: `Assets/Scripts/Game/Unit/Utility/UnitModifierResolver.cs`
- Modify: `Assets/Scripts/Game/Unit/Utility/UnitBuffUtility.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitBuffRuntimeAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/System/UnitBuffSystem.cs`
- Modify: `Assets/Scripts/Game/Unit/Utility/UnitModifierUtility.cs`
- Modify: `Assets/Scripts/Game/Unit/System/UnitResetSystem.cs`
- Modify: `Assets/Scripts/Game/Unit/System/PlayerEquipmentPropertySystem.cs`
- Test: `Assets/Tests/Editor/UnitModifierResolverTests.cs`

**Interfaces:**
- `UnitBuffUtility.Apply`, `Remove`, `ChangeStack`, and `GetStackCount` are the only active-Buff mutation/read helpers used by Effects and Sources.
- `UnitModifierResolver` exposes all final scalar property readers, `BuildPersistentSkillModifiers`, and `ApplyDamageTakenModifiers`.

- [ ] **Step 1: Write resolver tests**

Test factor/bonus stacking, minimum factor clamping, stack changes, removal, skill-modifier set rebuilding, and damage-taken modification against managed `UnitBuffRuntimeComponent` entries.

- [ ] **Step 2: Implement resolver and migrate Buff utility/source setters**

`UnitBuffSource` uses `AddContextSet` and calls utility methods. `Apply` preserves current stack and duration behavior; `Remove` removes all matching entries; `ChangeStack` removes the entry at zero. `UnitBuffSystem` retains only lifecycle/tick work.

- [ ] **Step 3: Preserve static equipment/element data**

Remove runtime Buff writes from `UnitModifierUtility`. Stop `UnitResetSystem` from zeroing elements. Make equipment assignment idempotent, including element values, so Buff element bonuses exist only in `UnitModifierResolver.GetElementPower`.

- [ ] **Step 4: Run resolver tests and build**

Run: Unity Test Runner, `UnitModifierResolverTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
feat: resolve active buff modifiers on demand
```

### Task 5: Remove attribute caches and migrate all final-value consumers

**Files:**
- Modify: `Assets/Scripts/Game/Unit/Component/UnitMoveAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitVitalityAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitAttackAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitManaAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitElementAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/System/UnitMoveSystem.cs`
- Modify: `Assets/Scripts/Game/Unit/System/UnitRecoverySystem.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/DamageEffect.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/RecoverEffect.cs`
- Modify: `Assets/Scripts/Game/Unit/Unit/CompareSource/UnitHealthRatioSource.cs`
- Modify: `Assets/Scripts/Game/Unit/Editor/UnitRuntimeAttributeDrawers.cs`
- Modify: `Assets/Scripts/UI/PropertyUI/PropertyUIModel.cs`
- Modify: `Assets/Scripts/UI/BattleUI/BattleUIModel.cs`
- Modify: `Assets/Scripts/UI/TrainingDummyStatsUI/TrainingDummyStatsUIModel.cs`

**Interfaces:**
- Components retain base values, equipment offsets, and current health/mana only; final values are `UnitModifierResolver` calls.
- `UnitMoveSystem` and `UnitRecoverySystem` become managed/main-thread systems because active Buff lists are managed components.

- [ ] **Step 1: Write a same-effect-chain regression test**

Execute `ApplyBuff(Attack +100)` followed by `Damage` and assert the damage calculation reads the increased attack. Execute `ApplyBuff(Skill Damage +50%)` followed by `Damage` and assert the already-resolved coefficient is unchanged.

- [ ] **Step 2: Remove cache fields and replace Source final getters**

Replace every `Real*`/factor/bonus component reader with `AddContextGet` calls to the resolver. Keep public Source keys, base getters, and current health/mana getters stable.

- [ ] **Step 3: Migrate systems, effects, UI, and drawers**

Use entity-aware resolver calls for movement acceleration/speed, recovery clamp limits, damage/defense, heal/mana restore attack power, health cost maximum health, UI snapshots, and editor displays. Use resolver values in emitted health events.

- [ ] **Step 4: Run tests and build**

Run: Unity Test Runner, `UnitModifierResolverTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
refactor: remove cached buff attribute values
```

### Task 6: Add Addition callback data, generated action registry, and editor support

**Files:**
- Create: `Assets/Scripts/Game/Data/SkillAddition/SkillAdditionCallbackData.cs`
- Create: `Assets/Scripts/Game/Data/SkillAddition/SkillAdditionActionData.cs`
- Create: `Assets/Scripts/Game/Data/SkillAddition/Actions/ModifyCurrentSkillAdditionActionData.cs`
- Create: `Assets/Scripts/Game/Data/SkillAddition/Actions/SetSourceValueAdditionActionData.cs`
- Create: `Assets/Scripts/Game/Data/SkillAddition/Actions/ExecuteEffectsAdditionActionData.cs`
- Create: `Assets/Scripts/Game/Data/SkillAddition/Actions/ReplayCurrentSkillAdditionActionData.cs`
- Create: `Assets/Scripts/Game/Data/SkillAddition/SkillAdditionGrantBuffData.cs`
- Modify: `Assets/Scripts/Game/Data/SkillAdditionData.cs`
- Modify: `Assets/Scripts/Game/Data/BuffData.cs`
- Modify: `Assets/Scripts/Game/Data/Editor/SkillAdditionEditorWindow.cs`
- Create: `Assets/Scripts/Game/Skill/Addition/SkillAdditionActionRegistry.cs`
- Create: `Assets/Scripts/Game/Skill/Editor/SkillAdditionActionRegistryGenerator.cs`
- Test: `Assets/Tests/Editor/SkillAdditionDataTests.cs`

**Interfaces:**
- Action data uses `[FactoryKey]`; registry generation creates data/runtime registrations without a behavior switch.
- `SkillAdditionGrantBuffData : BuffData` contains `List<int> GrantedAdditionIds`.

- [ ] **Step 1: Write data serialization and editor-schema tests**

Assert a callback retains event name, condition collection, and a concrete action-data type through the project's JSON settings. Assert no old `Modifiers`, `FollowupEffects`, `CastTasks`, or `EffectChain` members remain on `SkillAdditionData`.

- [ ] **Step 2: Implement data types and generated registry pattern**

Follow `StateScriptRegistryGenerator`'s generated-file pattern. The generated runtime factory maps each data type to a concrete `SkillAdditionAction`; it does not select behavior with an enum or `switch`.

- [ ] **Step 3: Replace the Addition editor UI**

Draw callback event names, conditions, ordered action rows, add/remove/reorder controls, and concrete fields. Remove all legacy modifier, Followup, cast-task, and EffectChain sections.

- [ ] **Step 4: Run data tests and build**

Run: Unity Test Runner, `SkillAdditionDataTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
feat: define callback based skill additions
```

### Task 7: Implement Addition runtime actions and dispatcher

**Files:**
- Create: `Assets/Scripts/Game/Skill/Addition/SkillAdditionAction.cs`
- Create: `Assets/Scripts/Game/Skill/Addition/SkillAdditionEventDispatcher.cs`
- Create: `Assets/Scripts/Game/Skill/Addition/Actions/ModifyCurrentSkillAdditionAction.cs`
- Create: `Assets/Scripts/Game/Skill/Addition/Actions/SetSourceValueAdditionAction.cs`
- Create: `Assets/Scripts/Game/Skill/Addition/Actions/ExecuteEffectsAdditionAction.cs`
- Create: `Assets/Scripts/Game/Skill/Addition/Actions/ReplayCurrentSkillAdditionAction.cs`
- Test: `Assets/Tests/Editor/SkillAdditionDispatcherTests.cs`

**Interfaces:**
- `SkillAdditionAction.Start()` returns `Completed` or `Running`; `Tick(float)` and `Stop()` are virtual.
- `SkillAdditionEventDispatcher.CreateActions(StateScriptRuntime, string)` returns ordered action instances.

- [ ] **Step 1: Write dispatcher isolation tests**

Assert equipped callback actions precede granted-Buff actions, failed conditions create no action, `ModifyCurrentSkill` appends only to the owner’s pending extra set, and replay requests have empty `ExtraModifiers`.

- [ ] **Step 2: Implement dispatcher and expression bindings**

Evaluate callback conditions through the existing comparator/source path. Build concrete actions with the generated action registry. Action effect execution builds a normal self-originated `SkillContent`; it never creates Addition-only effect behavior.

- [ ] **Step 3: Implement the four action types**

`ModifyCurrentSkill` validates editable channels and appends entries. `SetSourceValue` invokes `Runtime.Sources.TrySet`. `ExecuteEffects` invokes `SkillExecutor.ExecuteEffects`. `ReplayCurrentSkill` submits a raw request through `SkillReleaseRequestUtility` and never dispatches events.

- [ ] **Step 4: Run dispatcher tests and build**

Run: Unity Test Runner, `SkillAdditionDispatcherTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
feat: execute skill addition callbacks
```

### Task 8: Add StateScript Addition nodes without changing graph JSON

**Files:**
- Create: `Assets/Scripts/Game/Data/StateScript/AdditionStateScriptNodeData.cs`
- Create: `Assets/Scripts/Game/Data/StateScript/RequestSkillWithAdditionActionNodeData.cs`
- Create: `Assets/Scripts/Game/Unit/StateScript/Nodes/AdditionStateScriptNode.cs`
- Create: `Assets/Scripts/Game/Unit/StateScript/Nodes/RequestSkillWithAdditionActionNode.cs`
- Modify: `Assets/Scripts/Game/Unit/Editor/StateScriptRegistryGenerator.cs`
- Regenerate: `Assets/Scripts/Game/Unit/StateScript/StateScriptRegistry.cs`
- Test: `Assets/Tests/Editor/StateScriptAdditionNodeTests.cs`

**Interfaces:**
- `AdditionStateScriptNodeData.EventName` is its only behavior field.
- `RequestSkillWithAdditionActionNode` consumes `PlayerCurrentSkillUtility.ConsumePendingExtraModifiers` exactly once before queueing a request.

- [ ] **Step 1: Write node lifecycle tests**

Assert an Addition node with no running actions completes immediately, abort/stop calls `Stop` on remaining actions, and deactivation only clears action references. Assert `RequestSkillWithAddition` does not consume on an invalid current skill and pulses `Out` only after enqueueing a valid request.

- [ ] **Step 2: Implement nodes and factory annotations**

Derive `AdditionStateScriptNode` from `StateScriptStateNode` and use its fixed ports. Derive `RequestSkillWithAdditionActionNode` from `StateScriptActionNode` with `In`/`Out`. Do not edit `StateScriptDataTable.json`.

- [ ] **Step 3: Regenerate registry and verify editor discovery**

Run the existing StateScript registry generator and assert generated registry entries contain both new data and runtime mappings.

- [ ] **Step 4: Run StateScript tests and build**

Run: Unity Test Runner, `StateScriptAdditionNodeTests`; then `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
feat: add state script skill addition nodes
```

### Task 9: Clear Addition configuration and delete legacy systems

**Files:**
- Modify: `Assets/Res/Data/SkillAdditionDataTable.json`
- Modify: `Assets/Res/Data/SkillDataTable.json`
- Modify: `Assets/Scripts/Game/Data/SkillData.cs`
- Delete: `Assets/Scripts/Game/Data/SkillCastTaskData.cs`
- Delete: `Assets/Scripts/Game/Data/SkillFollowupEffectData.cs`
- Delete: `Assets/Scripts/Game/Data/SkillFollowupConsumeRuleData.cs`
- Delete: `Assets/Scripts/Game/Data/SkillFollowupFilterData.cs`
- Delete: `Assets/Scripts/Game/Data/SkillFollowupModifierRuleData.cs`
- Delete: `Assets/Scripts/Game/Skill/Followup/`
- Delete: `Assets/Scripts/Game/Skill/SkillFollowup*.cs`
- Delete: `Assets/Scripts/Game/Unit/Component/UnitSkillModifierRuntimeAuthoring.cs`
- Delete: `Assets/Scripts/Game/Unit/Utility/UnitSkillModifierUtility.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitSkillReleaseAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/Source/UnitComponentSourceRegistry.cs`
- Modify: `Assets/Scripts/Game/Unit/System/UnitResetSystem.cs`
- Modify: `Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs`
- Delete: obsolete Followup registry/editor generator files

- [ ] **Step 1: Write a no-legacy-reference test**

Use a compilation-level test/source scan that asserts production code has no references to `SkillFollowup`, `SkillCastTask`, or `UnitSkillModifierRuntime` after deletion.

- [ ] **Step 2: Replace data tables safely**

Write `SkillAdditionDataTable.json` as an empty table in the new schema. Strip only `CastTasks` and `FollowupEffects` properties from rows in `SkillDataTable.json`; preserve every normal skill field and effect chain. Do not edit StateScript JSON.

- [ ] **Step 3: Delete validated legacy files and prefab components**

First locate every prefab/reference using each script meta GUID. Remove only those script component blocks, then delete code and matching `.meta` files. Remove obsolete registry/editor registration references in the same patch.

- [ ] **Step 4: Run full project search and build**

Run: `rg -n 'SkillFollowup|SkillCastTask|UnitSkillModifierRuntime|SubmittedAddition|SubmitCurrentChainSkill' Assets/Scripts Assets/Tests`

Expected: no production references.

Then run: `dotnet build "Crystal Magic.sln" -nologo`.

- [ ] **Step 5: Commit**

```text
refactor: remove legacy skill addition systems
```

### Task 10: Run regression verification and document configuration handoff

**Files:**
- Modify: `Assets/Tests/Editor/UnitRuntimeBehaviorTests.cs`
- Modify: `Docs/superpowers/specs/2026-08-22-skill-addition-ecs-design.md`

- [ ] **Step 1: Run the full EditMode suite**

Run all tests in `Assets/Tests/Editor` through Unity Test Runner. Resolve every failure before proceeding.

- [ ] **Step 2: Perform the untouched-graph smoke check**

Open the test scene and use the existing player StateScript graph. Verify a normal skill releases, spends its resolved MP cost, and executes base effects without Addition data or graph changes.

- [ ] **Step 3: Record manual future-configuration requirements**

Add a short note to the spec: player graph configuration must set a stable chain/slot pair before using `Addition` or `RequestSkillWithAddition`; Buff removal remains an explicit configured Effect.

- [ ] **Step 4: Commit and report verification evidence**

```text
test: verify skill addition ecs migration
```
