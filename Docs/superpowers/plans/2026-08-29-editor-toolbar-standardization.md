# Editor Toolbar Standardization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Standardize editor-window toolbars so automatic data loading replaces redundant Load and Refresh controls, while each editor exposes only actions valid for its data source.

**Architecture:** Treat the toolbar as the standardized action surface and leave nested data-editing controls unchanged. Self-contained row tables receive Save/Add/Copy/Delete; externally bound tables receive Save only. Type selectors, build/code-generation actions, and direct tile-grid editing remain only where they are the editor's primary workflow.

**Tech Stack:** Unity Editor IMGUI/UI Toolkit, C# 9, Newtonsoft.Json, Unity JsonUtility, `dotnet build`.

**Spec:** `Docs/superpowers/specs/2026-08-29-editor-toolbar-standardization-design.md`

## Global Constraints

- Only window toolbars are standardized; buttons that edit nested data, graph contents, or tile grids remain.
- Self-contained table copy must deep-copy the selected row, assign a valid new identifier, and select the copy.
- External-bound tables must remain Save-only to prevent orphaned or unsynchronized records.
- Table/config selectors remain visible and load their selected content automatically.
- Existing delete confirmations remain intact.
- Preserve unrelated working-tree changes.

---

## File Structure

- `Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs` — self-contained Skill table toolbar.
- `Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs` — self-contained Buff table toolbar.
- `Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.cs` — self-contained Dungeon table toolbar.
- `Assets/Scripts/Game/Data/Editor/SkillAdditionEditorWindow.cs` — add dirty tracking and deep-copy support.
- `Assets/Scripts/Core/Data/Editor/DataTableViewerWindow.cs` — automatic type loading and selected-row copy/delete.
- `Assets/Scripts/Game/Data/Editor/NPCEditorWindow.cs` — Prefab-bound Save-only toolbar.
- `Assets/Scripts/Game/Data/Editor/EquipEditorWindow.cs` — ItemData-bound Save-only toolbar.
- `Assets/Scripts/Game/Data/Editor/PropEditorWindow.cs` — ItemData-bound Save-only toolbar.
- `Assets/Scripts/Game/Unit/Editor/StateScriptEditorWindow.cs` — Prefab-bound Save-only toolbar.
- `Assets/Scripts/Core/Config/Editor/ConfigEditorWindow.cs` — automatic type selection and Save-only configuration toolbar.
- `Assets/Scripts/Editor/UIConfigWindow.cs` — singleton configuration Save-only toolbar.
- `Assets/Scripts/Core/Resource/Editor/BundleBuildWindow.cs` — preserve Save/Build and remove Load.
- `Assets/Scripts/Game/Unit/Editor/UnitRuntimeDebugWindow.cs` — automatic live-unit refresh without a Refresh button.

`UnitEditorWindow` and `BehaviorTreeGraphWindow` already meet the Save-only bound-editor rule. `UINodeConfigWindow` already correctly exposes Save plus its specialized generator. `TileGridPreviewWindow` is intentionally unchanged.

### Task 1: Make existing self-contained table toolbars conform

**Files:**
- Modify: `Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs:330-369`
- Modify: `Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs:356-391`
- Modify: `Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.cs:72-99`

**Interfaces:**
- Consumes: existing `SaveData`, `AddSkill`/`AddBuff`/`AddTheme`, `DuplicateSelected`/`DuplicateTheme`, and deletion methods.
- Produces: exactly `Save / Add / Copy / Delete` toolbar actions in each window.

- [ ] **Step 1: Remove only the Load controls**

Delete the `GUILayout.Button("Load"...)` or `GUILayout.Button("加载"...)` blocks. Keep each window's lifecycle load call and its existing Add, Copy, Delete, dirty state, and confirmation behavior.

- [ ] **Step 2: Normalize the action labels**

Use `Save`, `Add`, `Copy`, and `Delete` for the four toolbar actions while retaining existing enable/disable logic. Do not alter effect, modifier, node, or terrain-grid buttons in detail panels.

- [ ] **Step 3: Verify the source shape**

Run:

```powershell
rg -n 'GUILayout\.Button\("(Load|加载)"' Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.cs
```

Expected: no result.

### Task 2: Complete Skill Addition's standard table actions

**Files:**
- Modify: `Assets/Scripts/Game/Data/Editor/SkillAdditionEditorWindow.cs:42-260`

**Interfaces:**
- Consumes: `_rows`, `_selectedIndex`, `s_jsonSettings`, `NormalizeIds`, and `Select`.
- Produces: `DuplicateSelectedRow()` and `MarkDirty()` methods; Save/Add/Copy/Delete toolbar.

- [ ] **Step 1: Add a dirty-state field and helper**

Add `private bool _isDirty;` and a `MarkDirty()` method that sets it true. Call it after row add, delete, JSON apply, and editable field/callback changes. Reset it in `Load()` and after successful `Save()`.

