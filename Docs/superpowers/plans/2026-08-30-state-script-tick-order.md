# State Script Tick Order Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make per-frame State Script node order explicit and configurable, then use it to stabilize repeated held skill-chain casting.

**Architecture:** Store an integer order on the shared state-node data base class. Build the existing traversal list first, then apply a stable ascending order only to nodes that implement per-frame state updates. The editor exposes the field, while non-state nodes retain synchronous port-pulse behavior.

**Tech Stack:** Unity C#, Newtonsoft JSON, Unity IMGUI editor tooling.

**Spec:** `Docs/superpowers/specs/2026-08-30-state-script-tick-order-design.md`

## Global Constraints

- `TickOrder` defaults to `0` for backward-compatible State Script JSON loading.
- Lower values update first; equal values preserve current graph traversal order.
- Only `StateScriptStateNode` instances participate in ordering.
- The State Script JSON file must retain its UTF-8 BOM.

---

### Task 1: Persist and schedule Tick Order

**Files:**
- Modify: `Assets/Scripts/Game/Data/StateScript/StateScriptData.cs`
- Modify: `Assets/Scripts/Game/Unit/StateScript/StateScriptRuntime.cs`

**Interfaces:**
- Produces: `StateStateScriptNodeData.TickOrder`, an integer defaulting to `0`.
- Produces: `StateScriptRuntime.StatesInTickOrder`, ordered by ascending `TickOrder` with stable traversal-order ties.

- [ ] Add `public int TickOrder;` to `StateStateScriptNodeData`.
- [ ] Replace direct state-list append in `CompleteBuild()` with an ascending stable sort over the existing traversal sequence.
- [ ] Build `Assembly-CSharp.csproj` and verify zero errors.

### Task 2: Expose Tick Order in the State Script inspector

**Files:**
- Modify: `Assets/Scripts/Game/Unit/Editor/StateScriptNodeInspector.cs`

**Interfaces:**
- Consumes: `StateStateScriptNodeData.TickOrder`.
- Produces: an editable `Tick Order` field for every state node, reporting changes through the existing `onChanged` callback.

- [ ] Render `Tick Order` before node-specific configuration when the selected node is a state node.
- [ ] Mark the graph dirty only when the integer value changes.
- [ ] Build `Assembly-CSharp-Editor.csproj` and verify zero errors.

### Task 3: Configure and validate SkillChain

**Files:**
- Modify: `Assets/Res/Data/StateScriptDataTable.json`

**Interfaces:**
- Consumes: `TickOrder` on the SkillChain casting monitor.
- Produces: `TickOrder = -10` for the casting monitor and default `0` for all other nodes.

- [ ] Add `TickOrder: -10` only to the SkillChain casting monitor.
- [ ] Confirm the JSON parses, retains a UTF-8 BOM, and has no unrelated graph rewrites.
- [ ] Build both main and editor assemblies serially.
