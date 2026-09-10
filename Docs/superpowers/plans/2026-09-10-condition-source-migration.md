# Condition Source Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the legacy scalar condition-source system completely and migrate every skill, buff, prop, effect search, and projectile-hit condition to `ConditionConfig.Inputs` value expressions backed by `UnitSource`; expose target-condition editing for search effects and collision-target-condition editing for projectiles.

**Architecture:** A new effect-condition resolver evaluates an expression against the candidate unit's `UnitSourceRuntimeComponent`, while separately providing dynamic `effect.context.*` inputs from `SkillContent`.  All runtime call sites use that single path.  A shared editor drawer supplies the same UnitSource plus effect-context schema everywhere conditions are authored.  Legacy source implementations, fields, registry support, and serialized JSON fields are removed rather than retained as a compatibility layer.

**Tech Stack:** Unity 6/C#, Unity Entities 1.3, Unity Test Framework/NUnit, Newtonsoft JSON configuration tables, Unity IMGUI editor tools.

**Spec:** [Condition Source Migration Design](../specs/2026-09-10-condition-source-migration-design.md)

## Global Constraints

- Keep the user's existing Buff-data changes intact; only touch Skill and StateScript JSON entries required by this migration.
- Empty condition lists must remain permissive. Projectile collision retains its current non-self default when no collision conditions are configured.
- The legacy `ISource` and `SourceContext` are removed. Preserve the new `UnitValue`, `ValueExpression`, `IParameterizedUnitValueGetter`, and `IComparatorValueResolver` declarations currently co-located in `ISource.cs` by moving/retaining them in an appropriately named file.
- Do not add a new replacement for `UnitIsCastingSource`; any future casting predicate uses `unit.variables.getBool("casting")`.
- Do not add `effect.context.*` to the general State Script/Behavior Tree schemas: they are only valid for effect execution.
- Do not hand-edit the generated comparator registry. Update its generator and regenerate it from Unity's `Tools/Registry/Comparator` menu.
- For every changed C# file, update `cs_review_status.txt` according to the project skill: reviewed files are `TRUE DIRTY`, newly created files are initially `FALSE`, and removed files have their entries removed.

### Task 1: Establish migration tests and remove legacy comparator contracts

**Files:**

- Create: `Assets/Tests/Editor/Comparator/ConditionExpressionMigrationTests.cs`
- Modify: `Assets/Scripts/Game/Comparator/ISource.cs`
- Modify: `Assets/Scripts/Game/Comparator/ConditionConfig.cs`
- Modify: `Assets/Scripts/Game/Comparator/ComparatorFactory.cs`
- Modify: `Assets/Scripts/Game/Comparator/Editor/ComparatorRegistryGenerator.cs`
- Regenerate: `Assets/Scripts/Game/Comparator/ComparatorRegistry.cs`
- Delete: `Assets/Scripts/Game/Unit/Unit/CompareSource/UnitBuffStackSource.cs` and its `.meta`
- Delete: `Assets/Scripts/Game/Unit/Unit/CompareSource/UnitHealthRatioSource.cs` and its `.meta`
- Delete: `Assets/Scripts/Game/Unit/Unit/CompareSource/UnitIsCastingSource.cs` and its `.meta`
- Delete: `Assets/Scripts/Game/Unit/Unit/CompareSource/UnitIsControlledSource.cs` and its `.meta`
- Modify: `cs_review_status.txt`

**Interfaces:**

- Consumes: `ConditionConfig`, `ValueExpression`, `IComparatorValueResolver`, generated comparison and operation factories.
- Produces: a comparator API that only accepts expression conditions and a registry that only registers comparison types and value operations.

- [ ] Add NUnit tests first. Cover a two-input expression condition (for example `Equal(unit value, literal)`), unary expressions, and invalid/missing inputs returning a failed comparator. Confirm the pre-migration test compile/run fails because the new migration-only contract is not yet present.