- [ ] **Step 2: Implement deep row copying**

Add:

```csharp
private void DuplicateSelectedRow()
{
    if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
        return;

    string json = JsonConvert.SerializeObject(_rows[_selectedIndex], s_jsonSettings);
    SkillAdditionData copy = JsonConvert.DeserializeObject<SkillAdditionData>(json, s_jsonSettings);
    if (copy == null)
        return;

    _rows.Add(copy);
    NormalizeIds();
    Select(_rows.Count - 1);
    MarkDirty();
}
```

- [ ] **Step 3: Replace the toolbar**

Remove Reload. Order the toolbar actions as Save, Add, Copy, Delete. Disable Copy/Delete when no row is selected and show `Save *` while dirty.

- [ ] **Step 4: Verify clone isolation**

Open the editor, create an Addition with a callback, copy it, alter the copied callback, and confirm the source row's callback remains unchanged before saving.

### Task 3: Standardize Data Table Viewer around a selected row

**Files:**
- Modify: `Assets/Scripts/Core/Data/Editor/DataTableViewerWindow.cs:20-276,280-430`

**Interfaces:**
- Consumes: `_selectedTypeIndex`, `_loadedType`, `_rows`, `LoadTable(Type)`, `NormalizeRowIds()`, and `SaveTable()`.
- Produces: `_selectedRowIndex`, `SelectRow(int)`, `DuplicateSelectedRow()`, and `DeleteSelectedRow()`.

- [ ] **Step 1: Load a default table automatically**

Extend `OnEnable()` to scan types and call `LoadTable(_rowTypes[0])` when a type exists. On type-popup changes, call `LoadTable(_rowTypes[_selectedTypeIndex])` immediately instead of clearing `_loadedType` and waiting for Load.

- [ ] **Step 2: Add selected-row state and toolbar actions**

Add `private int _selectedRowIndex = -1;`. Select rows from their main table-row click area and reset the selection in `LoadTable`. Replace Load and Refresh Types with toolbar buttons in this order:

```csharp
SaveTable();
AddRow();
DuplicateSelectedRow();
DeleteSelectedRow();
```

Keep the table-type popup and search field. Remove the inline row `X` button so Delete has one consistent toolbar entry.

- [ ] **Step 3: Implement generic deep copy and deletion**

Use Unity serialization for the polymorphic runtime type:

```csharp
private void DuplicateSelectedRow()
{
    if (_selectedRowIndex < 0 || _selectedRowIndex >= _rows.Count || _loadedType == null)
        return;

    object copy = JsonUtility.FromJson(JsonUtility.ToJson(_rows[_selectedRowIndex]), _loadedType);
    _rows.Insert(_selectedRowIndex + 1, copy);
    NormalizeRowIds();
    _selectedRowIndex++;
    _isDirty = true;
}
```

`DeleteSelectedRow()` removes the selected entry, normalizes IDs, clears or clamps the selected index, and sets `_isDirty = true`.

- [ ] **Step 4: Verify type switching and copy**

Switch between two table types and verify each table loads without a Load click. Copy a selected row with an array field, edit the copied array, and verify the original remains unchanged.

### Task 4: Reduce external-bound editor toolbars to Save

**Files:**
- Modify: `Assets/Scripts/Game/Data/Editor/NPCEditorWindow.cs:81-109`
- Modify: `Assets/Scripts/Game/Data/Editor/EquipEditorWindow.cs:76-96`
- Modify: `Assets/Scripts/Game/Data/Editor/PropEditorWindow.cs:227-247`
- Modify: `Assets/Scripts/Game/Unit/Editor/StateScriptEditorWindow.cs:161-182`

**Interfaces:**
- Consumes: existing lifecycle methods `LoadData`, `RefreshRowsFromPrefabs`, `RefreshItemCache`, `LoadThemes`, and `LoadData` in State Script.
- Produces: one Save action per external-bound toolbar.

- [ ] **Step 1: Remove NPC, Equip, and Prop Load/Refresh controls**

Delete only the toolbar button blocks. Preserve `OnEnable()` loading and the internal Prefab/Item synchronization performed by those loaders.

- [ ] **Step 2: Make State Script toolbar Save-only**

In `BuildToolbar`, retain only Save and the status label. Remove Load, Add Graph, Delete Graph, Validate, Generate Registry, and Refresh Runtime toolbar buttons. Do not change unit/graph selection, graph-canvas controls, or existing graph drag-and-drop behavior.

- [ ] **Step 3: Verify bound-window preservation**

Open NPC, Equip, Prop, and State Script editors. Confirm their initial data displays after opening, their toolbar contains Save only, and selecting/editing existing content remains possible.

### Task 5: Make special configuration workflows automatic

**Files:**
- Modify: `Assets/Scripts/Core/Config/Editor/ConfigEditorWindow.cs:46-155`
- Modify: `Assets/Scripts/Editor/UIConfigWindow.cs:26-67`
- Modify: `Assets/Scripts/Core/Resource/Editor/BundleBuildWindow.cs:21-72`

