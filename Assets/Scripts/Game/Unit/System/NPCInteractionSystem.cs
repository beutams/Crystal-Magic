using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using System.Collections.Generic;

public struct NPCInteractionState : IComponentData
{
    public Entity CurrentTarget;
}

public struct NPCInteractionRequest : IComponentData
{
    public Entity Target;
    public byte HasRequest;
}

[UpdateAfter(typeof(UnitMoveSystem))]
partial struct NPCInteractPromptSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();

        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new NPCInteractionState
        {
            CurrentTarget = Entity.Null,
        });
        state.EntityManager.AddComponentData(singletonEntity, new NPCInteractionRequest
        {
            Target = Entity.Null,
            HasRequest = 0,
        });
    }

    public void OnUpdate(ref SystemState state)
    {
        float3 playerPosition = float3.zero;
        bool hasPlayer = false;

        foreach ((RefRO<PlayerTag> _, RefRO<LocalTransform> transform) in
            SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>())
        {
            playerPosition = transform.ValueRO.Position;
            hasPlayer = true;
            break;
        }

        Entity nearestNpc = Entity.Null;
        float nearestDistanceSq = float.MaxValue;

        foreach ((RefRO<NPCTag> _, RefRO<NPCInteractable> interactable, RefRO<LocalTransform> transform, Entity entity) in
            SystemAPI.Query<RefRO<NPCTag>, RefRO<NPCInteractable>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (hasPlayer)
            {
                float distanceSq = math.distancesq(playerPosition, transform.ValueRO.Position);
                if (distanceSq <= interactable.ValueRO.interactRangeSq && distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestNpc = entity;
                }
            }

            SetPromptVisible(state.EntityManager, interactable.ValueRO, shouldShow: false);
        }

        if (nearestNpc != Entity.Null && state.EntityManager.HasComponent<NPCInteractable>(nearestNpc))
        {
            NPCInteractable interactable = state.EntityManager.GetComponentData<NPCInteractable>(nearestNpc);
            SetPromptVisible(state.EntityManager, interactable, shouldShow: true);
        }

        RefRW<NPCInteractionState> interactionState = SystemAPI.GetSingletonRW<NPCInteractionState>();
        interactionState.ValueRW.CurrentTarget = nearestNpc;
    }

    private static void SetPromptVisible(EntityManager entityManager, NPCInteractable interactable, bool shouldShow)
    {
        if (interactable.interact == Entity.Null)
            return;

        if (!entityManager.HasComponent<LocalTransform>(interactable.interact))
            return;

        LocalTransform interactTransform = entityManager.GetComponentData<LocalTransform>(interactable.interact);
        interactTransform.Scale = shouldShow ? interactable.promptVisibleScale : 0f;
        entityManager.SetComponentData(interactable.interact, interactTransform);
    }
}

[UpdateAfter(typeof(NPCInteractPromptSystem))]
partial struct NPCInteractInputSystem : ISystem
{
    private NativeReference<bool> _interactRequested;
    private bool _subscribed;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NPCInteractionState>();
        _interactRequested = new NativeReference<bool>(false, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_subscribed && InputComponent.Instance != null)
        {
            InputComponent.Instance.OnInteract -= HandleInteract;
        }

        if (_interactRequested.IsCreated)
        {
            _interactRequested.Dispose();
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_subscribed && InputComponent.Instance != null)
        {
            InputComponent.Instance.OnInteract += HandleInteract;
            _subscribed = true;
        }

        if (!_interactRequested.Value)
            return;

        _interactRequested.Value = false;

        Entity target = SystemAPI.GetSingleton<NPCInteractionState>().CurrentTarget;
        if (target == Entity.Null)
            return;

        RefRW<NPCInteractionRequest> request = SystemAPI.GetSingletonRW<NPCInteractionRequest>();
        request.ValueRW.Target = target;
        request.ValueRW.HasRequest = 1;
    }

    private void HandleInteract()
    {
        _interactRequested.Value = true;
    }
}

[UpdateAfter(typeof(NPCInteractInputSystem))]
partial class NPCInteractionConsumeSystem : SystemBase
{
    private NPCInteractionSession _session;

