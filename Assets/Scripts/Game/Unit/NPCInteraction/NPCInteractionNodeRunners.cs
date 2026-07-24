using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Unity.Entities;
using UnityEngine;

public sealed class NPCDialogueInteractionNodeRunner : NPCInteractionNodeRunner
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

public sealed class NPCOpenUIInteractionNodeRunner : NPCInteractionNodeRunner
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

        _openedPanel = string.IsNullOrWhiteSpace(_node.OpenData) ? UIComponent.Instance.Open(_node.UIName) : UIComponent.Instance.Open(_node.UIName, _node.OpenData);

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
            ReleaseOpenedPanel();
            _completed = true;
        }
    }

    public override bool IsCompleted(NPCInteractionSession session)
    {
        return _completed;
    }

    public override void Cancel(NPCInteractionSession session)
    {
        if (_node.WaitUntilClosed)
        {
            ReleaseOpenedPanel();
        }
    }

    private void ReleaseOpenedPanel()
    {
        if (_openedPanel == null || UIComponent.Instance == null)
            return;

        UIComponent.Instance.ReleaseUI(_openedPanel);
        _openedPanel = null;
    }
}

public sealed class NPCMoveInteractionNodeRunner : NPCInteractionNodeRunner
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

public sealed class NPCEnterDungeonInteractionNodeRunner : NPCInteractionNodeRunner
{
    private readonly NPCEnterDungeonInteractionNodeData _node;
    private bool _completed;

    public NPCEnterDungeonInteractionNodeRunner(NPCEnterDungeonInteractionNodeData node)
    {
        _node = node;
    }

    public override void Enter(NPCInteractionSession session)
    {
        _completed = true;

        if (GameFlowComponent.Instance == null)
        {
            Debug.LogWarning("[NPCInteraction] GameFlowComponent is not available for EnterDungeon node.");
            return;
        }

        int dungeonFloor = ResolveDungeonFloor(session);
        SaveDataComponent saveDataComponent = SaveDataComponent.Instance;
        SaveAreaType currentAreaType = saveDataComponent?.GetLocationData()?.AreaType ?? SaveAreaType.Town;
        if (currentAreaType != SaveAreaType.Dungeon)
            saveDataComponent?.ClearDungeonRun();

        LoadGameContext context = saveDataComponent?.CreateLoadGameContext(
            SaveAreaType.Dungeon,
            dungeonFloor);

        GameFlowComponent.Instance.BeginTransition(DungeonState.CreateEnterTransitionData(context));
    }

    private int ResolveDungeonFloor(NPCInteractionSession session)
    {
        if (session != null && session.Target != Entity.Null)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                EntityManager entityManager = world.EntityManager;
                if (entityManager.Exists(session.Target) && entityManager.HasComponent<DungeonExitComponent>(session.Target))
                    return Math.Max(1, entityManager.GetComponentData<DungeonExitComponent>(session.Target).TargetFloor);
            }
        }

        return Math.Max(1, _node.DungeonFloor);
    }

    public override bool IsCompleted(NPCInteractionSession session)
    {
        return _completed;
    }
}

public sealed class NPCEnterTrainingGroundInteractionNodeRunner : NPCInteractionNodeRunner
{
    private bool _completed;

    public NPCEnterTrainingGroundInteractionNodeRunner(NPCEnterTrainingGroundInteractionNodeData node)
    {
    }

    public override void Enter(NPCInteractionSession session)
    {
        _completed = true;

        if (GameFlowComponent.Instance == null)
        {
            Debug.LogWarning("[NPCInteraction] GameFlowComponent is not available for EnterTrainingGround node.");
            return;
        }

        LoadGameContext context = SaveDataComponent.Instance?.CreateLoadGameContext(SaveAreaType.Training);

        GameFlowComponent.Instance.BeginTransition(TrainingState.CreateEnterTransitionData(context));
    }

    public override bool IsCompleted(NPCInteractionSession session)
    {
        return _completed;
    }
}

public sealed class NPCEnterTownInteractionNodeRunner : NPCInteractionNodeRunner
{
    private bool _completed;

    public NPCEnterTownInteractionNodeRunner(NPCEnterTownInteractionNodeData node)
    {
    }

    public override void Enter(NPCInteractionSession session)
    {
        _completed = true;

        if (GameFlowComponent.Instance == null)
        {
            Debug.LogWarning("[NPCInteraction] GameFlowComponent is not available for EnterTown node.");
            return;
        }

        SaveDataComponent.Instance.CommitDungeonRunToPersistent();
        LoadGameContext context = SaveDataComponent.Instance.CreateLoadGameContext(SaveAreaType.Town);
        GameFlowComponent.Instance.SetState<ResultState>(ResultStateData.Create(ResultOutcome.Success, context));
    }

    public override bool IsCompleted(NPCInteractionSession session)
    {
        return _completed;
    }
}

public sealed class NPCSelectInteractionNodeRunner : NPCInteractionNodeRunner
{
    private readonly NPCSelectInteractionNodeData _node;
    private UIBase _openedPanel;
    private bool _completed;
    private bool _selectionResolved;

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

        if (enabledOptions.Count == 0)
        {
            session.SelectedNextNodeGuid = null;
            _completed = true;
            return;
        }

        _openedPanel = UIComponent.Instance.Open<InteractionSelectUI>(new InteractionSelectUIOpenData(
            enabledOptions,
            option => HandleOptionSelected(session, option)));

        if (_openedPanel == null)
        {
            _completed = true;
        }
    }

    public override void Update(NPCInteractionSession session, float deltaTime)
    {
        if (_completed || _openedPanel == null)
            return;

        if (!_openedPanel.gameObject.activeInHierarchy)
        {
            ReleaseOpenedPanel();
            _completed = true;
        }
    }

    public override bool IsCompleted(NPCInteractionSession session)
    {
        return _completed;
    }

    public override void Cancel(NPCInteractionSession session)
    {
        ReleaseOpenedPanel();
    }

    private void HandleOptionSelected(NPCInteractionSession session, NPCSelectOptionData option)
    {
        if (_selectionResolved)
            return;

        _selectionResolved = true;
        session.SelectedNextNodeGuid = option?.NextNodeGuid;
        _openedPanel = null;
        _completed = true;
    }

    private void ReleaseOpenedPanel()
    {
        if (_openedPanel == null)
            return;

        UIComponent.Instance.ReleaseUI(_openedPanel);
        _openedPanel = null;
    }
}
