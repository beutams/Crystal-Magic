# Skill Addition and ECS Migration Design

## Status and Scope

This is a one-time breaking migration. The existing `SkillAddition`, Followup,
and `SkillCastTask` configuration is discarded. The project will expose the new
code and editor model, but the existing `StateScriptDataTable.json` graph data
is deliberately left unchanged. No node is inserted into any graph.

Existing graphs continue to use the generic `RequestSkill` node and continue to
release ordinary skills. They cannot execute an Addition until a later manual
graph configuration introduces the new player-current-skill and Addition nodes.

The migration must not retain a compatibility path for old Followup, cast-task,
or aggregated skill-modifier state.

## Invariants

1. StateScript ports transmit pulses only; a skill context is never passed over
   a graph port.
2. Property Buff modifiers are resolved from the active Buff list at the point
   of use. No frame-wide aggregated property cache exists.
3. Addition modifiers belong only to one player-chain submission. They never
   become a Buff modifier or a unit-wide modifier.
4. Buff skill modifiers persist with the unit's active Buff state and are
   rebuilt for each release snapshot.
5. `SkillReleaseSystem` creates the final immutable `ResolvedSkillData`.
6. A generic request, monster request, passive request, or Buff Tick request
   always has an empty submitted-Addition modifier set.

## Target Release Flow

`RequestSkillActionNode` remains the generic node. It writes a raw
`SkillReleaseRequest`: skill ID, origin, origin transform/facing, and configured
target data. It always creates an empty `SubmittedAdditionModifiers` set. It no
longer captures an element or modifier snapshot.

`SubmitCurrentChainSkillActionNode` is the player-only node. It resolves the
selected current-chain skill ID, consumes a clone of that player's pending
Addition modifiers, and enqueues one raw request. It does not inspect Buffs or
resolve a skill.

`SkillReleaseSystem` removes requests from `UnitSkillReleaseComponent` and uses
`SkillReleaseSnapshotUtility.TryCreate` before calling `SkillReleaseUtility`.
The utility:

1. reads `SkillData` from the request skill ID;
2. builds persistent skill modifiers from the caster's current Buffs;
3. adds the request-owned `SubmittedAdditionModifiers`;
4. captures the caster's current resolved element values; and
5. calls `SkillResolver.Resolve` once.

`ResolvedSkillData` is immutable for that one execution. A later Effect in the
same chain can immediately affect live property reads by applying a Buff, but it
cannot modify that release's already-resolved effect chain.

## Player Chain and StateScript

Add `PlayerCurrentSkillAuthoring`, `PlayerCurrentSkillComponent`,
`PlayerCurrentSkillSource`, and `PlayerCurrentSkillUtility`.

`PlayerCurrentSkillComponent` is a managed ECS component because it owns a
`SkillModifierSet`:

```csharp
public int CurrentSlotIndex = -1;
public SkillModifierSet PendingAdditionModifiers = new();
```

Skill ID and Addition ID are derived from `WorldSkillDataComponent.CurrentChainId`
and that slot index. They are not copied into component fields. Changing the
slot clears pending Addition modifiers. Only `SubmitCurrentChainSkill` may
consume the pending set. The configured cleanup path calls `ClearCurrentSlot`.

Add two StateScript node types and refresh the generated StateScript registry:

- `AdditionStateScriptNode` is a state node whose data contains only
  `EventName`. It creates matching Addition actions on activation, ticks only
  actions that report `Running`, stops running actions on abort/stop, and never
  removes Buffs implicitly.
- `SubmitCurrentChainSkillActionNode` is an action node with `In` and `Out`.
  It submits the selected player-chain skill with the consumed local modifiers.

The source surface is limited to the selected slot getters plus a setter for
the slot and an append-only pending-modifier setter. No graph-visible clear or
consume operation is exposed.

No `StateScriptDataTable.json` node, edge, or expression is changed in this
migration. A player prefab must receive `PlayerCurrentSkillAuthoring` before a
future graph uses the new player-current-skill accessors.

## Addition Model

Replace the old `SkillAdditionData` fields (`Modifiers`, `FollowupEffects`,
`CastTasks`, and `EffectChain`) with callback data:

```csharp
public sealed class SkillAdditionData : DataRow
{
    public string NameKey;
    public string DescriptionKey;
    public string IconPath;
    public List<SkillAdditionCallbackData> Callbacks = new();
}
```

Each callback has an event name, conditions, and a serialized list of
`SkillAdditionActionData`. Runtime actions are created by a generated
factory/registry keyed by `[FactoryKey]`; no Addition type enum or central
behavior switch is introduced.

Initial actions are:

- `ModifyCurrentSkill`: resolves modifier expressions and appends local pending
  modifiers; it completes immediately.
