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
        { "SpawnUnit", typeof(SpawnUnitActionNodeData) },
        { "QueryUnits", typeof(QueryUnitsActionNodeData) },
        { "ExecuteEffect", typeof(ExecuteEffectActionNodeData) },
        { "DestroySelf", typeof(DestroySelfActionNodeData) },
        { "CompleteInteraction", typeof(CompleteInteractionActionNodeData) },
        { "AcknowledgeInteraction", typeof(AcknowledgeInteractionActionNodeData) },
        { "CollectInteraction", typeof(CollectInteractionActionNodeData) },
        { "StartNpcInteraction", typeof(StartNpcInteractionActionNodeData) },
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
        { typeof(SpawnUnitActionNodeData), "SpawnUnit" },
        { typeof(QueryUnitsActionNodeData), "QueryUnits" },
        { typeof(ExecuteEffectActionNodeData), "ExecuteEffect" },
        { typeof(DestroySelfActionNodeData), "DestroySelf" },
        { typeof(CompleteInteractionActionNodeData), "CompleteInteraction" },
        { typeof(AcknowledgeInteractionActionNodeData), "AcknowledgeInteraction" },
        { typeof(CollectInteractionActionNodeData), "CollectInteraction" },
        { typeof(StartNpcInteractionActionNodeData), "StartNpcInteraction" },
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
        { "SpawnUnit", "Spawn Unit" },
        { "QueryUnits", "Query Units" },
        { "ExecuteEffect", "Execute Effect" },
        { "DestroySelf", "Destroy Self" },
        { "CompleteInteraction", "Complete Interaction" },
        { "AcknowledgeInteraction", "Acknowledge Interaction" },
        { "CollectInteraction", "Collect Interaction" },
        { "StartNpcInteraction", "Start NPC Interaction" },
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
        new("SpawnUnit", "Spawn Unit", typeof(SpawnUnitActionNodeData), 15),
        new("QueryUnits", "Query Units", typeof(QueryUnitsActionNodeData), 16),
        new("ExecuteEffect", "Execute Effect", typeof(ExecuteEffectActionNodeData), 17),
        new("DestroySelf", "Destroy Self", typeof(DestroySelfActionNodeData), 18),
        new("CompleteInteraction", "Complete Interaction", typeof(CompleteInteractionActionNodeData), 19),
        new("Timer", "Timer", typeof(TimerStateScriptNodeData), 20),
        new("Keep", "Keep", typeof(KeepStateScriptNodeData), 21),
        new("Monitor", "Monitor", typeof(MonitorStateScriptNodeData), 22),
        new("NumberMonitor", "Number Monitor", typeof(NumberMonitorStateScriptNodeData), 23),
        new("Addition", "Addition", typeof(AdditionStateScriptNodeData), 24),
        new("AcknowledgeInteraction", "Acknowledge Interaction", typeof(AcknowledgeInteractionActionNodeData), 25),
        new("CollectInteraction", "Collect Interaction", typeof(CollectInteractionActionNodeData), 26),
        new("StartNpcInteraction", "Start NPC Interaction", typeof(StartNpcInteractionActionNodeData), 27),
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
        factory.Register("SpawnUnit", static () => new SpawnUnitActionNodeData());
        factory.Register("QueryUnits", static () => new QueryUnitsActionNodeData());
        factory.Register("ExecuteEffect", static () => new ExecuteEffectActionNodeData());
        factory.Register("DestroySelf", static () => new DestroySelfActionNodeData());
        factory.Register("CompleteInteraction", static () => new CompleteInteractionActionNodeData());
        factory.Register("Timer", static () => new TimerStateScriptNodeData());
        factory.Register("Keep", static () => new KeepStateScriptNodeData());
        factory.Register("Monitor", static () => new MonitorStateScriptNodeData());
        factory.Register("NumberMonitor", static () => new NumberMonitorStateScriptNodeData());
        factory.Register("Addition", static () => new AdditionStateScriptNodeData());
        factory.Register("AcknowledgeInteraction", static () => new AcknowledgeInteractionActionNodeData());
        factory.Register("CollectInteraction", static () => new CollectInteractionActionNodeData());
        factory.Register("StartNpcInteraction", static () => new StartNpcInteractionActionNodeData());
    }

}