**Interfaces:**
- Consumes: `ScanConfigTypes`, `LoadConfig(Type)`, `SaveConfig`, `UIConfigWindow.LoadConfig`, `UIConfigWindow.SaveConfig`, and `BundleBuildUtility.LoadConfig/SaveConfig/Build`.
- Produces: automatic selected-configuration loading, singleton Save-only toolbar, and Save/Build bundle toolbar.

- [ ] **Step 1: Auto-load Config Editor selections**

After `ScanConfigTypes()` in `OnEnable`, call `LoadConfig` for the first type when available. When the type popup changes, call `LoadConfig` for the new type immediately. Remove Load and Refresh Types buttons while retaining the selector and Save.

- [ ] **Step 2: Simplify UI Config to Save-only**

Remove the New Config and Load Config buttons. Retain `OnEnable()` loading and the Save button; update the null-state help text to state that configuration loads automatically. Remove the now-unreachable new-config method only if it has no remaining call sites.

- [ ] **Step 3: Simplify the bundle builder toolbar**

Remove Load. Keep Save and Build because build is the window's primary specialized action, and retain automatic config loading in `OnEnable()`.

- [ ] **Step 4: Verify special workflows**

Open Config Editor and switch types; confirm data changes immediately. Open UI Config and AssetBundle Builder; confirm their data loads on open and their retained actions work.

### Task 6: Replace runtime debug's manual refresh with scheduled refresh

**Files:**
- Modify: `Assets/Scripts/Game/Unit/Editor/UnitRuntimeDebugWindow.cs:17-84`

**Interfaces:**
- Consumes: `RefreshUnits()`, `EditorApplication.timeSinceStartup`, and `Application.isPlaying`.
- Produces: a runtime refresh interval and a toolbar with status text only.

- [ ] **Step 1: Add refresh scheduling state**

Add:

```csharp
private const double RuntimeRefreshIntervalSeconds = 0.5d;
private double _nextRuntimeRefreshTime;
```

- [ ] **Step 2: Refresh automatically in Play Mode**

Replace `OnInspectorUpdate()` with logic that calls `RefreshUnits()` only when play mode is active and `EditorApplication.timeSinceStartup >= _nextRuntimeRefreshTime`, then moves the next timestamp forward by the interval. Repaint after this check.

- [ ] **Step 3: Remove the toolbar button**

Delete the Refresh button from `DrawToolbar`; leave the status label. Keep initial and play-mode-transition refreshes.

- [ ] **Step 4: Verify live updates**

Enter Play Mode, spawn or destroy a unit, and confirm the unit list updates within one second without pressing a button.

### Task 7: Verify the final toolbar matrix and compile

**Files:**
- Modify: `cs_review_status.txt` only for reviewed C# files whose current status requires an update.

**Interfaces:**
- Consumes: all toolbar methods modified by Tasks 1-6.
- Produces: a clean C# build and an evidence-backed toolbar audit.

- [ ] **Step 1: Audit toolbar labels**

Run targeted `rg` queries for `Load`, `Reload`, and `Refresh` in every changed toolbar. Confirm remaining instances only belong to lifecycle methods, nested controls, or explicitly preserved specialized workflows.

- [ ] **Step 2: Review changed C# sources**

Follow `cs_review_status.txt`: mark each edited C# file reviewed according to its existing convention, without changing unrelated entries.

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build 'Crystal Magic.sln' -nologo
```

Expected: build succeeds with zero errors. Report any existing warnings separately from toolbar changes.

- [ ] **Step 4: Commit focused changes**

Stage only the changed toolbar sources, status file, and plan documentation; do not stage unrelated existing changes.

```powershell
git add -- Assets/Scripts/Game/Skill/Editor/SkillEditorWindow.cs Assets/Scripts/Game/Data/Editor/BuffEditorWindow.cs Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.cs Assets/Scripts/Game/Data/Editor/SkillAdditionEditorWindow.cs Assets/Scripts/Core/Data/Editor/DataTableViewerWindow.cs Assets/Scripts/Game/Data/Editor/NPCEditorWindow.cs Assets/Scripts/Game/Data/Editor/EquipEditorWindow.cs Assets/Scripts/Game/Data/Editor/PropEditorWindow.cs Assets/Scripts/Game/Unit/Editor/StateScriptEditorWindow.cs Assets/Scripts/Core/Config/Editor/ConfigEditorWindow.cs Assets/Scripts/Editor/UIConfigWindow.cs Assets/Scripts/Core/Resource/Editor/BundleBuildWindow.cs Assets/Scripts/Game/Unit/Editor/UnitRuntimeDebugWindow.cs cs_review_status.txt Docs/superpowers/plans/2026-08-29-editor-toolbar-standardization.md
git commit -m "refactor: standardize editor toolbars"
```
