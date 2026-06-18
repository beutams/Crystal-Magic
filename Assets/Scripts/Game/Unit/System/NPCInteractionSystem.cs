using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(NPCInteractPromptSystem))]
[UpdateAfter(typeof(DungeonExitSystem))]
partial class NPCInteractionSystem : SystemBase
{
    private NPCInteractionNodeRunnerFactory _runnerFactory;
    private NPCInteractionSession _session;

    protected override void OnCreate()
    {
        base.OnCreate();
        _runnerFactory = new NPCInteractionNodeRunnerFactory();
        NPCInteractionNodeRunnerRegistry.RegisterAll(_runnerFactory);
        RequireForUpdate<PlayerInteractionRuntimeComponent>();
        RequireForUpdate<PlayerTag>();
    }

    protected override void OnDestroy()
    {
        _session?.Cancel();
        _session = null;
        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        if (_session != null && _session.IsActive)
        {
            UpdateActiveSession(SystemAPI.Time.DeltaTime);
            return;
        }

        TryStartRequestedInteraction();
    }

    private void TryStartRequestedInteraction()
    {
        RefRW<PlayerInteractionRuntimeComponent> runtime = SystemAPI.GetSingletonRW<PlayerInteractionRuntimeComponent>();
        if (runtime.ValueRO.CurrentKind != PlayerInteractionKind.Npc || runtime.ValueRO.CurrentTarget == Entity.Null)
            return;

        Entity playerEntity = Entity.Null;
        foreach ((RefRO<PlayerTag> _, RefRO<UnitIntentComponent> intentRef, Entity entity) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<UnitIntentComponent>>().WithEntityAccess())
        {
            if (UnitControlUtility.IsInControlledState(EntityManager, entity))
                break;

            playerEntity = entity;
            if (!intentRef.ValueRO.WantToInteract)
                return;

            break;
        }

        if (playerEntity == Entity.Null)
            return;

        Entity target = runtime.ValueRO.CurrentTarget;
        if (!TryStartInteraction(target))
            return;

        runtime.ValueRW.CurrentTarget = Entity.Null;
        runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
        ConsumeInteract(playerEntity);
    }

    private bool TryStartInteraction(Entity target)
    {
        if (target == Entity.Null || !EntityManager.Exists(target) || !EntityManager.HasComponent<NPCInteractableComponent>(target))
        {
            return false;
        }

        NPCInteractableComponent interactable = EntityManager.GetComponentData<NPCInteractableComponent>(target);
        if (interactable.NpcId < 0)
        {
            Debug.LogWarning("[NPCInteraction] NPCInteractableComponent did not resolve a matching NPCData during baking.");
            return false;
        }

        NPCData npcData = DataComponent.Instance?.Get<NPCData>(interactable.NpcId);
        if (npcData == null)
        {
            Debug.LogWarning($"[NPCInteraction] NPCData not found for resolved Id '{interactable.NpcId}'.");
            return false;
        }

        NPCInteractionData interaction = SelectInteraction(npcData);
        if (interaction == null)
        {
            Debug.Log($"[NPCInteraction] No enabled interaction found for NPC '{npcData.NPC}'.");
            return false;
        }

        if (interaction.GetEntryNode() == null)
        {
            Debug.LogWarning($"[NPCInteraction] Interaction '{interaction.Key}' on NPC '{npcData.NPC}' is missing an entry node.");
            return false;
        }

        _session = new NPCInteractionSession(target, npcData, interaction);
        EventComponent.Instance.Publish(new NPCInteractionStartedEvent(target, npcData, interaction));
        AdvanceSessionUntilBlocked(0f);
        return true;
    }

