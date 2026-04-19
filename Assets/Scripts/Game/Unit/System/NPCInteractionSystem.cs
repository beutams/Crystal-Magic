using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using System.Collections.Generic;


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

    private NPCInteractionNodeRunner CreateRunner(NPCInteractionNodeData node)
    {
        return node switch
        {
            NPCDialogueInteractionNodeData dialogue => new NPCDialogueInteractionNodeRunner(dialogue),
            NPCSelectInteractionNodeData select => new NPCSelectInteractionNodeRunner(select),
            NPCOpenUIInteractionNodeData openUI => new NPCOpenUIInteractionNodeRunner(openUI),
            NPCMoveInteractionNodeData move => new NPCMoveInteractionNodeRunner(move),
            NPCEnterDungeonInteractionNodeData enterDungeon => new NPCEnterDungeonInteractionNodeRunner(enterDungeon),
            NPCEnterTrainingGroundInteractionNodeData enterTrainingGround => new NPCEnterTrainingGroundInteractionNodeRunner(enterTrainingGround),
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
        public bool ShouldTerminateInteraction { get; private set; }

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

        public void RequestTerminateInteraction()
        {
            ShouldTerminateInteraction = true;
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

    private sealed class NPCEnterDungeonInteractionNodeRunner : NPCInteractionNodeRunner
    {
        private readonly NPCEnterDungeonInteractionNodeData _node;
        private bool _completed;

        public NPCEnterDungeonInteractionNodeRunner(NPCEnterDungeonInteractionNodeData node)
        {
            _node = node;
        }

        public override void Enter(NPCInteractionSession session)
        {
            session.RequestTerminateInteraction();
            _completed = true;

            if (GameFlowComponent.Instance == null)
            {
                Debug.LogWarning("[NPCInteraction] GameFlowComponent is not available for EnterDungeon node.");
                return;
            }

            LoadGameContext context = SaveDataComponent.Instance?.CreateLoadGameContext(
                SaveAreaType.Dungeon,
                Mathf.Max(1, _node.DungeonFloor));

            GameFlowComponent.Instance.SetState<TransitionState>(new TransitionData
            {
                TargetSceneName = DungeonState.SceneName,
                TargetStateType = typeof(DungeonState),
                TargetStateData = context,
                ForceReloadTargetScene = true,
            });
        }

        public override bool IsCompleted(NPCInteractionSession session)
        {
            return _completed;
        }
    }

    private sealed class NPCEnterTrainingGroundInteractionNodeRunner : NPCInteractionNodeRunner
    {
        private bool _completed;

        public NPCEnterTrainingGroundInteractionNodeRunner(NPCEnterTrainingGroundInteractionNodeData node)
        {
        }

        public override void Enter(NPCInteractionSession session)
        {
            session.RequestTerminateInteraction();
            _completed = true;

            if (GameFlowComponent.Instance == null)
            {
                Debug.LogWarning("[NPCInteraction] GameFlowComponent is not available for EnterTrainingGround node.");
                return;
            }

            LoadGameContext context = SaveDataComponent.Instance?.CreateLoadGameContext(SaveAreaType.Training);

            GameFlowComponent.Instance.SetState<TransitionState>(TrainingState.CreateEnterTransitionData(context));
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

