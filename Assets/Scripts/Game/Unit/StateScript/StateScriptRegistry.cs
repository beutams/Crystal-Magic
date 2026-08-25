// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/State Script

using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

public static class StateScriptRegistry
{
    private static readonly Dictionary<string, Type> s_nodeDataTypes = new(StringComparer.Ordinal)
    {
        { "Entry", typeof(StateScriptEntryNodeData) },
        { "Compare", typeof(CompareStateScriptNodeData) },
        { "SetValue", typeof(SetValueStateScriptNodeData) },
        { "RequestSkill", typeof(RequestSkillActionNodeData) },
        { "PublishGameEvent", typeof(PublishGameEventStateScriptNodeData) },
        { "RequestSkillWithAddition", typeof(RequestSkillWithAdditionActionNodeData) },
        { "RequestInteraction", typeof(RequestInteractionActionNodeData) },
        { "Timer", typeof(TimerStateScriptNodeData) },
        { "Keep", typeof(KeepStateScriptNodeData) },
        { "Monitor", typeof(MonitorStateScriptNodeData) },
        { "NumberMonitor", typeof(NumberMonitorStateScriptNodeData) },
        { "Addition", typeof(AdditionStateScriptNodeData) },
    };

    private static readonly Dictionary<Type, string> s_nodeDataKeys = new()
    {
        { typeof(StateScriptEntryNodeData), "Entry" },
        { typeof(CompareStateScriptNodeData), "Compare" },
        { typeof(SetValueStateScriptNodeData), "SetValue" },
        { typeof(RequestSkillActionNodeData), "RequestSkill" },
        { typeof(PublishGameEventStateScriptNodeData), "PublishGameEvent" },
        { typeof(RequestSkillWithAdditionActionNodeData), "RequestSkillWithAddition" },
        { typeof(RequestInteractionActionNodeData), "RequestInteraction" },
        { typeof(TimerStateScriptNodeData), "Timer" },
        { typeof(KeepStateScriptNodeData), "Keep" },
        { typeof(MonitorStateScriptNodeData), "Monitor" },
        { typeof(NumberMonitorStateScriptNodeData), "NumberMonitor" },
        { typeof(AdditionStateScriptNodeData), "Addition" },
    };

    private static readonly Dictionary<string, string> s_nodeDataDisplayNames = new(StringComparer.Ordinal)
    {
        { "Entry", "Entry" },
        { "Compare", "Compare" },
        { "SetValue", "Set Value" },
        { "RequestSkill", "Request Skill" },
        { "PublishGameEvent", "Publish Game Event" },
        { "RequestSkillWithAddition", "Request Skill With Addition" },
        { "RequestInteraction", "Request Interaction" },
        { "Timer", "Timer" },
        { "Keep", "Keep" },
        { "Monitor", "Monitor" },
        { "NumberMonitor", "Number Monitor" },
        { "Addition", "Addition" },
    };

    private static readonly FactoryTypeInfo[] s_nodeDataTypeInfos =
    {
        new("Entry", "Entry", typeof(StateScriptEntryNodeData), -100),
        new("Compare", "Compare", typeof(CompareStateScriptNodeData), 0),
        new("SetValue", "Set Value", typeof(SetValueStateScriptNodeData), 10),
        new("RequestSkill", "Request Skill", typeof(RequestSkillActionNodeData), 11),
        new("PublishGameEvent", "Publish Game Event", typeof(PublishGameEventStateScriptNodeData), 12),
        new("RequestSkillWithAddition", "Request Skill With Addition", typeof(RequestSkillWithAdditionActionNodeData), 13),
        new("RequestInteraction", "Request Interaction", typeof(RequestInteractionActionNodeData), 14),
        new("Timer", "Timer", typeof(TimerStateScriptNodeData), 20),
        new("Keep", "Keep", typeof(KeepStateScriptNodeData), 21),
        new("Monitor", "Monitor", typeof(MonitorStateScriptNodeData), 22),
        new("NumberMonitor", "Number Monitor", typeof(NumberMonitorStateScriptNodeData), 23),
        new("Addition", "Addition", typeof(AdditionStateScriptNodeData), 24),
    };

