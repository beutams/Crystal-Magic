# Effect Graph Editor Design

## Goal

Replace every editor-facing `EffectData[]` list with one consistent GraphView editor. An effect graph makes array ownership and nested effect branches visible while preserving the existing runtime `EffectData` model and JSON exactly.

The graph uses a container for every `EffectData[]`. Containers show their child effects as an ordered left-to-right row; users drag an effect within a container to insert it at a new execution position, or drag it into another container to move it between arrays.

## Non-goals

- Do not add GUIDs, graph edges, positions, or editor-only fields to `EffectData` or to any gameplay JSON.
- Do not change `SkillExecutor`, effect implementations, runtime copies, ECS systems, or effect execution semantics.
- Do not support loops, merged branches, or one effect being owned by more than one array.

## Existing data remains authoritative

The existing data remains the only serialized gameplay model:

- `SkillData.EffectChain`
- `PropData.EffectChain`
- `BuffTriggerEntry.Effects`
- `ExecuteEffectsSkillAdditionActionData.Effects`
- every nested public `EffectData[]` field on an effect data class

The editor graph is a projection of those arrays. Adding, deleting, moving, or editing a node immediately mutates the same in-memory `EffectData[]`; the host editor's existing `Save` action remains responsible for writing its own table JSON.

## Graph model and visual structure

### Effect-array containers

Each `EffectData[]` is represented by exactly one non-deletable `EffectArrayContainerView`.

- The root array is reached from a single `Entry` node via one edge to the root container.
- An effect field such as `OnAfterSearch`, `OnCollisionEffects`, `OnTickEffects`, or `OnEndEffects` has one named bottom output. It connects to exactly one dedicated container.
- A container has a top input from its owner and vertically oriented child connections below it.
- The container owns a horizontal row of direct child effect nodes. Its child order, from left to right, is the exact order of the corresponding `EffectData[]`.

For example:

```text
Entry
  |
Root Effects container
  |-- Search --(Search After)--> Search After container -- Damage -- Knockback
  |-- Damage
  `-- Spawn VFX
```

The diagram's branches represent nested array ownership, not a new runtime control-flow language.

### Effect nodes

- Every `EffectData` instance has one node and one input.
- Nodes without nested `EffectData[]` fields, such as damage or healing, have no output ports.
- Nodes with nested arrays expose one named output per field. The display label comes from `[EditorLabel]` where present and otherwise from the field name.
- No Effect node is free-floating: it must always belong to exactly one container.

### Ordering and drag behavior

- Child nodes inside a container are laid out automatically in one horizontal row.
- Dragging over a container reveals an insertion indicator between children.
- Dropping inserts the effect at that index; the target array is rewritten in the same order immediately.
- Dropping in another container removes it from the source array before insertion into the target array.
- Free placement inside a container is intentionally disabled so visual layout can never diverge from execution order.
- Moving an effect into a container that belongs to that effect or one of its descendants is rejected, preventing cycles.

## Properties and editing

Selecting an Effect node shows its normal public fields and conditions in the right inspector panel.

- All `EffectData[]` fields are omitted from the property inspector because their containers are the only editing surface for child effects.
- Normal scalar, enum, object, list, and condition fields retain the existing editing behavior and serialization.
- Selecting a container shows its label, source field path, and effect count; the container itself exposes no gameplay properties.
- Right-clicking a container offers all registered effect types and inserts the selected type at the indicated position. Removing an Effect deletes it from its parent array, including any nested arrays it owns.

`SkillAdditionEditorWindow` currently allows raw row JSON editing. That must be replaced with a structured callback/action inspector. Each `ExecuteEffectsSkillAdditionActionData` action exposes an **Edit Effect Graph** affordance and never shows its `Effects` array in raw text. This closes the last route that could bypass the graph editor.

## Editor integration

A reusable `EffectGraphEditorWindow` opens an `EffectGraphSession` supplied by one host editor. A session contains:

- a stable owner key for editor layout;
- a root array getter/setter;
- a callback that marks the host editor dirty and repaints it;
- a user-facing owner label.

The four hosts open it for their selected data:

| Host | Root binding key |
| --- | --- |
| Skill editor | `Skill:{id}:EffectChain` |
| Prop editor | `Prop:{id}:EffectChain` |
| Buff editor | `Buff:{id}:TriggerEntries[{index}].Effects` |
| Skill addition editor | `SkillAddition:{id}:Callbacks[{callbackIndex}].Actions[{actionIndex}].Effects` |

Each nested container receives a deterministic path below that root, built from the containing effect's array index and field name. When a drag changes array order, the active session moves the associated layout record to the new path before persisting it.

The existing inline effect-chain editors in the Skill, Buff, and Prop editors are replaced by a concise graph entry point and effect count. No editor continues to expose an inline `EffectData[]` editor.

## Editor-only layout persistence

`Assets/Editor/EffectGraphLayouts.json` stores only graph presentation metadata:

- owner key and version;
- GraphView pan position and zoom;
- container path, position, and expanded state.

It never stores gameplay field values or execution links. Since it lives below an `Editor` directory, it is excluded from player builds while remaining available to source control. Stale layout entries are ignored and removed when their data path no longer exists.

Children do not store independent free-form positions: their container and array order determine their row positions. This makes insertion deterministic even after reopening the graph.

## Validation and error handling

Before returning control to a host editor and before writing layout metadata, validate that:

- one root container exists for the bound root array;
- each accessible `EffectData[]` field has one matching container;
- every Effect belongs to one container only;
- each container's children map in order to its backing array;
- no child move makes an ancestor/descendant cycle.

Malformed or legacy data is still displayable: null arrays normalize to empty arrays, null effects are skipped with an editor warning, and missing layout records use automatic placement. Validation failures reject the graph operation and keep the prior data unchanged.

## Implementation boundaries

Add a shared editor-only Effect Graph module near the skill editor code:

- session/binding and traversal model;
- effect type registry and reflected nested-array-port discovery;
- GraphView, Entry view, Effect node view, and container view;
- right-side inspector renderer;
- editor-only layout store.

Modify only editor code for Skill, Buff, Prop, and Skill Addition to open the shared graph and remove inline/raw effect editing. Runtime data classes and runtime systems remain unchanged.

## Verification

- Editor round-trip coverage for every current nested effect array type: root, search, projectile hit/destroy, persistent start/tick/end, random point, and buff-stack follow-up.
- Drag/reorder tests verify the resulting `EffectData[]` order exactly matches the container order.
- Move-between-container, deletion, empty-container, and cycle-rejection tests.
- Confirm an unchanged existing data file serializes back without gameplay schema changes.
- Build `Crystal Magic.sln` with zero errors and manually open graphs from all four host editors.