- [ ] Remove `SourceContext` and `ISource`; retain the new typed-value declarations without leaving the misleading legacy filename. Remove `SourceType`, `SourceParam`, and `CompareValue` from `ConditionConfig` so C# callers cannot create legacy conditions.

- [ ] Delete source registration, creation, counting, legacy `BuildComparator(List<ConditionConfig>, Entity, EntityManager, ...)`, and `TryBuildLegacyCondition` from `ComparatorFactory`. Keep and test the existing `BuildComparator(IReadOnlyList<ConditionConfig>, IComparatorValueResolver)` expression path.

- [ ] Change `ComparatorRegistryGenerator` to collect only `ICompareType` and `IValueOperation`, remove `RegisterSource` generation, then invoke `Tools/Registry/Comparator` in Unity to regenerate `ComparatorRegistry.cs`. Verify the generated file contains no `RegisterSource` call.

- [ ] Delete the four legacy source files (five source types total, including `UnitIsEnemySource`), plus matching Unity metadata only after verifying each exact path. Do not leave orphan registry entries.

- [ ] Run the NUnit comparator tests in Unity EditMode and use `rg -n --glob '*.cs' 'ISource|SourceContext|SourceType|SourceParam|CompareValue|Unit(IsEnemy|IsCasting|IsControlled|BuffStack|HealthRatio)Source' Assets` to confirm no legacy condition code remains outside intentional migration test names.

### Task 2: Add a single effect-condition evaluation path and expression sources

**Files:**

- Create: `Assets/Scripts/Game/Skill/Effects/EffectConditionUtility.cs`
- Create: `Assets/Tests/Editor/Skill/EffectConditionUtilityTests.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/UnitFactionAuthoring.cs`
- Modify: `Assets/Scripts/Game/Skill/SkillExecutor.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/AreaSearchEffect.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/ConeSearchEffect.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/ChainSearchEffect.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/ForwardRectSearchEffect.cs`
- Modify: `cs_review_status.txt`

**Interfaces:**

- Consumes: `SkillContent`, candidate `Entity`, `UnitSourceRuntimeComponent.Table`, `UnitSourceAccessTable`, and `ConditionConfig.Inputs`.
- Produces: `EffectConditionUtility.Pass(IReadOnlyList<ConditionConfig>, SkillContent, Entity)` (or an equivalent explicitly named single public API) and effect-context getter keys usable only by effects.

- [ ] Write EditMode tests first with an ECS `EntityManager` and a controlled source table. Assert that an enemy condition using `unit.faction.isEnemyTo(effect.context.originEntity)` passes for an enemy, fails for an ally, and fails safely when an expected context entity or source runtime is absent. Add coverage for `effect.context.targetEntity`, `otherEntity`, `position`, and `triggerValue` default behavior.

- [ ] Implement an `IComparatorValueResolver` that resolves `effect.context.originEntity`, `effect.context.targetEntity`, `effect.context.otherEntity`, `effect.context.position`, and `effect.context.triggerValue` dynamically from `SkillContent`; delegate all other keys to the evaluated candidate unit's source table. Define these keys as shared constants so runtime and editor schema cannot drift.

- [ ] Add `unit.faction.isEnemyTo(OtherEntity: Entity) -> Bool` to `UnitFactionSource`, using `UnitFactionUtility.IsEnemy` and context-aware source binding. Return `false` when either entity lacks faction data.

- [ ] Route `SkillExecutor.PassEffectConditions` and all four search effects through the new utility. In each search, evaluate the candidate unit while retaining its existing geometry, deduplication, and traversal behavior.

- [ ] Run the new EditMode tests. Then check runtime callers with `rg -n --glob '*.cs' 'BuildComparator\(' Assets\Scripts\Game` and verify the removed Entity/EntityManager overload has no callers.

### Task 3: Make projectile-hit filtering data-driven with the same condition API

**Files:**

