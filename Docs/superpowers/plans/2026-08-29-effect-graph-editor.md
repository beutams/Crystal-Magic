# Effect Graph Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all editor-facing `EffectData[]` editing with a reusable GraphView editor that uses ordered effect-array containers while leaving gameplay JSON and runtime execution untouched.

**Architecture:** An editor-only graph session reflects the existing effect-object tree into one container per `EffectData[]`. Containers own a left-to-right ordered row of effect nodes, and drag insertion writes directly back to the corresponding array. A separate editor-only layout JSON persists graph presentation, while the existing Skill, Buff, Prop, and Skill Addition editors retain ownership of gameplay-table saving.

**Tech Stack:** Unity Editor, `UnityEditor.Experimental.GraphView`, UIElements, IMGUI editor controls, Newtonsoft.Json, NUnit Editor tests.

**Spec:** `Docs/superpowers/specs/2026-08-29-effect-graph-editor-design.md`

## Global Constraints

- Do not alter `EffectData`, any effect subclass, `SkillExecutor`, ECS systems, or gameplay JSON schemas.
- Use `Assets/Editor/EffectGraphLayouts.json` exclusively for presentation metadata; it must contain no effect field values or execution links.
- Every `EffectData[]` is represented by one non-deletable container, and effect order is the container's left-to-right child order.
- Effects may be moved only between containers; reject a move into the effect's own descendant container.
- Keep all new graph code editor-only and update `cs_review_status.txt` after reviewing modified or created C# files.

---

## File structure

- `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphBinding.cs` — host callback contract for a root effect array.
- `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphModel.cs` — reflected container tree, array mutation, and cycle validation.
- `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphTypeRegistry.cs` — one shared list of creatable effect types, labels, and colors.
- `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphLayoutStore.cs` — editor-only JSON DTOs and persistence for canvas/container presentation.
- `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphView.cs` — GraphView canvas, entry node, container/effect views, edges, auto-layout, and drag insertion.
- `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphWindow.cs` — reusable window, inspector panel, and host-session lifecycle.
- `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphInspector.cs` — reflected editor UI for normal Effect fields and conditions; excludes nested `EffectData[]` fields.
- `Assets/Editor/Tests/EffectGraph/EffectGraphModelTests.cs` — model, ordering, nested-array, and cycle tests.
- `Assets/Editor/Tests/EffectGraph/EffectGraphLayoutStoreTests.cs` — isolated layout serialization tests.
- `Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs` — replace inline effect-chain controls with graph entry point.
- `Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs` — replace per-trigger inline effect-chain controls with graph entry point.
- `Assets/Scripts/Game/Data/Editor/PropEditorWindow.cs` — replace inline effect-chain controls with graph entry point.
- `Assets/Scripts/Game/Data/Editor/SkillAdditionEditorWindow.cs` — replace editable row JSON with structured callback/action UI and graph entry point for Execute Effects actions.

### Task 1: Build the reflected effect-container model

**Files:**
- Create: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphBinding.cs`
- Create: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphModel.cs`
- Create: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphTypeRegistry.cs`
- Create: `Assets/Editor/Tests/EffectGraph/EffectGraphModelTests.cs`

**Interfaces:**
- Consumes: `CrystalMagic.Game.Data.Effects.EffectData`, its public nested `EffectData[]` fields, and host `Action` dirty callbacks.
- Produces: `EffectGraphBinding`, `EffectGraphModel`, `EffectGraphContainerModel`, and `EffectGraphTypeRegistry`, used by all graph views and hosts.

- [ ] **Step 1: Write the failing ordering and nested-container tests**

```csharp
[Test]
public void MoveEffect_InsertsAtRequestedContainerIndex()
{
    DamageEffectData first = new();
    HealEffectData second = new();
    EffectData[] root = { first, second };
    EffectGraphModel model = CreateModel(ref root);

    Assert.That(model.MoveEffect(model.Root, 1, model.Root, 0), Is.True);
    Assert.That(root, Is.EqualTo(new EffectData[] { second, first }));
}

[Test]
public void NestedEffectArray_CreatesNamedContainer()
{
    ForwardRectSearchEffectData search = new();
    EffectData[] root = { search };
    EffectGraphModel model = CreateModel(ref root);

    Assert.That(model.FindContainer("root/0/OnAfterSearch"), Is.Not.Null);
}
```

- [ ] **Step 2: Run the Editor tests and verify the model types are missing**

Run in Unity Test Runner: `EffectGraphModelTests` in EditMode.

Expected: compilation failure referencing missing `EffectGraphModel` and `EffectGraphBinding`.

- [ ] **Step 3: Implement the host binding contract and model**

```csharp
public sealed class EffectGraphBinding
{
    public EffectGraphBinding(
        string ownerKey,
        string displayName,
        Func<EffectData[]> getRootEffects,
        Action<EffectData[]> setRootEffects,
        Action notifyChanged) { /* assign non-null callbacks */ }

