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
not a replacement for all ECS components. Every unit always owns and accesses
its own `UnitVariableElement` buffer. `UnitVariableComponent.Other` only identifies
the second unit available to an expression; it never redirects local storage. Health, movement, control, buff lists, rendering data,
and other high-frequency or strongly structured data must remain in their
dedicated components.

## Source Contract

There should be three data scopes. This prevents StateScript variables from
becoming a second copy of every unit component.

| Scope | Owner | Read/write policy | Examples |
| --- | --- | --- | --- |
| `unit.*` | Existing ECS component | The component Source explicitly declares each read/write permission | `unit.vitality.currentHealthPercentage`, `unit.perception.targetDistance`, `unit.move.setDirection(...)` |
| `var.*` | Each unit's `UnitVariableElement` buffer | Read/write state for BT, StateScript, and gameplay systems; each Source expression explicitly chooses `Self` or `Other` | `var.input.castHeld`, `var.cooldown.shieldSlam`, `var.animation.clip` |
| `script.*` | One running StateScript graph | Local graph state; never used as cross-graph communication | local timer, local branch flag, temporary loop counter |

Conditions use typed `UnitSource` value expressions. They support booleans,
numbers, entities, `float2`, `float3`, strings, and nested operations rather
than flattening values to a scalar. StateScript action nodes therefore
also need typed component Sources, for example `UnitPerceptionSource` can give
the target entity and target position directly. Do not force all data through
a float expression API.

The variable component supports number, bool, `float2`, `float3`, `Entity`,
and string. Runtime keys and string values use `FixedString128Bytes`; oversized
authored values fail binding or assignment instead of introducing managed
strings into the hot path.

## Behavior Tree Source Binding Plan

### Ownership And Placement

Each exposed unit component has one static Source provider, normally beside
that component's Authoring/Baker definition. Provider methods are marked with
`UnitSourceGet` or `UnitSourceSet`; the editor generator discovers those
attributes and emits the shared `UnitSourceId`, schema registry, and switch
dispatcher.

The shared Source files are:

- Source provider/get/set attributes
- `UnitValue` for managed graph data and `UnitSourceValue` for unmanaged dispatch
- `UnitSourceResolver`, the managed adapter used by current graph runtimes
- the generated Source registry and `UnitSourceDispatcher`

Each component Source has two responsibilities:

1. Describe its parameterized `Get` and `Set` functions for the editor. Every
   function declares a fixed return type (for `Get`) and fixed input count and
   input types.
2. Implement the real static getter or setter against an ECS component or any
   number of `ComponentLookup` / `BufferLookup` parameters.

Sources decide their own permissions. A field with no registered setter is
read-only. Structured data is not exposed as a collection type: for example,
buff queries are `Get` functions such as `unit.buffs.getCount()`, while
`add(...)`, `remove(...)`, and `clear()` are `Set` functions. Behavior Tree
and StateScript never access an ECS component directly.

### Generated Runtime Dispatch

There is no Source component, dictionary, or binding callback on each unit.
`UnitSourceDispatcherSystem` owns one dispatcher per World and refreshes its
lookup values. A call resolves its authored string key to a generated
`UnitSourceId`, then the generated switch calls the provider with the target
`Entity` and arguments. There is no managed runtime fallback. Providers that
still take `EntityManager` or expose a managed component remain in the schema
but return `false` until their data access is migrated to lookups.

Behavior Tree and StateScript keep a lightweight `UnitSourceResolver` containing
an unmanaged `UnitSourceContext` (`Self` and `Other`) plus the current dispatcher. Every
generated getter instruction and setter node explicitly selects one of those two entities. Comparators use
the same generated `Get` schema to create input ports. Reflection is restricted
to editor generation and is absent from the runtime dispatch path.

`UnitVariableComponent` is exposed by `UnitVariableSource` using the same
table. It provides typed `get*`, `set`, `has`, `remove`, and `clear` functions;
the variable name is its string parameter, normally using the `var.*` naming
convention. Behavior-tree-local data keeps a separate local scope and is not
copied into the unit variable component.

