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
        { "Timer", typeof(TimerStateScriptNodeData) },
        { "Keep", typeof(KeepStateScriptNodeData) },
        { "Monitor", typeof(MonitorStateScriptNodeData) },
        { "NumberMonitor", typeof(NumberMonitorStateScriptNodeData) },
    };

    private static readonly Dictionary<Type, string> s_nodeDataKeys = new()
    {
        { typeof(StateScriptEntryNodeData), "Entry" },
        { typeof(CompareStateScriptNodeData), "Compare" },
        { typeof(SetValueStateScriptNodeData), "SetValue" },
        { typeof(RequestSkillActionNodeData), "RequestSkill" },
        { typeof(TimerStateScriptNodeData), "Timer" },
        { typeof(KeepStateScriptNodeData), "Keep" },
        { typeof(MonitorStateScriptNodeData), "Monitor" },
        { typeof(NumberMonitorStateScriptNodeData), "NumberMonitor" },
    };

    private static readonly Dictionary<string, string> s_nodeDataDisplayNames = new(StringComparer.Ordinal)
    {
        { "Entry", "Entry" },
        { "Compare", "Compare" },
        { "SetValue", "Set Value" },
        { "RequestSkill", "Request Skill" },
        { "Timer", "Timer" },
        { "Keep", "Keep" },
        { "Monitor", "Monitor" },
        { "NumberMonitor", "Number Monitor" },
    };

    private static readonly FactoryTypeInfo[] s_nodeDataTypeInfos =
    {
        new("Entry", "Entry", typeof(StateScriptEntryNodeData), -100),
        new("Compare", "Compare", typeof(CompareStateScriptNodeData), 0),
        new("SetValue", "Set Value", typeof(SetValueStateScriptNodeData), 10),
        new("RequestSkill", "Request Skill", typeof(RequestSkillActionNodeData), 11),
        new("Timer", "Timer", typeof(TimerStateScriptNodeData), 20),
        new("Keep", "Keep", typeof(KeepStateScriptNodeData), 21),
        new("Monitor", "Monitor", typeof(MonitorStateScriptNodeData), 22),
        new("NumberMonitor", "Number Monitor", typeof(NumberMonitorStateScriptNodeData), 23),
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
        factory.Register("Timer", static () => new TimerStateScriptNodeData());
        factory.Register("Keep", static () => new KeepStateScriptNodeData());
        factory.Register("Monitor", static () => new MonitorStateScriptNodeData());
        factory.Register("NumberMonitor", static () => new NumberMonitorStateScriptNodeData());
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
        factory.Register(typeof(TimerStateScriptNodeData), static request =>
            new TimerStateScriptNode((TimerStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(KeepStateScriptNodeData), static request =>
            new KeepStateScriptNode((KeepStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(MonitorStateScriptNodeData), static request =>
            new MonitorStateScriptNode((MonitorStateScriptNodeData)request.Data, request.Runtime));
        factory.Register(typeof(NumberMonitorStateScriptNodeData), static request =>
            new NumberMonitorStateScriptNode((NumberMonitorStateScriptNodeData)request.Data, request.Runtime));
    }
}
