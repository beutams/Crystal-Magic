# Game Interaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace unit-specific interaction selection with a game-level request and maintenance pipeline, including persistent NPC interactions.

**Architecture:** A generic `UnitInteractableComponent` describes target entities. `InteractionCandidateSystem` exposes the nearest valid candidate through a singleton, while StateScript `RequestInteraction` resolves a typed getter or fixed request snapshot into `GameInteractionRequest`. `GameInteractionSystem` executes immediate kinds and owns the existing managed `NPCInteractionSession` for NPC flows.

**Tech Stack:** Unity Entities, C#, existing GameGate, StateScript source/node registry, NUnit EditMode tests.

**Spec:** `Docs/superpowers/specs/2026-08-22-game-interaction-design.md`

## Global Constraints

- Migrate existing interaction-node JSON to `RequestInteraction` while preserving its candidate-getter behavior.
- `InteractionCandidateComponent` remains a singleton and is not player-named.
- Non-player initiators submit `GameInteractionRequest` directly and specify `Actor`.
- NPC sessions lock `GameGateType.PlayerInput` only; `UIInput` and `Simulation` remain unlocked.
- Rename `DungeonTreasureComponent` to `TreasureComponent`.
- Preserve existing NPC session, Runner, factory, registry, and event contracts.

---

### Task 1: Define generic target, candidate, and request data

**Files:**
- Create: `Assets/Scripts/Game/Interaction/Component/UnitInteractableComponent.cs`
- Create: `Assets/Scripts/Game/Interaction/Component/InteractionCandidateComponent.cs`
- Create: `Assets/Scripts/Game/Interaction/Component/GameInteractionRequest.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/NPCInteractableAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/DungeonTreasureAuthoring.cs`
- Modify: `Assets/Scripts/Game/Unit/Utility/WorldDropSpawnUtility.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/DungeonTreasureCandidateItemElement.cs`
- Delete: `Assets/Scripts/Game/Unit/Component/PlayerInteractionRuntimeComponent.cs`
- Delete: `Assets/Scripts/Game/Unit/Component/WorldDropComponent.cs`
- Test: `Assets/Tests/Editor/GameInteractionDataTests.cs`

**Interfaces:**
- `InteractionKind` has `None`, `Drop`, `Treasure`, and `Npc`.
- `UnitInteractionData` has `InteractionKind Kind`, `int DataId`, and `int Amount`.
- `UnitInteractableComponent` contains `UnitInteractionData Data`, `float RangeSq`, and `byte IsEnabled`.
- `GameInteractionRequest` contains `Entity Actor`, `Entity Target`, `UnitInteractionData Data`, and `byte HasRequest`.

- [ ] Write tests that verify the default data is `None`, drop data retains item ID and amount, and a request retains its copied payload after the candidate payload is changed.
- [ ] Add the three component files and the generic enums/structs above.
- [ ] Make NPC baking, treasure baking, and drop spawning add `UnitInteractableComponent`; move old component fields into its payload.
- [ ] Rename `DungeonTreasureComponent` and every candidate element/reference to `TreasureComponent` naming.
- [ ] Delete obsolete components and run `dotnet build "Crystal Magic.sln" -nologo`.

### Task 2: Build candidate discovery and generic prompt consumption

**Files:**
- Create: `Assets/Scripts/Game/Interaction/System/InteractionCandidateSystem.cs`
- Modify: `Assets/Scripts/Game/Unit/System/UnitQuerySystem.cs`
- Modify: `Assets/Scripts/Game/Unit/Utility/UnitQueryUtility.cs`
- Modify: `Assets/Scripts/UI/WorldDropPromptUI/WorldDropPromptManager.cs`
- Delete: `Assets/Scripts/Game/Unit/System/NPCInteractPromptSystem.cs`
- Test: `Assets/Tests/Editor/InteractionCandidateSelectionTests.cs`

**Interfaces:**
- The new spatial tree is `UnitQueryTreeKind.Interactable` and indexes enabled `UnitInteractableComponent` entities.
- `InteractionCandidateSystem` creates exactly one `InteractionCandidateComponent` singleton and clears it while `GameGateType.PlayerInput` is locked.
- The prompt manager consumes only `InteractionCandidateComponent` and is renamed to `InteractionPromptManager`.

