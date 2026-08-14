# Unit Component Inventory And Variable Migration

## Purpose

This document records the unit-side components that are actually added by
the individual unit Authoring components, in logical runtime order. It is the decision list for
moving the old skill flow to Behavior Tree + StateScript.

The proposed direction is valid:

- Each unit gets a shared `UnitVariableComponent` for authored runtime state.
- Behavior Tree and StateScript read component data through Sources instead of
  copying it into several Intent components.
- Behavior Tree writes shared variables or starts a StateScript graph.
- StateScript consumes shared variables and uses explicit action nodes to change
  movement, skills, animation, and other gameplay components.

The important boundary is that `UnitVariableComponent` is a shared blackboard,
not a replacement for all ECS components. Health, movement, control, buff
lists, rendering data, and other high-frequency or strongly structured data
must remain in their dedicated components.

## Source Contract

There should be three data scopes. This prevents StateScript variables from
becoming a second copy of every unit component.

| Scope | Owner | Read/write policy | Examples |
| --- | --- | --- | --- |
| `unit.*` | Existing ECS component | The component Source explicitly declares each read/write permission | `unit.vitality.currentHealthPercentage`, `unit.perception.targetDistance`, `unit.move.setTargetMovement(...)` |
| `var.*` | `UnitVariableComponent` | Shared read/write state for BT, StateScript, and gameplay systems | `var.input.castHeld`, `var.cooldown.shieldSlam`, `var.animation.clip` |
| `script.*` | One running StateScript graph | Local graph state; never used as cross-graph communication | local timer, local branch flag, temporary loop counter |

The existing `ISource.GetValue()` returns only `float`. It is appropriate for
CheckData expressions: booleans use `0/1`, and vectors expose scalar fields
such as `.x`, `.y`, and `.length`. It is not sufficient for target entities,
`float2`, skill snapshots, or buff lists. StateScript action nodes therefore
also need typed component Sources, for example `UnitPerceptionSource` can give
the target entity and target position directly. Do not force all data through
a float expression API.

The variable component supports number, bool, `float2`, `float3`, `Entity`,
and string. Variable-name strings are authored once and retained by compiled
nodes; runtime code must not create formatted keys during a per-frame path.

## Behavior Tree Source Binding Plan

### Ownership And Placement

Every retained unit component has exactly one `UnitComponentSource`
implementation. The implementation lives in the same `.cs` file as that
component's Authoring/Baker class. For example, `UnitMoveAuthoring.cs` contains
`UnitMoveComponent`, its Baker, and `UnitMoveComponentSource`; no separate
source-file hierarchy is needed.

Only the shared contracts and runtime tables are common files:

- `UnitComponentSource`
- `UnitValue` and its value-type metadata
- `UnitSourceAccessTable`
- Source schema/binding builders and the generated source registry

Each component Source has two responsibilities:

1. Describe its parameterized `Get` and `Set` functions for the editor. Every
   function declares a fixed return type (for `Get`) and fixed input count and
   input types.
2. During unit initialization, register the real per-entity getter, setter,
   delegates into that unit's access table.

Sources decide their own permissions. A field with no registered setter is
read-only. Structured data is not exposed as a collection type: for example,
buff queries are `Get` functions such as `unit.buffs.getCount()`, while
`add(...)`, `remove(...)`, and `clear()` are `Set` functions. Behavior Tree
and StateScript never access an ECS component directly.

### Per-Unit Runtime Table

Add `UnitSourceRuntimeComponent` as a managed component on every unit. It owns
one `UnitSourceAccessTable` for that entity. The table contains:

| Table | Purpose |
| --- | --- |
| `Gets` | `string -> UnitSourceGet`; includes return type, fixed input signature, and `UnitValue[] -> UnitValue` delegate |
| `Sets` | `string -> UnitSourceSet`; includes fixed input signature and `UnitValue[] -> bool` delegate |

The table is populated once when the unit initializes. Behavior Tree and
StateScript graph nodes store only string keys in data, but resolve those keys
to delegate references when their runtime instance is built. Comparators use
the same `Get` signature to create input ports. There is no reflection or
source discovery in the per-frame path.