    public static string DefaultNodeDataKey => "Entry";
    public static IReadOnlyList<FactoryTypeInfo> NodeDataTypeInfos => s_nodeDataTypeInfos;

    public static bool ContainsNodeDataKey(string key) => s_nodeDataTypes.ContainsKey(key ?? string.Empty);
    public static bool TryGetNodeDataType(string key, out Type type) => s_nodeDataTypes.TryGetValue(key ?? string.Empty, out type);
    public static bool TryGetNodeDataKey(Type type, out string key) => s_nodeDataKeys.TryGetValue(type, out key);
    public static string GetNodeDataDisplayName(string key) => s_nodeDataDisplayNames.TryGetValue(key ?? string.Empty, out string displayName) ? displayName : key ?? "Unknown";

    public static void RegisterAll(StateScriptNodeDataFactory factory)
    {
        if (factory == null)
            return;

        factory.Register("Entry", static () => new StateScriptEntryNodeData());
        factory.Register("Compare", static () => new CompareStateScriptNodeData());
        factory.Register("SetValue", static () => new SetValueStateScriptNodeData());
        factory.Register("RequestSkill", static () => new RequestSkillActionNodeData());
        factory.Register("PublishGameEvent", static () => new PublishGameEventStateScriptNodeData());
        factory.Register("RequestSkillWithAddition", static () => new RequestSkillWithAdditionActionNodeData());
        factory.Register("RequestInteraction", static () => new RequestInteractionActionNodeData());
        factory.Register("Timer", static () => new TimerStateScriptNodeData());
        factory.Register("Keep", static () => new KeepStateScriptNodeData());
        factory.Register("Monitor", static () => new MonitorStateScriptNodeData());
        factory.Register("NumberMonitor", static () => new NumberMonitorStateScriptNodeData());
        factory.Register("Addition", static () => new AdditionStateScriptNodeData());
    }

    public static void RegisterAll(StateScriptNodeRuntimeFactory factory)
    {
        if (factory == null)
            return;

        factory.Register(typeof(StateScriptEntryNodeData), static request =>
            new StateScriptEntryNode((StateScriptEntryNodeData)request.Data, request.Runtime));
        factory.Register(typeof(CompareStateScriptNodeData), static request =>
            new CompareStateScriptNode((CompareStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(SetValueStateScriptNodeData), static request =>
            new SetValueStateScriptNode((SetValueStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(RequestSkillActionNodeData), static request =>
            new RequestSkillActionNode((RequestSkillActionNodeData)request.Data, request.Runtime));
        factory.Register(typeof(PublishGameEventStateScriptNodeData), static request =>
            new PublishGameEventStateScriptNode((PublishGameEventStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(RequestSkillWithAdditionActionNodeData), static request =>
            new RequestSkillWithAdditionActionNode((RequestSkillWithAdditionActionNodeData)request.Data, request.Runtime));
        factory.Register(typeof(RequestInteractionActionNodeData), static request =>
            new RequestInteractionActionNode((RequestInteractionActionNodeData)request.Data, request.Runtime));
        factory.Register(typeof(TimerStateScriptNodeData), static request =>
            new TimerStateScriptNode((TimerStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(KeepStateScriptNodeData), static request =>
            new KeepStateScriptNode((KeepStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(MonitorStateScriptNodeData), static request =>
            new MonitorStateScriptNode((MonitorStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(NumberMonitorStateScriptNodeData), static request =>
            new NumberMonitorStateScriptNode((NumberMonitorStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(AdditionStateScriptNodeData), static request =>
            new AdditionStateScriptNode((AdditionStateScriptNodeData)request.Data, request.Runtime));
    }
}