- Modify: `Assets/Scripts/Game/Data/Effects/SpawnProjectileEffectData.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/SpawnProjectileEffect.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/SkillProjectileRuntime.cs`
- Modify: `Assets/Scripts/Game/Unit/System/SkillProjectileSpawnSystem.cs`
- Modify: `Assets/Scripts/Game/Unit/System/SkillProjectileSystem.cs`
- Modify: `Assets/Tests/Editor/Skill/EffectConditionUtilityTests.cs`
- Modify: `cs_review_status.txt`

**Interfaces:**

- Consumes: `SpawnProjectileEffectData.CollisionTargetConditions`, the spawn request/payload, `SkillContent`, and projectile collision candidates.
- Produces: an immutable runtime projectile payload that carries collision conditions and filters each candidate through `EffectConditionUtility`.

- [ ] Extend the previous test fixture first to cover an empty collision condition list (accept the first valid non-self hit) and an enemy-only list (skip an allied nearer hit, select the closest qualifying enemy). Confirm the second test fails before payload propagation and filtering are implemented.

- [ ] Add `List<ConditionConfig> CollisionTargetConditions = new()` to `SpawnProjectileEffectData`, and make `CreateRuntimeCopy` clone that list just as the base effect clones `Conditions`.

- [ ] Copy the list without aliasing through `SpawnProjectileEffect`, `SkillProjectileSpawnRequest`, `SkillProjectilePayloadComponent`, and `SkillProjectileSpawnSystem.ApplyPayloadComponent`. Preserve the projectile's execution context so `effect.context.originEntity` stays available during collision evaluation.

- [ ] In `SkillProjectileSystem.TryFindHitEntity`, retain the non-self check and geometric nearest-hit ordering; for each candidate, skip it when collision conditions fail and continue searching. A projectile must only execute its normal hit/destruction path after a qualifying candidate is found.

- [ ] Inspect the user-modified `SkillProjectileSystem.cs` diff immediately before patching and merge only the condition-filtering hunk. Run the focused tests and Unity playmode verification with one ally and one enemy placed along a Fireball path.

### Task 4: Replace all legacy condition editing with a shared expression editor

**Files:**

