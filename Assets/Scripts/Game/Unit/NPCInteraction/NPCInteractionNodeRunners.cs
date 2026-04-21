using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
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

public sealed class NPCEnterTrainingGroundInteractionNodeRunner : NPCInteractionNodeRunner
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

public sealed class NPCSelectInteractionNodeRunner : NPCInteractionNodeRunner
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
