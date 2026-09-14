# Editor Toolbar Standardization

## Goal

Remove redundant manual load and refresh controls from project editor windows. Editors load their data automatically when opened, and their toolbar exposes only actions that match the source of the edited data.

This specification standardizes window toolbars only. It does not remove buttons that edit nested content inside a row, graph, dialogue, rule, condition, or tile grid.

## Toolbar Rules

| Editor source | Toolbar actions |
| --- | --- |
| Self-contained data table | Save, Add, Copy, Delete |
| External-bound data (Prefab or ItemData) | Save only |
| Singleton or type-selected configuration | Save plus its required selector |
| Specialized workflow | Keep the specialized action in addition to its applicable save action |

All affected windows load their current data automatically. Selecting a table or configuration type loads that selection automatically; no separate Load button remains.

Copy duplicates the currently selected row as a deep copy and assigns the next valid row identifier. It is only available in self-contained data-table editors.

## Affected Windows

### Self-contained data tables

- `SkillEditorWindow`
- `BuffEditorWindow`
- `SkillAdditionEditorWindow`
- `DungeonEditorWindow`
- `DataTableViewerWindow`

Their toolbar will be exactly `Save / Add / Copy / Delete`, apart from Data Table Viewer's retained table selector and search field. Existing Add, Copy, and Delete behaviors are reused where available. Skill Addition and Data Table Viewer receive a deep-copy action.

### External-bound data

- `UnitEditorWindow` (Prefab list)
- `NPCEditorWindow` (Prefab-synchronized rows)
- `BehaviorTreeGraphWindow` (unit Prefab binding)
- `StateScriptEditorWindow` (unit Prefab binding)
- `EquipEditorWindow` (ItemData binding)
- `PropEditorWindow` (ItemData binding)

Their toolbar will contain only Save. Existing drag-and-drop copies, graph canvas actions, and data-content controls remain unchanged. NPC Prefab synchronization still occurs during automatic loading. State Script's top-level graph and runtime management buttons are removed; the graph canvas and existing drag behavior stay available.

### Specialized and singleton windows

- `ConfigEditorWindow`: retain the configuration-type selector, auto-load on selection, and retain Save.
- `UIConfigWindow`: auto-load the singleton configuration and retain Save only.
- `UINodeConfigWindow`: retain Save and `Generate UINode`, because generation is its primary specialized workflow.
- `BundleBuildWindow`: retain Save and Build; remove Load.
- `TileGridPreviewWindow`: preserve Clear and Resize because they directly edit the currently open grid.
- `UnitRuntimeDebugWindow`: remove its manual Refresh button and refresh the runtime unit list automatically while in Play Mode.

## Data Integrity and Failure Handling

- Removing a Load or Refresh button does not remove the corresponding internal loader; it remains called from the window lifecycle or from selection changes.
- Bound tables remain save-only so that their synchronization code cannot create orphaned Equip or Prop rows.
- Copy actions clone nested fields rather than sharing mutable child collections. The copied entry receives a unique identifier and becomes selected.
- Existing confirmation dialogs for destructive row deletion remain intact.

## Verification

- Review every affected toolbar to confirm its action set matches this specification.
- Verify automatic loading on window open and when changing a table or configuration type.
- Verify copied rows are independent from their source and use valid identifiers.
- Build `Crystal Magic.sln` with no C# errors or warnings.
