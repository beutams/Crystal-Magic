// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Behavior Tree

using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

public static class BehaviorTreeRegistry
{
    private static readonly string[] s_behaviorNodeDataTypeOrder =
    {
        "Root", "Selector", "Sequence", "Parallel", "Check", "Set", "Wait",
        "Inverter", "Succeeder", "Failer", "Repeater", "UntilSuccess", "UntilFailure", "Cooldown", "Timeout",
    };

    private static readonly Dictionary<string, Type> s_behaviorNodeDataTypes = new(StringComparer.Ordinal)
    {
        { "Root", typeof(RootBehaviorNodeData) },
        { "Selector", typeof(SelectorBehaviorNodeData) },
        { "Sequence", typeof(SequenceBehaviorNodeData) },
        { "Parallel", typeof(ParallelBehaviorNodeData) },
        { "Check", typeof(CheckBehaviorNodeData) },
        { "Set", typeof(SetBehaviorNodeData) },
        { "Wait", typeof(WaitBehaviorNodeData) },
        { "Inverter", typeof(InverterBehaviorNodeData) },
        { "Succeeder", typeof(SucceederBehaviorNodeData) },
        { "Failer", typeof(FailerBehaviorNodeData) },
        { "Repeater", typeof(RepeaterBehaviorNodeData) },
        { "UntilSuccess", typeof(UntilSuccessBehaviorNodeData) },
        { "UntilFailure", typeof(UntilFailureBehaviorNodeData) },
        { "Cooldown", typeof(CooldownBehaviorNodeData) },
        { "Timeout", typeof(TimeoutBehaviorNodeData) },
    };

    private static readonly Dictionary<Type, string> s_behaviorNodeDataKeys = new()
    {
        { typeof(RootBehaviorNodeData), "Root" },
        { typeof(SelectorBehaviorNodeData), "Selector" },
        { typeof(SequenceBehaviorNodeData), "Sequence" },
        { typeof(ParallelBehaviorNodeData), "Parallel" },
        { typeof(CheckBehaviorNodeData), "Check" },
        { typeof(SetBehaviorNodeData), "Set" },
        { typeof(WaitBehaviorNodeData), "Wait" },
        { typeof(InverterBehaviorNodeData), "Inverter" },
        { typeof(SucceederBehaviorNodeData), "Succeeder" },
        { typeof(FailerBehaviorNodeData), "Failer" },
        { typeof(RepeaterBehaviorNodeData), "Repeater" },
        { typeof(UntilSuccessBehaviorNodeData), "UntilSuccess" },
        { typeof(UntilFailureBehaviorNodeData), "UntilFailure" },
        { typeof(CooldownBehaviorNodeData), "Cooldown" },
        { typeof(TimeoutBehaviorNodeData), "Timeout" },
    };

    private static readonly Dictionary<string, string> s_behaviorNodeDataDisplayNames = new(StringComparer.Ordinal)
    {
        { "Root", "Root" }, { "Selector", "Selector" }, { "Sequence", "Sequence" }, { "Parallel", "Parallel" },
        { "Check", "Check" }, { "Set", "Set" }, { "Wait", "Wait" },
        { "Inverter", "Inverter" }, { "Succeeder", "Succeeder" }, { "Failer", "Failer" },
        { "Repeater", "Repeater" }, { "UntilSuccess", "Until Success" }, { "UntilFailure", "Until Failure" },
        { "Cooldown", "Cooldown" }, { "Timeout", "Timeout" },
    };

    private static readonly FactoryTypeInfo[] s_behaviorNodeDataTypeInfos =
    {
        new("Root", "Root", typeof(RootBehaviorNodeData), -100),
        new("Selector", "Selector", typeof(SelectorBehaviorNodeData), 0),
        new("Sequence", "Sequence", typeof(SequenceBehaviorNodeData), 1),
        new("Parallel", "Parallel", typeof(ParallelBehaviorNodeData), 2),
        new("Check", "Check", typeof(CheckBehaviorNodeData), 10),
        new("Set", "Set", typeof(SetBehaviorNodeData), 11),
        new("Wait", "Wait", typeof(WaitBehaviorNodeData), 12),
        new("Inverter", "Inverter", typeof(InverterBehaviorNodeData), 20),
        new("Succeeder", "Succeeder", typeof(SucceederBehaviorNodeData), 21),
        new("Failer", "Failer", typeof(FailerBehaviorNodeData), 22),
        new("Repeater", "Repeater", typeof(RepeaterBehaviorNodeData), 23),
        new("UntilSuccess", "Until Success", typeof(UntilSuccessBehaviorNodeData), 24),
        new("UntilFailure", "Until Failure", typeof(UntilFailureBehaviorNodeData), 25),
        new("Cooldown", "Cooldown", typeof(CooldownBehaviorNodeData), 26),
        new("Timeout", "Timeout", typeof(TimeoutBehaviorNodeData), 27),
    };