`UnitVariableComponent` contains only the optional `Other` entity. Local values live in
`UnitVariableElement`; referenced units also maintain `UnitVariableConsumerElement` so
consumer-count queries do not scan every unit. Selecting `Other` changes the entity passed
to the normal generated Source dispatcher, so expressions can combine both units' variables
and ordinary components without copying data. `WorldVariableComponent` uses
the same unmanaged buffer pattern through `WorldVariableElement`, and the
generated dispatcher routes global sources to the `WorldStateComponent`
singleton instead of the evaluated unit.

### Behavior Tree Runtime Changes

`BehaviorBlackboard` stops owning hard-coded `Sense` and `Intent` structures.
It keeps only the entity/runtime context, its `UnitSourceResolver`, the
per-tick collected-value snapshot, local behavior-tree state, and debug data.

`BehaviorTreeSystem` changes from:

1. Manually copying transform and one auto-selected perception target to Sense.
2. Ticking the tree.
3. Copying Intent to `UnitIntentComponent`.

to:

1. Refresh the unit resolver with the World's current generated dispatcher.
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
Each condition compiles its configured getter/literal/operation tree into an
unmanaged postfix instruction program during graph binding. The tick evaluates
that program with `UnitSourceValue`, fixed-capacity instruction/literal lists,
and a fixed-capacity value stack; it creates no delegates, source wrappers,
arrays, Comparators, or reflection data. `ValueExpression`, `ConditionConfig`,
factories, and editor schemas remain managed configuration/binding data, while
`CompiledValueExpression` and `Comparator` are the Burst/job-safe runtime form.

The old scalar `ISource`/`ComparatorFactory.RegisterSource` path has been
removed. Behavior-tree, StateScript, and effect conditions all compile typed
UnitSource expressions through `ComparatorFactory`.

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

1. **Initialization**: query registration and buff initialization,
   behavior-tree construction, and the old state-machine construction.
2. **Decision**: death is evaluated first; perception refreshes target data;
   Behavior Tree and player input produce commands; control refreshes locks;
   skill cooldown and availability refresh; the old state transition and state
   machine run last.
3. **Execution**: stat recovery, old skill analysis and cast execution,
   effects/projectiles, movement, animation, then death-finalization.
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
| `UnitFactionComponent` | Universal unit identity: `Player`, `Friend`, `Enemy`, `Boss`, or `Npc`. | `UnitFactionSource`: exposes faction and relation queries. | Keep. Every unit has exactly one faction identity. |