`UnitVariableComponent` is exposed by `UnitVariableSource` using the same
table. It provides typed `get*`, `set`, `has`, `remove`, and `clear` functions;
the variable name is its string parameter, normally using the `var.*` naming
convention. Behavior-tree-local data keeps a separate local scope and is not
copied into the unit variable component.

### Behavior Tree Runtime Changes

`BehaviorBlackboard` stops owning hard-coded `Sense` and `Intent` structures.
It keeps only the entity/runtime context, its `UnitSourceAccessTable`, the
per-tick collected-value snapshot, local behavior-tree state, and debug data.

`BehaviorTreeSystem` changes from:

1. Manually copying transform and one auto-selected perception target to Sense.
2. Ticking the tree.
3. Copying Intent to `UnitIntentComponent`.

to:

1. Retrieve the unit's already-bound access table.
2. Run the fixed pre-tree collection phase for the getter keys compiled from
   this tree's conditions/expressions.
3. Tick the tree; setters and operations update authoritative components or
   `UnitVariableComponent` directly.

The collection phase is logically the requested information-collection node,
but it must be a fixed pre-tree stage rather than an ordinary graph branch. A
Selector must not be able to skip it. Setter/operation calls invalidate the
affected snapshot keys so a later condition in the same frame reads fresh data.

### Comparator Ownership

Behavior Tree continues to use `ComparatorFactory.BuildComparator()`. Getters
only provide typed values; Comparator owns comparison and value-operation rules.
Each condition compiles its configured getter/literal/operation inputs into
delegates during tree initialization. The behavior-tree tick then calls the
compiled Comparator without creating Sources, Comparators, or reflection data.

The old scalar `ISource`/`ComparatorFactory.RegisterSource` path must not be
used by new behavior-tree conditions. It may remain temporarily for unrelated
legacy users until the Comparator migration is complete.

### Editor Changes

The behavior-tree editor resolves its bound unit Prefab/UnitData first, then
asks every component Source in that Prefab's active Features to describe its
available keys. A node saves only those keys as strings, while the editor gives
searchable, typed selectors rather than free-text entry.

The editor validates at edit time:

- a getter/setter/operation key exists for the selected unit;
- the component Feature exists on that Prefab;
- a read-only key is not used as a Set target;
- operation parameter count and types match;
- no two Sources register the same key.

The current tree-to-Prefab lookup by unit name should be replaced with a stable
UnitData id or Prefab GUID before Source availability is used for validation.

### Node Migration

Add generic behavior-tree nodes for `Check`, `Set`, `Invoke`, and collection
access/iteration. `Check` delegates its expression to the compiled Comparator.
`Set` and `Invoke` use the bound Source delegates.

Replace the current fixed `MoveToTarget`, `CastToTarget`, and `Idle` nodes: they
depend on old Intent and the removed single-target perception fields.
`Wander` may remain as a stateful action, but writes through `UnitMoveSource`.
The current private-timer `Cooldown` decorator is removed or rewritten to use
the shared `var.cooldown.*` values, so cooldown state has one owner.

## Current Runtime Order

This is the logical order derived from the current system groups and explicit
`UpdateBefore` / `UpdateAfter` attributes. Systems without an explicit relation
inside the same group must not depend on their incidental order.

1. **Initialization**: query registration, stat recovery, buff initialization,
   behavior-tree construction, and the old state-machine construction.
2. **Decision**: death is evaluated first; perception refreshes target data;
   Behavior Tree and player input produce commands; control refreshes locks;
   skill cooldown and availability refresh; the old state transition and state
   machine run last.
3. **Execution**: old skill analysis and cast execution, effects/projectiles,
   movement, jump arc, animation, then death-finalization.
4. **Post process**: drops and entity destruction.

Target order after migration:

1. Perception, player input, and component Sources refresh their data.
2. Behavior Tree decides the high-level goal and writes `var.*` or starts a
   StateScript graph.
3. StateScript ticks active graphs and invokes explicit gameplay action nodes.
4. Movement, effects, physics, animation, death, and destruction consume their
   own dedicated components.

The old unit state machine has been removed. StateScript is now the only graph
runtime that will take over its former producer responsibilities.

## Unit Component Inventory