    public string OwnerKey { get; }
    public string DisplayName { get; }
    public EffectData[] GetRootEffects() => _getRootEffects() ?? Array.Empty<EffectData>();
    public void SetRootEffects(EffectData[] effects) => _setRootEffects(effects ?? Array.Empty<EffectData>());
    public void NotifyChanged() => _notifyChanged();
}

public sealed class EffectGraphModel
{
    public EffectGraphContainerModel Root { get; }
    public EffectGraphContainerModel FindContainer(string path);
    public bool MoveEffect(EffectGraphContainerModel source, int sourceIndex,
        EffectGraphContainerModel target, int targetIndex);
    public EffectData AddEffect(EffectGraphContainerModel target, Type effectType, int index);
    public bool RemoveEffect(EffectGraphContainerModel source, int index);
}
```

Implement `EffectGraphContainerModel` with a root getter/setter or an owner `EffectData` plus a reflected `FieldInfo`. Reflect only public instance fields whose exact element type is assignable to `EffectData`; generate nested paths as `root/{effectIndex}/{field.Name}`. `MoveEffect` must reject a target container discovered below the moved effect before mutating either array. Each successful mutation calls `EffectGraphBinding.NotifyChanged()`.

Move the three duplicated `KnownEffectTypes` / display-name / color arrays out of Skill, Buff, and Prop editors into `EffectGraphTypeRegistry`. Its `Create(Type)` method must reject non-`EffectData` or abstract types and otherwise use `Activator.CreateInstance`.

- [ ] **Step 4: Run the model tests and verify success**

Run in Unity Test Runner: `EffectGraphModelTests` in EditMode.

Expected: the ordering test, named nested-container test, add/remove test, and own-descendant rejection test all pass.

- [ ] **Step 5: Commit the isolated model task**

```powershell
git add -- Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphBinding.cs Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphModel.cs Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphTypeRegistry.cs Assets/Editor/Tests/EffectGraph/EffectGraphModelTests.cs
git commit -m "feat: add effect graph model"
```

### Task 2: Persist only editor graph layout

**Files:**
- Create: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphLayoutStore.cs`
- Create: `Assets/Editor/Tests/EffectGraph/EffectGraphLayoutStoreTests.cs`

**Interfaces:**
- Consumes: `EffectGraphBinding.OwnerKey`, container paths from `EffectGraphModel`, `Vector2`, and JSON file I/O.
- Produces: `EffectGraphLayoutStore.Load(ownerKey)`, `Save(ownerKey, layout)`, and `Prune(ownerKey, validPaths)` for `EffectGraphWindow`.

- [ ] **Step 1: Write a failing isolated layout round-trip test**

```csharp
[Test]
public void SaveAndLoad_PersistsOnlyPresentationForOwner()
{
    EffectGraphLayoutStore store = new(_temporaryLayoutPath);
    EffectGraphLayoutData expected = new()
    {
        ViewPosition = new Vector2(30f, 40f),
        ViewScale = 1.25f,
        Containers = { new EffectGraphContainerLayout { Path = "root/0/OnAfterSearch", Position = new Vector2(50f, 60f), Expanded = false } },
    };

    store.Save("Skill:7:EffectChain", expected);
    EffectGraphLayoutData actual = store.Load("Skill:7:EffectChain");

    Assert.That(actual.ViewScale, Is.EqualTo(1.25f));
    Assert.That(actual.Containers[0].Path, Is.EqualTo("root/0/OnAfterSearch"));
}
```

- [ ] **Step 2: Run the layout test and verify it fails because the store is missing**

Run in Unity Test Runner: `EffectGraphLayoutStoreTests` in EditMode.

Expected: compilation failure referencing missing layout DTOs and store.

- [ ] **Step 3: Implement versioned layout DTOs and store**

```csharp
public sealed class EffectGraphLayoutStore
{
    public const string DefaultPath = "Assets/Editor/EffectGraphLayouts.json";
    public EffectGraphLayoutStore(string path = DefaultPath) { /* normalize path */ }
    public EffectGraphLayoutData Load(string ownerKey);
    public void Save(string ownerKey, EffectGraphLayoutData layout);
    public void Prune(string ownerKey, ISet<string> validContainerPaths);
}

[Serializable]
public sealed class EffectGraphLayoutData
{
    public Vector2 ViewPosition;
    public float ViewScale = 1f;
    public List<EffectGraphContainerLayout> Containers = new();
}
```

