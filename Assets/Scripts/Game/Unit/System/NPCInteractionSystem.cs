using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

[UpdateAfter(typeof(NPCInteractInputSystem))]
partial class NPCInteractionConsumeSystem : SystemBase
{
    private NPCInteractionNodeFactory _nodeFactory;
    private NPCInteractionSession _session;

    protected override void OnCreate()
    {
        base.OnCreate();
        _nodeFactory = new NPCInteractionNodeFactory();
        NPCInteractionNodeRegistry.RegisterAll(_nodeFactory);
        RequireForUpdate<NPCInteractionRequest>();
    }

    protected override void OnDestroy()
    {
        _session?.Cancel();
        _session = null;
        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        ConsumePendingRequest();

        if (_session == null || !_session.IsActive)
        {
            return;
        }

        UpdateActiveSession(SystemAPI.Time.DeltaTime);
    }

    private void ConsumePendingRequest()
    {
        if (!SystemAPI.HasSingleton<NPCInteractionRequest>())
        {
            return;
        }

        RefRW<NPCInteractionRequest> request = SystemAPI.GetSingletonRW<NPCInteractionRequest>();
        if (request.ValueRO.HasRequest == 0)
        {
            return;
        }

        Entity target = request.ValueRO.Target;
        request.ValueRW.Target = Entity.Null;
        request.ValueRW.HasRequest = 0;

        if (_session != null && _session.IsActive)
        {
            Debug.Log("[NPCInteraction] Ignored interaction request because another interaction is active.");
            return;
        }

        TryStartInteraction(target);
    }

    private void TryStartInteraction(Entity target)
    {
        if (target == Entity.Null || !EntityManager.Exists(target) || !EntityManager.HasComponent<NPCInteractable>(target))
        {
            return;
        }

        NPCInteractable interactable = EntityManager.GetComponentData<NPCInteractable>(target);
        if (interactable.NpcDataId <= 0)
        {
            Debug.LogWarning("[NPCInteraction] NPCInteractable is missing NpcDataId.");
            return;
        }

        NPCData npcData = DataComponent.Instance?.Get<NPCData>(interactable.NpcDataId);
        if (npcData == null)
        {
            Debug.LogWarning($"[NPCInteraction] NPCData not found for Id '{interactable.NpcDataId}'.");
            return;
        }

        NPCInteractionData interaction = SelectInteraction(npcData);
        if (interaction == null)
        {
            Debug.Log($"[NPCInteraction] No enabled interaction found for NPC '{npcData.NPC}'.");
            return;
        }

        if (interaction.GetEntryNode() == null)
        {
            Debug.LogWarning($"[NPCInteraction] Interaction '{interaction.Key}' on NPC '{npcData.NPC}' is missing an entry node.");
            return;
        }

        _session = new NPCInteractionSession(target, npcData, interaction);
        EventComponent.Instance?.Publish(new NPCInteractionStartedEvent(target, npcData, interaction));
        AdvanceSessionUntilBlocked(0f);
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
                _session.CurrentRunner = _nodeFactory.Create(currentNode);
                if (_session.CurrentRunner == null)
                {
                    Debug.LogWarning($"[NPCInteraction] Unsupported node type '{currentNode?.Type}'. Skipped.");
                    _session.CurrentNodeGuid = ResolveNextNodeGuid(_session, currentNode, null);
                    continue;
                }

                EventComponent.Instance?.Publish(new NPCInteractionNodeStartedEvent(
                    _session.Target,
                    _session.NpcData,
                    _session.Interaction,
                    currentNode));
                _session.SelectedNextNodeGuid = null;
                _session.CurrentRunner.Enter(_session);
            }

            _session.CurrentRunner.Update(_session, deltaTime);
            if (_session.ShouldTerminateInteraction)
            {
                _session.CurrentRunner.Exit(_session);
                _session.CurrentRunner = null;
                FinishSession(wasCancelled: false);
                return;
            }

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

}