### Perception, Control, And Movement

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitPerceptionComponent` + `DynamicBuffer<UnitPerceptionEntityElement>` | Search radius plus all unit entities within that radius. It does not select a target. | `UnitPerceptionSource` exposes search radius and count; `UnitPerceptionUtility` exposes all units, all enemies, all friendlies, nearest enemy/friendly, and distance queries. | Keep, but redesign. The buffer contains only `Entity` values. Faction, transform, distance, and ordering are calculated on demand from the authoritative components. A locked-target skill explicitly writes its chosen entity to `var.skill.lockedTarget`. |
| `UnitControlRuntimeComponent` | Active stun, knockback, fear, movement/cast locks, and interruption data. | `UnitControlSource`: exposes every entry field and every resolved active-control field. | Keep. Its application can stop a StateScript graph through a dedicated interrupt action. |
| `UnitMoveComponent` | Direction command, StateScript movement multiplier, velocity, speed, and acceleration. | `UnitMoveSource`: exposes every stored field and every calculated movement value. | Keep. StateScript writes direction and multiplier independently; `UnitMoveSystem` remains the only integrator. |
| `UnitNavigationComponent` + `DynamicBuffer<UnitNavigationPathElement>` | Destination, grid-path state, collider-derived clearance radius, and current A* waypoints. | `UnitNavigationSource` exposes destination and stop commands. | Keep. It produces preferred movement direction but never integrates position. |
| `UnitAvoidanceComponent` | ORCA neighbor settings plus the safe velocity resolved for the current frame. | Internal to `UnitAvoidanceSystem`; behavior graphs continue writing navigation intent. | Keep. Agent snapshots, neighbor collection, and ORCA solving now run as two Burst jobs over unmanaged data; ECS remains the position authority. |
| `UnitFacingComponent` | The current facing direction. | `UnitFacingSource`: exposes the complete direction value and scalar projections/angle. | Keep. Graph actions set facing explicitly when a skill needs it. |

### Decisions And Skill Metadata

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitBehaviorTreeComponent` + `DynamicBuffer<BehaviorNodeStateElement>` | Unmanaged tree binding, current debug node, tick status, and per-node runtime state; immutable node definitions and expressions are shared through a Blob registry. | `UnitSourceDispatcher` reads source data through read-only lookups; behavior actions emit source or navigation commands that are applied before navigation and StateScript. | Keep. The editor `BehaviorNodeData` graph is compiled once, while `BehaviorTreeSystem` runs a Burst switch interpreter without per-unit managed node objects. |
| `PlayerSkillComponent` | Old runtime copy of the selected player skill chain and its current index. | Player loadout/skill-chain data remains in save data or graph configuration. | Remove. StateScript owns an executing graph's local progress; no runtime chain component is needed. |
| `UnitIntentComponent` | Per-frame move, cast, target, interaction, and prop requests. | Replace old intent Sources with `UnitVariableSource` and direct component Sources. | Remove. Player input and BT should write namespaced variables such as `var.input.move`, `var.input.aim`, and `var.input.castPressed`; StateScript consumes them. Movement/skill actions then write the real components. |
| `UnitCastComponent` | Old prepared-cast state, cast phase timer, hook continuation, current skill id, interruption flags. | `UnitStateScriptSource`: graph running, graph name, cancellation state. | Remove. These fields belong to the StateScript graph runtime rather than a second phase state machine. |
| `UnitCastTaskPayloadComponent` | Old hook-task payload carrier. | None. | Remove with the hook/phase machine. A StateScript node owns its own task state. |
| `UnitCastSkillPayloadComponent` | Current resolved-skill snapshot carrier. | Typed `ActiveSkillExecutionSource` if external systems need the snapshot. | Remove as a cast-specific carrier. Keep the single resolved-skill snapshot concept inside `UnitStateScriptRuntimeComponent` or a narrowly scoped `UnitSkillExecutionRuntimeComponent`; it must not become free-form variables. |
| `UnitCastFollowupRuntimeComponent` | Old long-lived follow-up rule instances for the current chain. | None. | Remove. Followup becomes a temporary chain-lifecycle buff: add it when the chain graph starts and remove it when that graph ends, is cancelled, or the unit dies. |
| `PlayerSkillChainElement` + `PlayerSkillChainSlotElement` | Compact per-player chain ranges and slots. | `PlayerSkillChainSource`: chain, slot, and skill-definition queries use generated component/buffer lookups and the global skill-definition Blob; the pressed chain ID comes from `PlayerInputSource`. | Keep. Managed character/save data is converted only when the loadout is initialized or rebuilt. |
| `PlayerCurrentSkillComponent` | Unmanaged selected chain/slot pair plus pending request-local extra modifiers. | `PlayerCurrentSkillSource`: stored and derived skill values, slot changes, clears, and extra modifiers all use generated unmanaged lookups. | Keep. The stored pair is independent from the world's mutable selected chain, so an in-progress chain release remains stable. |
| `UnitSkillReleaseComponent` + `SkillReleaseRequest` | Unmanaged marker and dynamic request buffer written by StateScript and consumed by `SkillReleaseSystem`. | `unit.self.entity` uses the marker lookup; release payloads are not exposed as Sources. | Keep. All skill releases now pass through the same buffered execution path. |

### Buff, Death, And Destruction

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitBuffComponent` + `UnitBuffElement` | Unmanaged Buff marker plus one dynamic-buffer element per complete Buff instance. Runtime state stores lifetime, stacks, source, and a Blob definition index; the definition maps `BuffEffectType + Id` to property modifiers, skill modifiers, or triggered effects. | `UnitBuffSource`: expression-facing scalar queries over the runtime buffer. | Keep the runtime buffer unmanaged so lifecycle and trigger evaluation stay in the Burst job. |
| `UnitModifierComponent` | Unmanaged per-unit cache rebuilt after `UnitBuffSystem`; stores every resolved property channel plus the persistent skill-modifier snapshot. | Attribute readers use the cached values through `UnitModifierResolver`; Burst callers can use its data-only overloads. | Keep. Buff changes after the modifier phase set `ModifierDirty` and become visible on the next frame. |
| `UnitDeathComponent` | Enableable death flag. Damage enables it immediately; later systems skip the entity. | `UnitDeathSource`: exposes only whether the flag is enabled. | Keep. `UnitDeathFinalizeSystem` publishes `UnitDiedEvent`, enables `DestroyEntityFlag`, and the normal destroy system recycles the entity in the same frame. |
| `DestroyEntityFlag` | Enableable destroy marker. | `UnitDestroySource`: exposes whether the marker is enabled. | Keep. It is structural lifecycle data. |

### Visuals

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitAnimationComponent` | Requested animation name plus private playback time. | `unit.animation.name` and `unit.animation.setName`. | StateScript controls only the name; the animation system selects and samples the directional Unity AnimationClip. |