Serialize a versioned top-level document with one entry per owner key. Persist only owner key, graph pan/zoom, container path, container position, and expanded state. `Load` returns a new default layout for absent or malformed entries. `Prune` removes paths that are no longer returned by `EffectGraphModel`; never inspect or serialize `EffectData` fields.

- [ ] **Step 4: Run the layout tests and verify success**

Run in Unity Test Runner: `EffectGraphLayoutStoreTests` in EditMode.

Expected: owner isolation, round-trip, missing-file default, and pruning tests pass.

- [ ] **Step 5: Commit the layout task**

```powershell
git add -- Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphLayoutStore.cs Assets/Editor/Tests/EffectGraph/EffectGraphLayoutStoreTests.cs
git commit -m "feat: persist effect graph layout"
```

### Task 3: Render and edit the container graph

**Files:**
- Create: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphView.cs`
- Create: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphWindow.cs`
- Create: `Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphInspector.cs`

**Interfaces:**
- Consumes: `EffectGraphBinding`, `EffectGraphModel`, `EffectGraphTypeRegistry`, and `EffectGraphLayoutStore` from Tasks 1–2.
- Produces: `EffectGraphWindow.Open(EffectGraphBinding)` for host editors.

- [ ] **Step 1: Add a manual GraphView smoke-test binding in the Editor test assembly**

```csharp
[Test]
public void GraphWindow_OpenWithEmptyRoot_CreatesEntryAndRootContainer()
{
    EffectData[] root = Array.Empty<EffectData>();
    EffectGraphBinding binding = CreateBinding(ref root);
    EffectGraphWindow window = EffectGraphWindow.Open(binding);

    Assert.That(window.GraphView.EntryView, Is.Not.Null);
    Assert.That(window.GraphView.RootContainerView, Is.Not.Null);
    window.Close();
}
```

- [ ] **Step 2: Run the smoke test and verify it fails before GraphView code exists**

Run in Unity Test Runner: `EffectGraphModelTests.GraphWindow_OpenWithEmptyRoot_CreatesEntryAndRootContainer`.

Expected: compilation failure referencing missing `EffectGraphWindow`.

- [ ] **Step 3: Implement GraphView views, graph rebuild, and inspector**

```csharp
public sealed class EffectGraphWindow : EditorWindow
{
    public static EffectGraphWindow Open(EffectGraphBinding binding);
    internal EffectGraphView GraphView { get; }
}

public sealed class EffectGraphView : GraphView
{
    public EffectGraphEntryView EntryView { get; }
    public EffectArrayContainerView RootContainerView { get; }
    public void Rebuild(EffectGraphModel model, EffectGraphLayoutData layout);
}
```

Build the canvas with zoom, content dragger, selection dragger, rectangle selector, and grid background, matching `StateScriptGraphView`. Create a fixed Entry node, a non-deletable container view for the root, a non-deletable container view for every reflected nested array, and one Effect node per non-null effect. Use vertical ports: owner bottom output to container top input, then container bottom output to direct child input. Do not add an output port to an Effect that has no reflected nested array field.

Within a container, assign each direct child a deterministic horizontal position based on its array index. Selection updates an IMGUI or UIElements inspector panel. The inspector draws public normal fields and `Conditions`, but skips fields recognized by `EffectGraphModel.IsNestedEffectArrayField`; it shows a small read-only label for those fields naming the corresponding container.

- [ ] **Step 4: Implement container insertion and deletion mutations**

```csharp
private void HandleEffectDropped(EffectNodeView dragged, EffectArrayContainerView target, int insertIndex)
{
    if (!_model.MoveEffect(dragged.Container, dragged.Index, target.Container, insertIndex))
        return;
    RebuildFromCurrentModel();
}
```

Render a highlighted insertion line while an Effect node is dragged across a container's child row. On drop, call `MoveEffect`; do not calculate execution order from free node coordinates. Add a contextual menu for the hovered container that calls `AddEffect(container, selectedType, insertionIndex)`. Delete removes the selected Effect from its parent container; deleting Entry or any container is ignored. Rebuild the graph after every structural mutation so ports, descendants, and node indices match source data.

- [ ] **Step 5: Persist viewport/container layout and reject invalid graph edits**