    public static string DefaultBehaviorNodeDataKey => "Root";
    public static IReadOnlyList<string> BehaviorNodeDataTypeOrder => s_behaviorNodeDataTypeOrder;
    public static IReadOnlyList<FactoryTypeInfo> BehaviorNodeDataTypeInfos => s_behaviorNodeDataTypeInfos;

    public static bool ContainsBehaviorNodeDataKey(string key) => s_behaviorNodeDataTypes.ContainsKey(key ?? string.Empty);
    public static bool TryGetBehaviorNodeDataType(string key, out Type type) => s_behaviorNodeDataTypes.TryGetValue(key ?? string.Empty, out type);
    public static bool TryGetBehaviorNodeDataKey(Type type, out string key) => s_behaviorNodeDataKeys.TryGetValue(type, out key);
    public static string GetBehaviorNodeDataDisplayName(string key) => s_behaviorNodeDataDisplayNames.TryGetValue(key ?? string.Empty, out string displayName) ? displayName : key ?? "Unknown";

    public static void RegisterAll(BehaviorNodeDataFactory factory)
    {
        if (factory == null)
            return;

        factory.Register("Root", static () => new RootBehaviorNodeData());
        factory.Register("Selector", static () => new SelectorBehaviorNodeData());
        factory.Register("Sequence", static () => new SequenceBehaviorNodeData());
        factory.Register("Parallel", static () => new ParallelBehaviorNodeData());
        factory.Register("Check", static () => new CheckBehaviorNodeData());
        factory.Register("Set", static () => new SetBehaviorNodeData());
        factory.Register("Wait", static () => new WaitBehaviorNodeData());
        factory.Register("Inverter", static () => new InverterBehaviorNodeData());
        factory.Register("Succeeder", static () => new SucceederBehaviorNodeData());
        factory.Register("Failer", static () => new FailerBehaviorNodeData());
        factory.Register("Repeater", static () => new RepeaterBehaviorNodeData());
        factory.Register("UntilSuccess", static () => new UntilSuccessBehaviorNodeData());
        factory.Register("UntilFailure", static () => new UntilFailureBehaviorNodeData());
        factory.Register("Cooldown", static () => new CooldownBehaviorNodeData());
        factory.Register("Timeout", static () => new TimeoutBehaviorNodeData());
    }

    public static void RegisterAll(BehaviorNodeFactory factory)
    {
        if (factory == null)
            return;

        factory.Register(typeof(RootBehaviorNodeData), static data => new RootBehaviorNode((RootBehaviorNodeData)data));
        factory.Register(typeof(SelectorBehaviorNodeData), static data => new SelectorBehaviorNode((SelectorBehaviorNodeData)data));
        factory.Register(typeof(SequenceBehaviorNodeData), static data => new SequenceBehaviorNode((SequenceBehaviorNodeData)data));
        factory.Register(typeof(ParallelBehaviorNodeData), static data => new ParallelBehaviorNode((ParallelBehaviorNodeData)data));
        factory.Register(typeof(CheckBehaviorNodeData), static data => new CheckBehaviorNode((CheckBehaviorNodeData)data));
        factory.Register(typeof(SetBehaviorNodeData), static data => new SetBehaviorNode((SetBehaviorNodeData)data));
        factory.Register(typeof(WaitBehaviorNodeData), static data => new WaitBehaviorNode((WaitBehaviorNodeData)data));
        factory.Register(typeof(InverterBehaviorNodeData), static data => new InverterBehaviorNode((InverterBehaviorNodeData)data));
        factory.Register(typeof(SucceederBehaviorNodeData), static data => new SucceederBehaviorNode((SucceederBehaviorNodeData)data));
        factory.Register(typeof(FailerBehaviorNodeData), static data => new FailerBehaviorNode((FailerBehaviorNodeData)data));
        factory.Register(typeof(RepeaterBehaviorNodeData), static data => new RepeaterBehaviorNode((RepeaterBehaviorNodeData)data));
        factory.Register(typeof(UntilSuccessBehaviorNodeData), static data => new UntilSuccessBehaviorNode((UntilSuccessBehaviorNodeData)data));
        factory.Register(typeof(UntilFailureBehaviorNodeData), static data => new UntilFailureBehaviorNode((UntilFailureBehaviorNodeData)data));
        factory.Register(typeof(CooldownBehaviorNodeData), static data => new CooldownBehaviorNode((CooldownBehaviorNodeData)data));
        factory.Register(typeof(TimeoutBehaviorNodeData), static data => new TimeoutBehaviorNode((TimeoutBehaviorNodeData)data));
    }
}