### Engine And Transform

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `LocalTransform` / `LocalToWorld` | Unit world position and transform result. | `UnitTransformSource` | Engine component. It is outside this unit-component keep/remove decision; do not mirror position into variables every frame. |
| Physics components and player `PhysicsMassOverride` | Physics body configuration and kinematic player setup. | Typed physics/motion Source only when needed. | Engine/physics component. It is outside this unit-component keep/remove decision. |

### Core Combat Attributes

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitVitalityComponent` | Current health, max-health formula, regeneration, defense formula. | `UnitVitalitySource`: exposes every stored and calculated field. | Keep. Buffs and damage write this component through explicit actions/systems. |
| `UnitManaComponent` | Current mana, max mana, and mana regeneration formula. | `UnitManaSource`: exposes every stored and calculated field. | Keep. Skill execution consumes mana directly; no `var.mana` duplicate. |
| `UnitAttackComponent` | Attack, range, action speed, chant speed, and their modifier formulas. | `UnitAttackSource`: exposes every stored and calculated field. | Keep. It is the canonical stat source. |
| `UnitElementComponent` | Water, fire, lightning, and wind values. | `UnitElementSource`: exposes every element field. | Keep. Buffs change element attributes here; skills read the resolved value here. |
| `UnitFactionComponent` | Universal unit identity: `Protagonist`, `Ally`, `Enemy`, `Npc`, or `Other`. | `UnitFactionSource`: exposes faction and relation queries. | Keep and expand. It replaces `PlayerTag`; every unit has exactly one faction identity. |

### Perception, Control, And Movement

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitPerceptionComponent` + `DynamicBuffer<UnitPerceptionEntityElement>` | Search radius plus all unit entities within that radius. It does not select a target. | `UnitPerceptionSource` exposes search radius and count; `UnitPerceptionUtility` exposes all units, all enemies, all friendlies, nearest enemy/friendly, and distance queries. | Keep, but redesign. The buffer contains only `Entity` values. Faction, transform, distance, and ordering are calculated on demand from the authoritative components. A locked-target skill explicitly writes its chosen entity to `var.skill.lockedTarget`. |
| `UnitControlRuntimeComponent` | Active stun, knockback, fear, movement/cast locks, and interruption data. | `UnitControlSource`: exposes every entry field and every resolved active-control field. | Keep. Its application can stop a StateScript graph through a dedicated interrupt action. |
| `UnitMoveComponent` | Desired movement command, velocity, speed, acceleration. | `UnitMoveSource`: exposes every stored field and every calculated movement value. | Keep. BT or StateScript uses a `SetMovement` action to write it; `UnitMoveSystem` remains the only integrator. |
| `UnitFacingComponent` | The current facing direction. | `UnitFacingSource`: exposes the complete direction value and scalar projections/angle. | Keep. Graph actions set facing explicitly when a skill needs it. |
| `UnitJumpArcComponent` | Runtime jump trajectory. | `UnitJumpArcSource`: exposes every stored field and calculated progress. | Keep as an optional ability component. A jump action owns its lifecycle. |