- `SetSourceValue`: evaluates arguments and invokes a normal source setter.
- `ExecuteEffects`: executes ordinary `EffectData` through the normal effect
  execution path.
- `ReplayCurrentSkill`: schedules raw replay requests with an empty submitted
  Addition modifier set. A replay never redispatches Addition events.

`SkillAdditionEventDispatcher` collects callbacks in this order: the Addition
on the selected player-chain slot, then Addition IDs granted by active
`SkillAdditionGrantBuffData` Buffs. Conditions use the existing comparator
path. The dispatcher creates actions per event and retains no long-lived
Addition runtime object.

`SkillAdditionGrantBuffData` replaces the Followup hierarchy. A callback that
must consume stacks does so explicitly through an ordinary Buff-stack effect or
source setter.

## Buff and Attribute Resolution

`UnitBuffRuntimeEntry` remains the sole active-Buff store. It keeps Buff ID,
duration, stacks, source, definition-derived modifier entries, and trigger
entries. `UnitBuffUtility` becomes the only mutation API: apply, remove,
change-stack, and get-stack-count. Effects and Buff Source setters call it;
they do not mutate the list directly.

`UnitBuffSystem` only decrements durations, removes expired/zero-stack Buffs,
and queues periodic Buff effects. It no longer builds modifier sets or writes
attribute components.

Add `UnitModifierResolver`. It traverses active Buff entries to calculate each
property modifier with the current additive formula, and it creates a new
`SkillModifierSet` for each release. Public readers cover movement, health,
defense, attack, range, MP, regeneration, chant speed, element values, and the
existing damage-taken multiplier behavior.

Base component fields and equipment offsets stay on unit components. Buff
factor/bonus fields and `Real*` properties are removed from move, vitality,
attack, and mana components. `UnitElementComponent` holds only the static
base/equipment element contribution; Buff element power is added by the
resolver. `UnitResetSystem` must stop clearing element values, and the equipment
system must assign its element contribution instead of accumulating it every
frame.

Final-value unit Sources gain contextual getters so they can invoke the
resolver with the entity and `EntityManager`. Existing public Source keys remain
stable where practical, but their implementation reads the resolver. Raw
values, Buff counts, Buff stacks, and base values remain direct component
getters.

All gameplay and presentation consumers of `Real*` move to the resolver:
movement, recovery, damage, healing, mana restoration, health costs, health
ratio calculation, battle/character/property UI, training-dummy UI, and unit
runtime inspector drawers.

## Deleted Legacy Surface and Data

Remove the old runtime and editor systems completely:

- `SkillCastTaskData` and all cast-task execution hooks;
- `SkillFollowupEffectData`, Followup filters, consume rules, modifier rules,
  factories, registries, and runtime collections;
- `UnitSkillModifierRuntimeAuthoring`, component, source, and utility;
- the old modifier snapshot fields on `SkillReleaseRequest`;
- old Addition editor controls and legacy JSON fields.

Keep the `SkillAdditionDataTable` asset and registry entry, but replace its
contents with an empty `Rows` collection in the new schema. Do not migrate old
rows. Remove `CastTasks` and `FollowupEffects` from `SkillData` code and strip
only those obsolete keys from existing skill JSON rows; normal skill identity,
cost, and effect data remain intact.

## Implementation Order

1. Create raw-request and release-snapshot classes, move release-time
   resolution, and change generic `RequestSkill` to submit empty local Addition
   modifiers.
2. Add `UnitModifierResolver`; update all consuming systems, effects, Sources,
   UI, and editor runtime drawers; then remove the aggregation writes and
   obsolete component fields.
3. Add player-current-skill ECS ownership and the StateScript source/node
   runtime types. Do not edit StateScript JSON graph data.
4. Add Addition callback data, generated factories, dispatcher, actions, and
   grant-Buff data.
5. Replace the Addition data table with an empty new-schema table and update
   its editor.
6. Delete all legacy Followup/cast-task/runtime-modifier code and regenerate
   the StateScript registry.
7. Add and run Editor tests before deleting any temporary build compatibility.

## Verification

Editor tests must cover:

- generic requests contain empty submitted Addition modifiers;
- player current-slot selection clears pending modifiers and submit consumes
  them exactly once;
- an Addition modifier is isolated to its one request;
- Buff property modifiers are visible immediately to later Effects in one
  effect chain;
- Buff skill modifiers affect a newly created release snapshot but cannot alter
  an active resolved release;
- expiring/removing/changing Buff stacks changes resolver results immediately;
- empty/new Addition data tables load correctly and old runtime types are no
  longer referenced.

Manual smoke checks confirm that an untouched current StateScript graph still
casts its base skill and that a later manually configured Addition graph can
cast, abort, replay, and remove Buffs at the configured graph events.