    private NPCInteractionData SelectInteraction(NPCData npcData)
    {
        NPCInteractionData selected = null;
        int enabledCount = 0;

        foreach (NPCInteractionData interaction in npcData.GetEnabledInteractions())
        {
            if (selected == null)
            {
                selected = interaction;
            }

            enabledCount++;
        }

        if (enabledCount > 1)
        {
            Debug.Log($"[NPCInteraction] NPC '{npcData.NPC}' has {enabledCount} enabled interactions. Using the first one: '{selected?.Key}'.");
        }

        return selected;
    }

    private void UpdateActiveSession(float deltaTime)
    {
        if (_session == null || !_session.IsActive)
        {
            return;
        }

        if (!_session.IsTargetValid(EntityManager))
        {
            FinishSession(wasCancelled: true);
            return;
        }

        AdvanceSessionUntilBlocked(deltaTime);
    }

    private void AdvanceSessionUntilBlocked(float deltaTime)
    {
        if (_session == null || !_session.IsActive)
        {
            return;
        }

        int maxSteps = _session.Interaction?.Nodes?.Count + 1 ?? 1;
        for (int i = 0; i < maxSteps; i++)
        {
            NPCInteractionNodeData currentNode = _session.GetCurrentNode();
            if (currentNode == null)
            {
                FinishSession(wasCancelled: false);
                return;
            }

            if (_session.CurrentRunner == null)
            {
                _session.CurrentRunner = _runnerFactory.Create(currentNode);
                if (_session.CurrentRunner == null)
                {
                    string nodeTypeName = NPCInteractionNodeDataRegistry.TryGetNodeKey(currentNode.GetType(), out string resolvedTypeName)
                        ? resolvedTypeName
                        : currentNode.GetType().FullName;
                    Debug.LogWarning($"[NPCInteraction] Unsupported node type '{nodeTypeName}'. Skipped.");
                    _session.CurrentNodeGuid = ResolveNextNodeGuid(_session, currentNode, null);
                    continue;
                }

                EventComponent.Instance?.Publish(new NPCInteractionNodeStartedEvent( _session.Target, _session.NpcData, _session.Interaction, currentNode));
                _session.SelectedNextNodeGuid = null;
                _session.CurrentRunner.Enter(_session);
            }

            _session.CurrentRunner.Update(_session, deltaTime);
            if (!_session.CurrentRunner.IsCompleted(_session))
            {
                return;
            }

            _session.CurrentRunner.Exit(_session);
            _session.CurrentRunner = null;
            _session.CurrentNodeGuid = ResolveNextNodeGuid(_session, currentNode, _session.SelectedNextNodeGuid);
            _session.SelectedNextNodeGuid = null;
        }

        Debug.LogWarning("[NPCInteraction] Interaction advanced too many nodes in one frame and was stopped defensively.");
    }

    private void FinishSession(bool wasCancelled)
    {
        if (_session == null)
        {
            return;
        }

        if (wasCancelled)
        {
            _session.Cancel();
        }

        EventComponent.Instance?.Publish(new NPCInteractionFinishedEvent(
            _session.Target,
            _session.NpcData,
            _session.Interaction,
            wasCancelled));
        _session = null;
    }

    private static string ResolveNextNodeGuid(NPCInteractionSession session, NPCInteractionNodeData currentNode, string selectedNextNodeGuid)
    {
        if (!string.IsNullOrWhiteSpace(selectedNextNodeGuid))
        {
            return selectedNextNodeGuid;
        }

        if (currentNode?.Branches != null)
        {
            for (int i = 0; i < currentNode.Branches.Count; i++)
            {
                NPCInteractionBranchData branch = currentNode.Branches[i];
                if (branch != null && branch.IsEnabled())
                {
                    return branch.NextNodeGuid;
                }
            }
        }

        return null;
    }

    private void ConsumeInteract(Entity playerEntity)
    {
        if (playerEntity == Entity.Null || !EntityManager.HasComponent<UnitIntentComponent>(playerEntity))
            return;

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(playerEntity);
        intent.WantToInteract = false;
        EntityManager.SetComponentData(playerEntity, intent);
    }

}