- [ ] Write selection tests for nearest distance and the stable tie order Drop, Treasure, NPC.
- [ ] Index generic interactables in `UnitQuerySystem` and use the tree in `InteractionCandidateSystem`.
- [ ] Apply kind-specific availability checks for destroyed drops and opened treasures before writing the candidate.
- [ ] Replace the prompt manager's dependency on the old singleton and remove legacy NPC child-transform hiding.
- [ ] Build and run the candidate-selection tests through the available test runner.

### Task 3: Restore game-level interaction maintenance

**Files:**
- Create: `Assets/Scripts/Game/Interaction/System/GameInteractionSystem.cs`
- Create: `Assets/Scripts/Game/Interaction/Utility/GameInteractionRequestUtility.cs`
- Modify: `Assets/Scripts/Game/Unit/NPCInteraction/NPCInteractionSession.cs`
- Modify: `Assets/Scripts/Game/Unit/NPCInteraction/NPCInteractionNodeRunners.cs`
- Modify: `Assets/Scripts/Game/Unit/System/DestroyEntitySystem.cs`
- Test: `Assets/Tests/Editor/GameInteractionRequestTests.cs`

**Interfaces:**
- `GameInteractionRequestUtility.TrySubmit(EntityManager, Entity actor, in InteractionRequestSnapshot interaction)` writes one request only when the snapshot is valid and the singleton is not interaction-active.
- `GameInteractionSystem` owns a nullable `NPCInteractionSession`; it exposes no public mutable session API.
- `GameInteractionSystem` validates `Target` and `UnitInteractableComponent` before dispatching by `InteractionKind`.

- [ ] Write request tests for candidate snapshot ownership and rejection of a second pending request.
- [ ] Implement request consumption and revalidation in `GameInteractionSystem`.
- [ ] Implement Drop as an inventory transaction with destruction only after success; on failure publish the existing user-facing capacity message path and leave the drop intact.
- [ ] Implement Treasure as a one-time transaction, setting `TreasureComponent.IsOpened` and disabling its common interactable descriptor.
- [ ] Move the prior `NPCInteractionSystem` update loop into the NPC branch. Resolve `NPCData` from `DataId`, create the session, advance runners each frame, and publish the existing start/node/finished events.
- [ ] Acquire `GameGateType.PlayerInput` using `GameInteraction.NpcSession` before entering the first NPC node. Release that exact reason in normal finish, cancel, invalid-target, and system-destroy paths.
- [ ] Build and run interaction request tests.

### Task 4: Expose StateScript request capability

**Files:**
- Create: StateScript data/source/runtime node files for `RequestInteraction`
- Modify: `Assets/Scripts/Game/Unit/Source/UnitComponentSourceRegistry.cs`
- Modify: `Assets/Scripts/Game/Unit/StateScript/StateScriptRegistry.cs`
- Modify: StateScript registry generator output as required
- Test: `Assets/Tests/Editor/GameInteractionDataTests.cs`

**Interfaces:**
- Sources expose `game.interaction.hasCandidate`, `game.interaction.candidateKind`, `game.interaction.candidateTarget`, typed `game.interaction.candidate`, and `world.interaction.isInteracting`.
- The action node resolves either a typed interaction getter or fixed data plus target Entity, calls `GameInteractionRequestUtility.TrySubmit`, and sends `Out` only on success.

- [ ] Write a node test for missing candidate, valid request creation, and no mutation of the candidate after the request is queued.
- [ ] Add data and runtime node types using the existing generated StateScript registration pattern.
- [ ] Register the sources, regenerate the registry, and migrate existing interaction-node JSON to `RequestInteraction`.
- [ ] Run targeted tests and build the solution.

### Task 5: Remove residue and verify the full migration

**Files:**
- Modify: affected prefabs and UI script/meta references after class renames
- Modify: `Assets/Scripts/Game/Unit/UnitComponentInventory.md`
- Delete: old interaction component/system/meta files from Tasks 1-2

- [ ] Search `Assets/Scripts` and prefabs for `PlayerInteractionRuntimeComponent`, `PlayerInteractionKind`, `WorldDropComponent`, `NPCInteractableComponent`, `DungeonTreasureComponent`, and `NPCInteractPromptSystem`; remove every obsolete reference.
- [ ] Confirm no StateScript JSON file changed with `git diff -- Assets/Res/Data/StateScriptDataTable.json`.
- [ ] Run `dotnet build "Crystal Magic.sln" -nologo` and `git diff --check`.
- [ ] In Unity, verify E prompt selection, successful/failed drop pickup, one-time treasure opening, NPC selection UI, input lock during NPC interaction, and UI click availability.
