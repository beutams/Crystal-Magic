using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;

[FactoryKey("Addition", 24, "Addition")]
public sealed class AdditionStateScriptNode : StateScriptStateNode
{
    private readonly AdditionStateScriptNodeData _data;
    private readonly List<SkillAdditionAction> _actions = new();

    public AdditionStateScriptNode(AdditionStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
    }

    protected override bool OnBind(out string error)
    {
        if (string.IsNullOrWhiteSpace(_data.EventName))
        {
            error = "Addition EventName is empty.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    protected override void OnActivate()
    {
        _actions.Clear();
        _actions.AddRange(SkillAdditionEventDispatcher.CreateActions(Runtime, _data.EventName));
    }

    protected override void OnUpdate()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
        {
            SkillAdditionAction action = _actions[i];
            action?.Tick();
            if (action == null || action.Status != SkillAdditionActionStatus.Running)
                _actions.RemoveAt(i);
        }

        if (_actions.Count == 0)
            Complete();
    }

    protected override void OnDeactivate()
    {
        for (int i = 0; i < _actions.Count; i++)
            _actions[i]?.Stop();

        _actions.Clear();
    }
}