### Decisions And Skill Metadata

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitBehaviorTreeComponent` | Managed Behavior Tree runtime, blackboard, debug state. | Not decided yet. | Defer. Do not design or change it in this migration pass. |
| `PlayerTag` | Old player-only query marker. | None. | Remove. Player-only systems query `UnitFactionComponent.Value == Protagonist` instead. |
| `UnitSkillComponent` | Old runtime skill slots, cooldown, availability, and pending cast state. | Static skill ownership comes from unit data; StateScript graph nodes reference the skill data they execute. | Remove. Skills are data/configuration, not runtime unit state. Cooldowns use `var.cooldown.*`. |
| `PlayerSkillComponent` | Old runtime copy of the selected player skill chain and its current index. | Player loadout/skill-chain data remains in save data or graph configuration. | Remove. StateScript owns an executing graph's local progress; no runtime chain component is needed. |
| `UnitCastAvailabilityComponent` | Old cached castable slot indexes and `CanStartCast`. | A StateScript/skill node evaluates its own data, mana, control, target query, and `var.cooldown.*` when it starts. | Remove. The cache exists only for the old skill-slot and state-machine flow. |
| `UnitIntentComponent` | Per-frame move, cast, target, interaction, and prop requests. | Replace old intent Sources with `UnitVariableSource` and direct component Sources. | Remove. Player input and BT should write namespaced variables such as `var.input.move`, `var.input.aim`, and `var.input.castPressed`; StateScript consumes them. Movement/skill actions then write the real components. |
| `UnitCastComponent` | Old prepared-cast state, cast phase timer, hook continuation, current skill id, interruption flags. | `UnitStateScriptSource`: graph running, graph name, cancellation state. | Remove. These fields belong to the StateScript graph runtime rather than a second phase state machine. |
| `UnitCastTaskPayloadComponent` | Old hook-task payload carrier. | None. | Remove with the hook/phase machine. A StateScript node owns its own task state. |
| `UnitCastSkillPayloadComponent` | Current resolved-skill snapshot carrier. | Typed `ActiveSkillExecutionSource` if external systems need the snapshot. | Remove as a cast-specific carrier. Keep the single resolved-skill snapshot concept inside `UnitStateScriptRuntimeComponent` or a narrowly scoped `UnitSkillExecutionRuntimeComponent`; it must not become free-form variables. |
| `UnitCastFollowupRuntimeComponent` | Old long-lived follow-up rule instances for the current chain. | None. | Remove. Followup becomes a temporary chain-lifecycle buff: add it when the chain graph starts and remove it when that graph ends, is cancelled, or the unit dies. |
| `UnitSkillModifierRuntimeComponent` | Runtime skill modifier set produced by buffs/additions. | `UnitSkillModifierSource` for scalar condition-facing results when required. | Keep. It is part of resolved-skill calculation, not graph flow state. |

### Buff, Death, And Destruction

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitBuffRuntimeComponent` | Managed list of buff instances, stack counts, timers, triggers, and modifiers. | `UnitBuffSource`: expression-facing scalar queries plus typed access to every buff-instance field, modifier, trigger, and source field. | Keep. A variable such as `var.hasPoison` must not replace the authoritative buff list. Followup design is deferred. |
| `UnitDeathComponent` | Enableable death flag. Damage enables it immediately; later systems skip the entity. | `UnitDeathSource`: exposes only whether the flag is enabled. | Keep. `UnitDeathFinalizeSystem` publishes `UnitDiedEvent`, enables `DestroyEntityFlag`, and the normal destroy system recycles the entity in the same frame. |
| `DestroyEntityFlag` | Enableable destroy marker. | `UnitDestroySource`: exposes whether the marker is enabled. | Keep. It is structural lifecycle data. |

### Visuals

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitAnimationComponent` | Current clip/frame/timing and directional animation result. | `UnitAnimationSource`: finished, looping, current clip. | Keep as-is for now. Animation design will be redone later. |
| `UnitAnimationFrameUvMinProperty` | Material UV origin for the sprite frame. | No gameplay Source. | Keep as-is for now. Animation design will be redone later. |
| `UnitAnimationFrameUvSizeProperty` | Material UV size for the sprite frame. | No gameplay Source. | Keep as-is for now. Animation design will be redone later. |
| `UnitAnimationFrameWorldSizeProperty` | Material world size for the frame. | No gameplay Source. | Keep as-is for now. Animation design will be redone later. |
| `UnitAnimationFramePivotOffsetProperty` | Material pivot offset for the frame. | No gameplay Source. | Keep as-is for now. Animation design will be redone later. |
| `UnitAnimationOverlayColorProperty` / `UnitAnimationOverlayStrengthProperty` | Optional overlay visual material data. | No gameplay Source. | Keep as-is for now. Animation design will be redone later. |

### Optional Unit Features

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitDropComponent` | References drop data for a defeated unit. | No regular Source. | Keep only on units with a drop module. |
| `NPCTag` | Old NPC identity marker. | None. | Remove. NPC identity is `UnitFactionComponent.Value == Npc`. |
| `NPCInteractableComponent` | NPC interaction configuration. | `NPCInteractionSource` where a graph needs it. | Keep only on interactable NPCs. |
| Dungeon treasure/exit components | Environment interaction, not ordinary unit state. | Environment-specific Sources. | Keep outside the base-unit component set. |