Save container locations, expanded state, pan, and zoom to `EffectGraphLayoutStore` whenever they change, then prune unknown paths after rebuild. Reject direct edge creation that is not one of the generated owner-to-container or container-to-child relationships; generated edges are view-only representations of model ownership. Before closing, normalize null arrays, skip null effects with a `Debug.LogWarning`, and leave unchanged data intact if a model validation error is reported.

- [ ] **Step 6: Run the smoke test and manually verify drag insertion**

Run in Unity Test Runner: the new window smoke test.

Manual check: open an empty graph; add Search, Damage, and Spawn VFX to the root container; drag Damage before Search; add Damage into Search's `OnAfterSearch` container; close and reopen. The root array order and nested array content must match the visible rows, and the viewport/container positions must persist.

- [ ] **Step 7: Commit the graph UI task**

```powershell
git add -- Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphView.cs Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphWindow.cs Assets/Scripts/Game/Skill/Editor/EffectGraph/EffectGraphInspector.cs Assets/Editor/Tests/EffectGraph/EffectGraphModelTests.cs
git commit -m "feat: add effect graph editor"
```

### Task 4: Connect Skill, Buff, and Prop editors

**Files:**
- Modify: `Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs:33-90,606-815`
- Modify: `Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs:77-130,712-960`
- Modify: `Assets/Scripts/Game/Data/Editor/PropEditorWindow.cs:27-70,313-525`
- Create: `Assets/Editor/Tests/EffectGraph/EffectGraphHostBindingTests.cs`

**Interfaces:**
- Consumes: `EffectGraphWindow.Open(EffectGraphBinding)` from Task 3.
- Produces: host-specific bindings that mark `_isDirty` and write their existing arrays unchanged during their normal `Save` workflow.

- [ ] **Step 1: Add failing host-binding tests for each root type**

```csharp
public void SkillBinding_UsesStableOwnerKeyAndWritesBack()
{
    SkillData skill = new() { Id = 3 };
    EffectGraphBinding binding = SkillEditorWindow.CreateEffectBinding(skill, () => { });

    binding.SetRootEffects(new EffectData[] { new DamageEffectData() });
    Assert.That(binding.OwnerKey, Is.EqualTo("Skill:3:EffectChain"));
    Assert.That(skill.EffectChain, Has.Length.EqualTo(1));
}
```

- [ ] **Step 2: Run the host-binding tests and verify the host factory methods are absent**

Run in Unity Test Runner: `EffectGraphHostBindingTests` in EditMode.

Expected: compilation failure referencing missing internal host binding factories.

- [ ] **Step 3: Replace inline chain rendering with graph entry points**

In each editor, delete its duplicated known-effect arrays and the `DrawEffectChainInline` / `DrawEffectEntry` methods. In the former chain section show the effect count and an `Edit Effect Graph` button. Add an `internal static EffectGraphBinding CreateEffectBinding(...)` method in each host so the production button and the EditMode test use the same binding factory. The Skill factory is:

```csharp
new EffectGraphBinding(
    $"Skill:{skill.Id}:EffectChain",
    $"Skill [{skill.Id}] {skill.NameKey}",
    () => skill.EffectChain,
    effects => skill.EffectChain = effects,
    () => { _isDirty = true; Repaint(); });
```

For Prop use `Prop:{row.Id}:EffectChain`. For a Buff trigger use `Buff:{buff.Id}:TriggerEntries[{triggerIndex}].Effects`; capture the selected `BuffTriggerEntry` list index and write the changed array back to that exact entry before setting `_isDirty`.

- [ ] **Step 4: Run tests and manually verify all three hosts**

Run in Unity Test Runner: `EffectGraphHostBindingTests` and prior Effect Graph tests.

Manual check: open one graph from each host, add/reorder an effect, click the host's Save button, reopen the editor, and verify the same effect order and values are loaded.

- [ ] **Step 5: Commit the three host integrations**

```powershell
git add -- Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs Assets/Scripts/Game/Data/Editor/PropEditorWindow.cs Assets/Editor/Tests/EffectGraph/EffectGraphHostBindingTests.cs
git commit -m "feat: open effect graph from data editors"
```

### Task 5: Replace Skill Addition raw JSON with structured callback/action editing

**Files:**
- Modify: `Assets/Scripts/Game/Data/Editor/SkillAdditionEditorWindow.cs:64-240`
- Create: `Assets/Editor/Tests/EffectGraph/SkillAdditionEffectGraphBindingTests.cs`

**Interfaces:**
- Consumes: `EffectGraphWindow.Open(EffectGraphBinding)` and `ExecuteEffectsSkillAdditionActionData`.
- Produces: structured callback/action list UI with an Effect Graph entry point for every Execute Effects action.

