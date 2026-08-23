# Game Interaction Design

## Goal

Make interaction a game-level pipeline: StateScript resolves an explicit interaction request, while ECS owns validation, execution, input gating, and every cross-frame interaction.

## Boundaries

- `InteractionCandidateComponent` is a singleton describing the current front-end candidate and whether a persistent interaction is active. It is not owned by, or named after, the player.
- `GameInteractionRequest` is a one-shot immutable snapshot containing `Actor`, `Target`, `InteractionKind`, `DataId`, and `Amount`. Its target data cannot change when candidate scanning picks another entity.
- `GameInteractionSystem` is the only consumer of requests and the only owner of an active NPC session. Other initiators can write a request directly without using the candidate singleton.
- `UnitInteractableComponent` is the common descriptor attached to every interactable entity. Its `DataId` means item ID for drops, NPC ID for NPCs, and region ID for treasures; `Amount` is used by drops.
- NPC interaction resolves `DataId` to the complete `NPCData` only after the request has been accepted. `NPCInteractionSession`, node runners, factories, registries, and existing events remain the persistent NPC implementation.
- NPC sessions lock only `GameGateType.PlayerInput` with an interaction-owned reason. UI input remains enabled and simulation is not locked. Every finish/cancel/invalid-target path releases that exact reason.
- StateScript's `RequestInteraction` accepts either a typed interaction getter or fixed data plus a target Entity expression. `game.interaction.candidate` returns the complete current-candidate snapshot.
- `world.interaction.isInteracting` exposes the persistent interaction state; candidate discovery and request submission both reject new interactions while it is true.

## Runtime Flow

1. `InteractionCandidateSystem` queries the interactable spatial tree and writes the nearest valid descriptor to its singleton.
2. A StateScript `RequestInteraction` action resolves a `Target + UnitInteractionData` snapshot from `game.interaction.candidate` or fixed configuration, then queues `GameInteractionRequest`.
3. `GameInteractionSystem` validates the request against the target descriptor. Drops and treasures execute synchronously. NPC requests set the singleton interaction-active flag, create a session, acquire the player-input gate, and advance its runner every frame until finish or cancel.
4. UI reads the candidate singleton only for prompt presentation. It is never an execution authority.

## Naming and Removal

- Rename `DungeonTreasureComponent` to `TreasureComponent`; retain its treasure-specific state and buffer.
- Replace `PlayerInteractionRuntimeComponent` / `PlayerInteractionKind` with `InteractionCandidateComponent` / `InteractionKind`.
- Replace `NPCInteractPromptSystem` with `InteractionCandidateSystem` and remove its type-specific candidate scans and legacy `Interact` child hiding.
- Absorb `WorldDropComponent` and `NPCInteractableComponent` data into `UnitInteractableComponent`; remove both old component types.
- Rename `WorldDropPromptManager` to `InteractionPromptManager`.
- Do not restore the deleted NPC-only request/state systems. The new `GameInteractionRequest` and `GameInteractionSystem` replace their role.

## Constraints

- Work directly in the main workspace.
- The main-character interaction graphs use the `RequestInteraction` node with the `game.interaction.candidate` getter.
- Keep UI selection input available during NPC sessions.
- Preserve existing NPC node semantics and scene-transition behavior.