### Optional Unit Features

| Component | Current role | Source | Decision |
| --- | --- | --- | --- |
| `UnitDropComponent` | References drop data for a defeated unit. | No regular Source. | Keep only on units with a drop module. |
| `UnitInteractableComponent` | Common descriptor for drop, treasure, NPC, and future interaction targets. | `game.interaction.*` reads the singleton candidate; execution uses `GameInteractionSystem`. | Keep only on entities that can receive an interaction request. |
| Dungeon treasure/exit components | Environment interaction, not ordinary unit state. | Environment-specific Sources. | Keep outside the base-unit component set. |

## Supporting Global Components

These components are not attached to ordinary units. They are listed separately
because unit perception, interaction, or effect execution use them.

| Component | Current role | Source / access | Decision |
| --- | --- | --- | --- |
| `UnitQuerySingleton` + `UnitQueryEntry` buffers | Unmanaged references and sorted spatial buffers for living units and generic interactables. Perception, projectiles, shape-search effects, interaction selection, and ORCA neighbor collection query the same buffers. | `UnitQueryUtility` exposes lightweight buffer views; Burst jobs use cell-range binary search directly. | Keep. Collection and sorting are Burst jobs, with no managed runtime query component or native-container wrapper object. |
| `InteractionCandidateComponent` | World singleton holding the current front-end candidate descriptor and persistent-interaction flag. | `game.interaction.hasCandidate`, `game.interaction.candidateKind`, `game.interaction.candidateTarget`, `game.interaction.candidate`, and `world.interaction.isInteracting`; prompt UI only reads it. | Keep. It rejects new candidate selection and requests while a persistent interaction is active. |
| `PlayerSkillDefinitionRegistryComponent` | World-owned Blob containing sorted immutable skill metadata and editable modifier-channel minimum factors. | Player skill Sources use the global entity lookup and binary search by skill ID or modifier channel. | Keep. It avoids copying the complete skill table into every player entity. |
| `GameInteractionRequest` | World singleton request snapshot containing actor, target, and descriptor. | Written by `RequestInteraction` or other game systems; consumed only by `GameInteractionSystem`. | Keep. |
| `NPCInteractionRuntimeComponent` | Declared interaction request state: current target, requested target, pending flag. No current system reads or writes it. | None currently. | Delete. It is unused. |
| `EffectComponent` + `EffectEntry` + `EffectDataBridgeComponent` | Unmanaged world request buffer plus the managed handle bridge for polymorphic `EffectData`, legacy context references, and condition lists. Buff jobs and managed producers emit the same `EffectEntry` shape; only `EffectExecutionSystem` crosses the bridge. | Queue/bridge utilities only; not Sources. | Keep the execution boundary managed while all ECS queue and context data remains unmanaged. |
| `PersistentEffectQueueComponent` + `PersistentEffectRequest` | Unmanaged world marker and request buffer. `PersistentEffectData` and legacy context references travel by bridge handle. | Queue utility only; not a Source. | Keep. The system-local instance scheduler is still managed and can be replaced independently later. |
| `EntitySpawnRegistrySingleton` + prefab registry buffers | World registry mapping names to unit, projectile, drop, environment, and VFX prefab entities. | Spawn registry utility only; not a Source. | Keep unchanged. It is static spawn infrastructure, not unit state. |

## Independent Entity Components

These components belong to projectile, VFX, drop, or dungeon entities. They
are not attached to ordinary units, but are created by skills or used by unit
interaction systems.