- [ ] **Step 1: Write a failing Execute Effects binding test**

```csharp
[Test]
public void ExecuteEffectsBinding_WritesBackToSelectedAction()
{
    ExecuteEffectsSkillAdditionActionData action = new();
    SkillAdditionData row = new() { Id = 6 };
    EffectGraphBinding binding = SkillAdditionEditorWindow.CreateEffectBinding(row, 0, 1, action, () => { });

    binding.SetRootEffects(new EffectData[] { new DamageEffectData() });
    Assert.That(action.Effects, Has.Length.EqualTo(1));
    Assert.That(binding.OwnerKey, Is.EqualTo("SkillAddition:6:Callbacks[0].Actions[1].Effects"));
}
```

- [ ] **Step 2: Run the test and verify it fails before structured action support exists**

Run in Unity Test Runner: `SkillAdditionEffectGraphBindingTests` in EditMode.

Expected: compilation failure referencing missing `CreateEffectBinding`.

- [ ] **Step 3: Implement structured callback/action editing and graph launch**

Remove the editable row JSON textarea and its Apply/Revert controls. Draw each callback's event name, conditions, and typed action list. Add actions from the existing `SkillAdditionActionData` factory/registry types. Add an `internal static EffectGraphBinding CreateEffectBinding(SkillAdditionData row, int callbackIndex, int actionIndex, ExecuteEffectsSkillAdditionActionData action, Action markDirty)` factory. For `ExecuteEffectsSkillAdditionActionData`, show the effect count and `Edit Effect Graph`; the factory uses this binding:

```csharp
new EffectGraphBinding(
    $"SkillAddition:{row.Id}:Callbacks[{callbackIndex}].Actions[{actionIndex}].Effects",
    $"Skill Addition [{row.Id}] Callback {callbackIndex} Execute Effects {actionIndex}",
    () => action.Effects,
    effects => action.Effects = effects,
    MarkDirty);
```

Keep non-effect action fields editable using the same reflection/value-expression controls used by the existing editor; no action UI may render an `EffectData[]` inline or as JSON text.

- [ ] **Step 4: Run the new binding test and manually verify the skill addition editor**

Run in Unity Test Runner: `SkillAdditionEffectGraphBindingTests` and all Effect Graph tests.

Manual check: add a callback, add Execute Effects, open its graph, add a Damage effect, save the addition table, reopen it, and verify the graph contains Damage. Confirm the detail panel has no editable raw JSON field.

- [ ] **Step 5: Commit the Skill Addition integration**

```powershell
git add -- Assets/Scripts/Game/Data/Editor/SkillAdditionEditorWindow.cs Assets/Editor/Tests/EffectGraph/SkillAdditionEffectGraphBindingTests.cs
git commit -m "feat: edit skill addition effects as graph"
```

### Task 6: Final validation and review-status update

**Files:**
- Modify: `cs_review_status.txt`
- Create on first graph edit: `Assets/Editor/EffectGraphLayouts.json` and Unity `.meta` only when the editor saves layout metadata.

**Interfaces:**
- Consumes: all tasks above and existing table serialization.
- Produces: verified editor workflow with no runtime or schema changes.

- [ ] **Step 1: Execute the complete EditMode test set**

Run in Unity Test Runner: all tests below `Assets/Editor/Tests/EffectGraph`.

Expected: model ordering, nested array discovery, cycle rejection, layout persistence, graph smoke, all host bindings, and Skill Addition binding pass.

- [ ] **Step 2: Build the solution**

```powershell
dotnet build "Crystal Magic.sln" -nologo
```

Expected: zero errors. Investigate any warning/error in a file touched by this plan before proceeding.

- [ ] **Step 3: Verify no gameplay schema or runtime files changed**

```powershell
git diff -- Assets/Scripts/Game/Data/Effects Assets/Scripts/Game/Skill/SkillExecutor.cs Assets/Scripts/Game/Unit
git diff -- Assets/Res/Data
```

Expected: no changes from this feature in either output. `Assets/Editor/EffectGraphLayouts.json` may appear only after a layout has been saved.

- [ ] **Step 4: Review every modified C# file and update review state**

Read each created/modified C# file. In `cs_review_status.txt`, change `FALSE` to `TRUE`; for a previously reviewed modified file, append or retain `DIRTY` according to the repository's existing review-state convention.

- [ ] **Step 5: Commit the review metadata only if code-task commits are clean**

```powershell
git add -- cs_review_status.txt
git commit -m "chore: review effect graph editor"
```

Do not include unrelated dirty-worktree files in any commit.