- Create: `Assets/Scripts/Game/Skill/Editor/EffectConditionSourceSchema.cs`
- Create: `Assets/Scripts/Game/Comparator/Editor/ConditionListEditor.cs`
- Modify: `Assets/Scripts/Game/Unit/Editor/StateScriptValueExpressionDrawer.cs`
- Modify: `Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs`
- Modify: `Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs`
- Modify: `Assets/Scripts/Game/Data/Editor/PropEditorWindow.cs`
- Modify: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphInspector.cs`
- Modify: `cs_review_status.txt`

**Interfaces:**

- Consumes: `ConditionConfig`, `StateScriptValueExpressionDrawer`, `UnitComponentSourceRegistry.Sources`, and `EffectConditionUtility` context-key constants.
- Produces: one reusable IMGUI list control that can add, remove, and edit full expression conditions for all effect-bearing data.

- [ ] Add an editor-focused test or a serialized-data fixture first that proves a `ConditionConfig` with `ConditionType.Unallowed` and a nested getter expression survives drawing/editing without resetting either field. This guards the prior State Script drawer behavior that forces conditions to `Necessary`.

- [ ] Build an effect-only source schema by describing every registered UnitSource and adding the five `effect.context.*` getters with their exact return types. Keep that schema separate from State Script and Behavior Tree source schemas.

- [ ] Implement the shared list drawer around `StateScriptValueExpressionDrawer.DrawCondition`: show the per-row necessary/unallowed selector, comparison type, typed inputs, nested operations/getters, plus add/remove controls. The drawer must mark the owning data editor dirty when a row changes.

- [ ] Refactor Skill, Buff, and Prop editor windows to call this list drawer for effect conditions and remove their old source-type arrays, scalar source parameter fields, compare-value widgets, and legacy row helpers.

- [ ] Teach `EffectGraphInspector.DrawValue` to recognize `List<ConditionConfig>` so `AreaSearchEffectData.TargetConditions` and `SpawnProjectileEffectData.CollisionTargetConditions` are editable in the graph inspector instead of rendered as an opaque label. Reuse the shared drawer rather than introducing a graph-only condition UI.

- [ ] In Unity, author and save the same enemy condition in the Skill editor, Buff editor, Prop editor, and Effect Graph inspector. Reopen each asset/table and verify that the condition remains expression-only, preserving `Unallowed` when selected.

### Task 5: Migrate configuration data and wire Fireball's search and collision conditions

**Files:**

- Modify: `Assets/Res/Data/SkillDataTable.json`
- Modify: `Assets/Res/Data/StateScriptDataTable.json`
- Modify: `Assets/Scripts/Game/Unit/UnitComponentInventory.md`

**Interfaces:**

- Consumes: expression condition JSON generated by the editors, `unit.faction.isEnemyTo`, and `effect.context.originEntity`.
- Produces: configuration tables with no legacy condition fields and a Fireball whose AreaSearch, Damage, and projectile collision all use the same enemy expression.

- [ ] Use the newly refactored Skill editor to create the Fireball expression `unit.faction.isEnemyTo(effect.context.originEntity)` for its area-search target conditions, damage effect conditions, and `CollisionTargetConditions`. This avoids guessing serialized `UnitValue` literal shape and ensures the JSON schema is exactly what the editor/runtime expect.

- [ ] Remove the legacy condition fields from all State Script rows using a narrowly scoped mechanical JSON edit: remove only `SourceType`, `SourceParam`, and `CompareValue`; preserve every existing `Inputs` expression and every unrelated row/value.

- [ ] Update `UnitComponentInventory.md` to state that scalar `ISource` is removed and effect conditions use the UnitSource/expression path. Do not claim the effect-context keys are usable by State Script or Behavior Tree.

- [ ] Run `rg -n '"(SourceType|SourceParam|CompareValue)"' Assets\Res\Data` and require an empty result. Use JSON parsing/format validation for both edited tables before launching Unity.

- [ ] Manually cast Fireball at a line containing caster/ally/enemy: the projectile must ignore caster and allies with the configured expression, hit an enemy, then its destroy-area search/damage must select enemies only. Verify empty collision filters on an additional temporary projectile still use old non-self behavior.

### Task 6: Regenerate, verify, and record review state

**Files:**

- Modify: all changed C# file entries in `cs_review_status.txt`
- Review: `Assets/Scripts/Game/Comparator/ComparatorRegistry.cs`
- Review: `Assets/Res/Data/SkillDataTable.json`
- Review: `Assets/Res/Data/StateScriptDataTable.json`

**Interfaces:**

- Consumes: completed runtime/editor/config migration.
- Produces: a buildable workspace with no legacy condition implementation or serialized fields.

- [ ] Regenerate both applicable registries from Unity: `Tools/Registry/Comparator` after deleting `ISource` implementations, and `Tools/Registry/Unit Component Source` after adding the faction getter. Review the generated changes rather than manually editing generated files.

- [ ] Run Unity EditMode tests for `ConditionExpressionMigrationTests` and `EffectConditionUtilityTests`, then run `dotnet build Crystal-Magic.sln --no-restore`. Address failures caused by this migration only; report unrelated existing failures separately.

- [ ] Perform final static checks:

  ```powershell
  rg -n --glob '*.cs' 'ISource|SourceContext|SourceType|SourceParam|CompareValue|RegisterSource|CreateSource|TryBuildLegacyCondition' Assets
  rg -n '"(SourceType|SourceParam|CompareValue)"' Assets\Res\Data
  ```

  Both commands must produce no migration-related matches. Confirm no orphaned legacy source `.cs.meta` files remain.

- [ ] Update every touched entry in `cs_review_status.txt` exactly once, preserving existing `DIRTY` flags on files that were already user-modified. Review `git diff --check` and `git diff` to confirm the diff contains only this migration plus the pre-existing user work.