## Supporting Global Components

These components are not attached to ordinary units. They are listed separately
because unit perception, interaction, or effect execution use them.

| Component | Current role | Source / access | Decision |
| --- | --- | --- | --- |
| `UnitQuerySingleton` + `UnitQueryRuntimeComponent` | World singleton that rebuilds spatial trees for all living entities with `UnitFactionComponent`, plus world drops. Perception, projectiles, and shape-search effects query it. | `UnitQueryUtility` query functions, not a per-unit Source. | Keep unchanged. It is the backing service for the redesigned perception buffer. |
| `PlayerInteractionRuntimeComponent` | World singleton holding the currently prompted player interaction target and kind: drop, treasure, or NPC. | Direct singleton access for interaction UI/systems. | Keep unchanged. It is not a component on the protagonist entity and should not become `UnitVariableComponent`. |
| `NPCInteractionRuntimeComponent` | Declared interaction request state: current target, requested target, pending flag. No current system reads or writes it. | None currently. | Delete. It is unused. |
| `PendingEffectExecutionQueueComponent` | World managed queue of effects that must execute in the execution phase. Buffs currently enqueue trigger effects here. | Queue utility only; not a Source. | Keep unchanged. It is not unit state. Hook-related fields require a separate later review after Hook removal. |
| `PersistentEffectQueueComponent` | World managed queue for persistent-effect requests. | Queue utility only; not a Source. | Keep unchanged. It is not unit state. |
| `EntitySpawnRegistrySingleton` + prefab registry buffers | World registry mapping names to unit, projectile, drop, environment, and VFX prefab entities. | Spawn registry utility only; not a Source. | Keep unchanged. It is static spawn infrastructure, not unit state. |

## Independent Entity Components

These components belong to projectile, VFX, drop, or dungeon entities. They
are not attached to ordinary units, but are created by skills or used by unit
interaction systems.

| Component | Current role | Decision |
| --- | --- | --- |
| `SkillProjectileComponent` + `SkillProjectileHitEntityElement` + `SkillProjectilePayloadComponent` | Projectile motion, hit history, destroy state, and managed visual/effect payload. | Keep unchanged. |
| `QuadAnimationComponent` + `QuadAnimationVisualComponent` | Generic frame-animation runtime and managed visual resource for projectile/VFX quads. | Keep unchanged. |
| `FollowEntityComponent` | Makes an effect quad follow an entity, with offset and optional rotation alignment. | Keep unchanged. |
| `QuadOverlayPulseComponent` | Timed material overlay pulse on a quad. | Keep unchanged. |
| `WorldDropComponent` | Drop type, item id, and amount for a spawned world drop. | Keep unchanged. |
| `DungeonMonsterSpawnComponent` | Dungeon region, squad, and boss identity for a spawned monster. | Keep unchanged. |
| `DungeonTreasureComponent` + `DungeonTreasureCandidateItemElement` | Dungeon chest state and its generated candidate rewards. | Keep unchanged. |
| `DungeonExitComponent` | Dungeon exit region, target floor, room-clear requirement, and open state. | Keep unchanged. |

## New Components To Add

| Component | Responsibility | Notes |
| --- | --- | --- |
| `UnitVariableComponent` | Shared unit blackboard for authored runtime variables. | New. It holds only `var.*`, not component mirrors or graph-local temporaries. |
| `UnitSourceRuntimeComponent` | Per-unit managed owner of the Source access table and compiled access delegates. | New. It is initialized once and shared by Behavior Tree, StateScript, and Comparator binding. |
| `UnitStateScriptRuntimeComponent` | Managed graph instances, active graph status, cancellation, and graph-local `script.*` variables. | New. It replaces the old state machine and cast phase runtime. State `OnComplete` connections replace cast hooks. It may hold the current resolved-skill snapshot until an execution graph finishes. |

## First Variable Keys

Use namespaced keys so a graph cannot accidentally share an unrelated state.