    protected override void OnCreate()
    {
        base.OnCreate();
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
        if (interactable.NPC.Length == 0)
        {
            Debug.LogWarning("[NPCInteraction] NPCInteractable is missing NPC.");
            return;
        }

        string npcName = interactable.NPC.ToString();
        NPCData npcData = DataComponent.Instance?.Find<NPCData>(data => string.Equals(data.NPC, npcName, StringComparison.Ordinal));
        if (npcData == null)
        {
            Debug.LogWarning($"[NPCInteraction] NPCData not found for NPC '{npcName}'.");
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
                _session.CurrentRunner = CreateRunner(currentNode);
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

    private NPCInteractionNodeRunner CreateRunner(NPCInteractionNodeData node)
    {
        return node switch
        {
            NPCDialogueInteractionNodeData dialogue => new NPCDialogueInteractionNodeRunner(dialogue),
            NPCSelectInteractionNodeData select => new NPCSelectInteractionNodeRunner(select),
            NPCOpenUIInteractionNodeData openUI => new NPCOpenUIInteractionNodeRunner(openUI),
            NPCMoveInteractionNodeData move => new NPCMoveInteractionNodeRunner(move),
            _ => null,
        };
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

    private sealed class NPCInteractionSession
    {
        public NPCInteractionSession(Entity target, NPCData npcData, NPCInteractionData interaction)
        {
            Target = target;
            NpcData = npcData;
            Interaction = interaction;
            CurrentNodeGuid = interaction?.EntryNodeGuid;
            IsActive = true;
        }

        public Entity Target { get; }
        public NPCData NpcData { get; }
        public NPCInteractionData Interaction { get; }
        public string CurrentNodeGuid { get; set; }
        public string SelectedNextNodeGuid { get; set; }
        public NPCInteractionNodeRunner CurrentRunner { get; set; }
        public bool IsActive { get; private set; }

        public NPCInteractionNodeData GetCurrentNode()
        {
            return Interaction?.GetNode(CurrentNodeGuid);
        }

        public bool IsTargetValid(EntityManager entityManager)
        {
            return Target != Entity.Null && entityManager.Exists(Target);
        }

        public void Cancel()
        {
            if (!IsActive)
            {
                return;
            }

            CurrentRunner?.Cancel(this);
            CurrentRunner = null;
            IsActive = false;
        }
    }

    private abstract class NPCInteractionNodeRunner
    {
        public abstract void Enter(NPCInteractionSession session);
        public virtual void Update(NPCInteractionSession session, float deltaTime) { }
        public abstract bool IsCompleted(NPCInteractionSession session);
        public virtual void Exit(NPCInteractionSession session) { }
        public virtual void Cancel(NPCInteractionSession session) { }
    }

    private sealed class NPCDialogueInteractionNodeRunner : NPCInteractionNodeRunner
    {
        private readonly NPCDialogueInteractionNodeData _node;
        private bool _completed;

        public NPCDialogueInteractionNodeRunner(NPCDialogueInteractionNodeData node)
        {
            _node = node;
        }

        public override void Enter(NPCInteractionSession session)
        {
            Debug.Log($"[NPCInteraction] Dialogue node started. Speaker='{_node.Speaker}', ContentKey='{_node.ContentKey}'.");
            _completed = true;
        }

        public override bool IsCompleted(NPCInteractionSession session)
        {
            return _completed;
        }
    }

    private sealed class NPCOpenUIInteractionNodeRunner : NPCInteractionNodeRunner
    {
        private readonly NPCOpenUIInteractionNodeData _node;
        private UIBase _openedPanel;
        private bool _completed;

        public NPCOpenUIInteractionNodeRunner(NPCOpenUIInteractionNodeData node)
        {
            _node = node;
        }

        public override void Enter(NPCInteractionSession session)
        {
            if (UIComponent.Instance == null)
            {
                Debug.LogWarning("[NPCInteraction] UIComponent is not available for OpenUI node.");
                _completed = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(_node.UIName))
            {
                Debug.LogWarning("[NPCInteraction] OpenUI node is missing UIName.");
                _completed = true;
                return;
            }

            _openedPanel = string.IsNullOrWhiteSpace(_node.OpenData)
                ? UIComponent.Instance.Open(_node.UIName)
                : UIComponent.Instance.Open(_node.UIName, _node.OpenData);

            if (_openedPanel == null)
            {
                Debug.LogWarning($"[NPCInteraction] Failed to open UI '{_node.UIName}'.");
                _completed = true;
                return;
            }

            if (!_node.WaitUntilClosed)
            {
                _completed = true;
            }
        }

        public override void Update(NPCInteractionSession session, float deltaTime)
        {
            if (_completed || !_node.WaitUntilClosed)
            {
                return;
            }

            if (_openedPanel == null || !_openedPanel.gameObject.activeInHierarchy)
            {
                _completed = true;
            }
        }

        public override bool IsCompleted(NPCInteractionSession session)
        {
            return _completed;
        }

        public override void Cancel(NPCInteractionSession session)
        {
            if (_openedPanel != null && _openedPanel.gameObject.activeInHierarchy && _node.WaitUntilClosed && UIComponent.Instance != null)
            {
                UIComponent.Instance.ReleaseUI(_openedPanel);
            }
        }
    }

    private sealed class NPCMoveInteractionNodeRunner : NPCInteractionNodeRunner
    {
        private readonly NPCMoveInteractionNodeData _node;
        private bool _completed;

        public NPCMoveInteractionNodeRunner(NPCMoveInteractionNodeData node)
        {
            _node = node;
        }

        public override void Enter(NPCInteractionSession session)
        {
            Debug.Log($"[NPCInteraction] Move node started. TargetMarker='{_node.TargetMarker}', StopDistance={_node.StopDistance}.");
            _completed = true;
        }

        public override bool IsCompleted(NPCInteractionSession session)
        {
            return _completed;
        }
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

    private sealed class NPCSelectInteractionNodeRunner : NPCInteractionNodeRunner
    {
        private readonly NPCSelectInteractionNodeData _node;
        private bool _completed;

        public NPCSelectInteractionNodeRunner(NPCSelectInteractionNodeData node)
        {
            _node = node;
        }

        public override void Enter(NPCInteractionSession session)
        {
            List<NPCSelectOptionData> enabledOptions = new List<NPCSelectOptionData>();
            if (_node.Options != null)
            {
                for (int i = 0; i < _node.Options.Count; i++)
                {
                    NPCSelectOptionData option = _node.Options[i];
                    if (option != null && option.IsEnabled())
                    {
                        enabledOptions.Add(option);
                    }
                }
            }

            EventComponent.Instance?.Publish(new NPCInteractionSelectRequestedEvent(
                session.Target,
                session.NpcData,
                session.Interaction,
                _node,
                enabledOptions));

            Debug.Log("[NPCInteraction] Select node reached, but InteractionSelectUI is not implemented yet.");
            session.SelectedNextNodeGuid = null;
            _completed = true;
        }

        public override bool IsCompleted(NPCInteractionSession session)
        {
            return _completed;
        }
    }
}

public readonly struct NPCInteractionStartedEvent : IGameEvent
{
    public NPCInteractionStartedEvent(Entity target, NPCData npcData, NPCInteractionData interaction)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
}

public readonly struct NPCInteractionNodeStartedEvent : IGameEvent
{
    public NPCInteractionNodeStartedEvent(Entity target, NPCData npcData, NPCInteractionData interaction, NPCInteractionNodeData node)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
        Node = node;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
    public NPCInteractionNodeData Node { get; }
}

public readonly struct NPCInteractionSelectRequestedEvent : IGameEvent
{
    public NPCInteractionSelectRequestedEvent(
        Entity target,
        NPCData npcData,
        NPCInteractionData interaction,
        NPCSelectInteractionNodeData node,
        IReadOnlyList<NPCSelectOptionData> options)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
        Node = node;
        Options = options;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
    public NPCSelectInteractionNodeData Node { get; }
    public IReadOnlyList<NPCSelectOptionData> Options { get; }
}

public readonly struct NPCInteractionFinishedEvent : IGameEvent
{
    public NPCInteractionFinishedEvent(Entity target, NPCData npcData, NPCInteractionData interaction, bool wasCancelled)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
        WasCancelled = wasCancelled;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
    public bool WasCancelled { get; }
}