| Component | Current role | Decision |
| --- | --- | --- |
| `SkillProjectileComponent` + `SkillProjectileHitEntityElement` + `SkillProjectilePayloadComponent` + `SkillProjectileVisualLinkComponent` | Projectile motion, hit history, unmanaged effect/context handles, and the link to its independent visual entity. Projectile movement is a Burst job; managed collision conditions are resolved only at the collision boundary. | Keep. Gameplay collision and destruction remain independent from visual playback. |
| `VfxArrivalComponent` | Unmanaged movement snapshot and arrival-effect request context. | Keep. `VfxArrivalSystem` advances it in a Burst job and emits an `EffectEntry` on arrival. |
| `SpriteEffectAnimationComponent` | Managed Enter/Loop/Exit clip playback state sampled into a SpriteRenderer companion. | Keep. Clips are frame sources only; no Animator is used at runtime. |
| `EffectVisualFollowComponent` | Makes a sprite effect follow an entity, preserving its last transform and then ending when the target disappears. | Keep. Used by follow effects and projectile visuals. |
| `UnitInteractableComponent` | Generic kind, ID, amount, variant, range, and availability for spawned drops and other targets. | Keep. |
| `DungeonMonsterSpawnComponent` | Dungeon region, squad, and boss identity for a spawned monster. | Keep unchanged. |
| `DungeonInterestPointComponent` | Unmanaged patrol-point settings. | Keep. Target discovery and patrol progress now live in state-script variables through `QueryUnits`; no dedicated per-frame interest-point system is required. |
| `TreasureComponent` + `DungeonTreasureCandidateItemElement` | Chest state and its generated candidate rewards. | Keep. |
| `DungeonExitComponent` | Dungeon exit region, target floor, room-clear requirement, and open state. | Keep unchanged. |

## New Components To Add

| Component | Responsibility | Notes |
| --- | --- | --- |
| `UnitVariableComponent` + `UnitVariableElement` + `UnitVariableConsumerElement` | Unmanaged per-unit blackboard plus an optional second-unit relationship. | The value buffer is always local. The component stores `Other`, and the referenced unit's consumer buffer tracks reverse relationships without a world scan. |
| `WorldVariableComponent` + `WorldVariableElement` | Unmanaged global blackboard on the WorldState singleton. | Uses the same typed source values as unit variables and is addressed through global generated-source dispatch. |
| `UnitStateScriptRuntimeComponent` | Managed graph instances, active graph status, cancellation, and graph-local `script.*` variables. | New. It replaces the old state machine and cast phase runtime. State `OnComplete` connections replace cast hooks. It may hold the current resolved-skill snapshot until an execution graph finishes. |

## First Variable Keys

Use namespaced keys so a graph cannot accidentally share an unrelated state.

| Key | Writer | Reader | Notes |
| --- | --- | --- | --- |
| `var.input.move` | Player input bridge | Player movement graph/action | `float2`; it replaces intent movement input, not `UnitMoveComponent`. |
| `var.input.aim` | Player input bridge | Position-targeted skill nodes | `float2`; it is real-time mouse position, not a locked target. |
| `var.input.castPressed` / `var.input.castHeld` | Player input bridge | Player graphs | Keep pressed and held distinct. |
| `var.cooldown.<skillKey>` | Skill completion/effect action | StateScript skill-start condition or a later BT condition | A shared cooldown can deliberately use one common key. |
| `unit.animation.name` | StateScript setter | UnitAnimationSystem | The exact animation name configured in the unit animation profile. |
| `var.ai.<custom>` | BT action | StateScript or other BT nodes | Use only for authored cross-system decisions, not perception copies. |

## Existing Source Migration

| Existing Source | Problem | Target |
| --- | --- | --- |
| `UnitWantToCastSource` | Reads old `UnitIntentComponent`. | Replace with `UnitVariableSource("input.castPressed")` or a typed input Source. |
| `UnitVelocitySource` | Its name says velocity but it reads old intent direction. | Change it to read `UnitMoveComponent.Velocity`; provide a separate input-direction Source if needed. |
| `UnitIsCastingSource` | Reads old `UnitCastComponent`. | Replace with `UnitStateScriptSource` that checks whether an ability graph is running. |
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
6. `UnitSkillComponent` and `UnitCastAvailabilityComponent` are removed.
   Remove `PlayerSkillComponent`, `UnitIntentComponent`, `UnitCastComponent`,
   `UnitCastFollowupRuntimeComponent`, and the old cast payload components only
   after no system queries them.

## Decisions Still Needed

- Whether the current resolved-skill snapshot lives inside
  `UnitStateScriptRuntimeComponent` or in a dedicated
  `UnitSkillExecutionRuntimeComponent`. It should have exactly one owner.