| Key | Writer | Reader | Notes |
| --- | --- | --- | --- |
| `var.input.move` | Player input bridge | Player movement graph/action | `float2`; it replaces intent movement input, not `UnitMoveComponent`. |
| `var.input.aim` | Player input bridge | Position-targeted skill nodes | `float2`; it is real-time mouse position, not a locked target. |
| `var.input.castPressed` / `var.input.castHeld` | Player input bridge | Player graphs | Keep pressed and held distinct. |
| `var.cooldown.<skillKey>` | Skill completion/effect action | StateScript skill-start condition or a later BT condition | A shared cooldown can deliberately use one common key. |
| `var.animation.state` | BT or StateScript action | Future animation system | Reserved for the animation redesign. |
| `var.animation.clip` | StateScript skill action | Animation system | Empty means use the state default animation. |
| `var.ai.<custom>` | BT action | StateScript or other BT nodes | Use only for authored cross-system decisions, not perception copies. |

## Existing Source Migration

| Existing Source | Problem | Target |
| --- | --- | --- |
| `UnitWantToCastSource` | Reads old `UnitIntentComponent`. | Replace with `UnitVariableSource("input.castPressed")` or a typed input Source. |
| `UnitVelocitySource` | Its name says velocity but it reads old intent direction. | Change it to read `UnitMoveComponent.Velocity`; provide a separate input-direction Source if needed. |
| `UnitIsCastingSource` | Reads old `UnitCastComponent`. | Replace with `UnitStateScriptSource` that checks whether an ability graph is running. |
| `UnitCanStartCastSource` | Reads the old availability cache. | Remove. A StateScript skill node evaluates its own start conditions from authoritative Sources and `var.cooldown.*`. |
| `UnitHasTargetSource`, `UnitTargetCastRangeMarginSource` | Depend on the old single-target `UnitPerceptionComponent` fields. | Remove. Replace them with explicit perception utility queries, such as `HasEnemyInRange`, `GetNearestEnemy`, and `GetDistance`. |
| `UnitHealthRatioSource`, `UnitIsControlledSource`, `UnitBuffStackSource` | Already read authoritative component data. | Keep the pattern, but group them under their component-specific Sources and expose all relevant properties. |

## Chain Followup And Skill Sequence

The old Hook system is removed. A skill graph expresses sequence directly:

1. A chant/windup state finishes.
2. Its `OnComplete` output runs the next action, such as resolving a skill,
   applying an effect, starting recovery, or starting the next chain skill.
3. The graph reaches its own complete output or is cancelled. No external
   Hook point, Hook continuation, or task payload is required.

Followup is not a cast runtime component. Treat it as a temporary buff owned by
one StateScript chain run:

1. The chain graph starts and adds its configured followup buff or buffs.
2. Those buffs participate in the ordinary buff/modifier/passive pipeline.
3. `UnitStateScriptRuntimeComponent` records the exact buff-instance handles it
   created for that graph run.
4. Graph completion, graph cancellation, death, and forced interruption remove
   only those recorded instances. An unrelated persistent buff with the same
   buff id must not be removed.

This makes a followup an ordinary gameplay effect with a clear lifetime instead
of a special second skill-flow system.

## Migration Sequence

1. Add the two new runtime components and Source registry without removing any
   old behavior.
2. Move player input command handoff from `UnitIntentComponent` to `var.input.*`
   and explicit movement/skill actions. Behavior Tree migration is deferred.
3. Move per-skill cooldowns to `var.cooldown.*`; make each StateScript skill
   node evaluate its own start conditions from skill data and Sources.
4. Move one simple monster to a StateScript graph. Its graph writes animation
   commands, uses state `OnComplete` outputs for sequential skill actions, and
   completes/cancels itself. Do not recreate Hook points.
5. Make death/control cancel the active graph; remove all old state-machine
   dependencies from death and animation.
6. Remove `PlayerTag`, `UnitSkillComponent`, `PlayerSkillComponent`,
   `UnitIntentComponent`, `UnitCastAvailabilityComponent`,
   `UnitCastComponent`,
   `UnitCastFollowupRuntimeComponent`, and the old cast payload components only
   after no system queries them.

## Decisions Still Needed

- Whether the current resolved-skill snapshot lives inside
  `UnitStateScriptRuntimeComponent` or in a dedicated
  `UnitSkillExecutionRuntimeComponent`. It should have exactly one owner.
