// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Behavior Tree

using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

public static class BehaviorTreeRegistry
{
    private static readonly string[] s_behaviorNodeDataTypeOrder =
    {
        "Root",
        "Selector",
        "Sequence",
        "Parallel",
        "CheckCondition",
        "MoveToTarget",
        "CastToTarget",
        "Wander",
        "Idle",
        "Inverter",
        "Succeeder",
        "Failer",
        "Repeater",
        "UntilSuccess",
        "UntilFailure",
        "Cooldown",
        "Timeout",
    };

    private static readonly Dictionary<string, Type> s_behaviorNodeDataTypes = new(StringComparer.Ordinal)
    {
        { "Root", typeof(CrystalMagic.Game.Data.RootBehaviorNodeData) },
        { "Selector", typeof(CrystalMagic.Game.Data.SelectorBehaviorNodeData) },
        { "Sequence", typeof(CrystalMagic.Game.Data.SequenceBehaviorNodeData) },
        { "Parallel", typeof(CrystalMagic.Game.Data.ParallelBehaviorNodeData) },
        { "CheckCondition", typeof(CrystalMagic.Game.Data.CheckConditionBehaviorNodeData) },
        { "MoveToTarget", typeof(CrystalMagic.Game.Data.MoveToTargetBehaviorNodeData) },
        { "CastToTarget", typeof(CrystalMagic.Game.Data.CastToTargetBehaviorNodeData) },
        { "Wander", typeof(CrystalMagic.Game.Data.WanderBehaviorNodeData) },
        { "Idle", typeof(CrystalMagic.Game.Data.IdleBehaviorNodeData) },
        { "Inverter", typeof(CrystalMagic.Game.Data.InverterBehaviorNodeData) },
        { "Succeeder", typeof(CrystalMagic.Game.Data.SucceederBehaviorNodeData) },
        { "Failer", typeof(CrystalMagic.Game.Data.FailerBehaviorNodeData) },
        { "Repeater", typeof(CrystalMagic.Game.Data.RepeaterBehaviorNodeData) },
        { "UntilSuccess", typeof(CrystalMagic.Game.Data.UntilSuccessBehaviorNodeData) },
        { "UntilFailure", typeof(CrystalMagic.Game.Data.UntilFailureBehaviorNodeData) },
        { "Cooldown", typeof(CrystalMagic.Game.Data.CooldownBehaviorNodeData) },
        { "Timeout", typeof(CrystalMagic.Game.Data.TimeoutBehaviorNodeData) },
    };

    private static readonly Dictionary<Type, string> s_behaviorNodeDataKeys = new()
    {
        { typeof(CrystalMagic.Game.Data.RootBehaviorNodeData), "Root" },
        { typeof(CrystalMagic.Game.Data.SelectorBehaviorNodeData), "Selector" },
        { typeof(CrystalMagic.Game.Data.SequenceBehaviorNodeData), "Sequence" },
        { typeof(CrystalMagic.Game.Data.ParallelBehaviorNodeData), "Parallel" },
        { typeof(CrystalMagic.Game.Data.CheckConditionBehaviorNodeData), "CheckCondition" },
        { typeof(CrystalMagic.Game.Data.MoveToTargetBehaviorNodeData), "MoveToTarget" },
        { typeof(CrystalMagic.Game.Data.CastToTargetBehaviorNodeData), "CastToTarget" },
        { typeof(CrystalMagic.Game.Data.WanderBehaviorNodeData), "Wander" },
        { typeof(CrystalMagic.Game.Data.IdleBehaviorNodeData), "Idle" },
        { typeof(CrystalMagic.Game.Data.InverterBehaviorNodeData), "Inverter" },
        { typeof(CrystalMagic.Game.Data.SucceederBehaviorNodeData), "Succeeder" },
        { typeof(CrystalMagic.Game.Data.FailerBehaviorNodeData), "Failer" },
        { typeof(CrystalMagic.Game.Data.RepeaterBehaviorNodeData), "Repeater" },
        { typeof(CrystalMagic.Game.Data.UntilSuccessBehaviorNodeData), "UntilSuccess" },
        { typeof(CrystalMagic.Game.Data.UntilFailureBehaviorNodeData), "UntilFailure" },
        { typeof(CrystalMagic.Game.Data.CooldownBehaviorNodeData), "Cooldown" },
        { typeof(CrystalMagic.Game.Data.TimeoutBehaviorNodeData), "Timeout" },
    };

    private static readonly Dictionary<string, string> s_behaviorNodeDataDisplayNames = new(StringComparer.Ordinal)
    {
        { "Root", "Root" },
        { "Selector", "Selector" },
        { "Sequence", "Sequence" },
        { "Parallel", "Parallel" },
        { "CheckCondition", "Check Condition" },
        { "MoveToTarget", "Move To Target" },
        { "CastToTarget", "Cast To Target" },
        { "Wander", "Wander" },
        { "Idle", "Idle" },
        { "Inverter", "Inverter" },
        { "Succeeder", "Succeeder" },
        { "Failer", "Failer" },
        { "Repeater", "Repeater" },
        { "UntilSuccess", "Until Success" },
        { "UntilFailure", "Until Failure" },
        { "Cooldown", "Cooldown" },
        { "Timeout", "Timeout" },
    };

    private static readonly FactoryTypeInfo[] s_behaviorNodeDataTypeInfos =
    {
        new("Root", "Root", typeof(CrystalMagic.Game.Data.RootBehaviorNodeData), -100),
        new("Selector", "Selector", typeof(CrystalMagic.Game.Data.SelectorBehaviorNodeData), 0),
        new("Sequence", "Sequence", typeof(CrystalMagic.Game.Data.SequenceBehaviorNodeData), 1),
        new("Parallel", "Parallel", typeof(CrystalMagic.Game.Data.ParallelBehaviorNodeData), 2),
        new("CheckCondition", "Check Condition", typeof(CrystalMagic.Game.Data.CheckConditionBehaviorNodeData), 10),
        new("MoveToTarget", "Move To Target", typeof(CrystalMagic.Game.Data.MoveToTargetBehaviorNodeData), 13),
        new("CastToTarget", "Cast To Target", typeof(CrystalMagic.Game.Data.CastToTargetBehaviorNodeData), 14),
        new("Wander", "Wander", typeof(CrystalMagic.Game.Data.WanderBehaviorNodeData), 15),
        new("Idle", "Idle", typeof(CrystalMagic.Game.Data.IdleBehaviorNodeData), 16),
        new("Inverter", "Inverter", typeof(CrystalMagic.Game.Data.InverterBehaviorNodeData), 20),
        new("Succeeder", "Succeeder", typeof(CrystalMagic.Game.Data.SucceederBehaviorNodeData), 21),
        new("Failer", "Failer", typeof(CrystalMagic.Game.Data.FailerBehaviorNodeData), 22),
        new("Repeater", "Repeater", typeof(CrystalMagic.Game.Data.RepeaterBehaviorNodeData), 23),
        new("UntilSuccess", "Until Success", typeof(CrystalMagic.Game.Data.UntilSuccessBehaviorNodeData), 24),
        new("UntilFailure", "Until Failure", typeof(CrystalMagic.Game.Data.UntilFailureBehaviorNodeData), 25),
        new("Cooldown", "Cooldown", typeof(CrystalMagic.Game.Data.CooldownBehaviorNodeData), 26),
        new("Timeout", "Timeout", typeof(CrystalMagic.Game.Data.TimeoutBehaviorNodeData), 27),
    };

    public static string DefaultBehaviorNodeDataKey => "Root";

    public static IReadOnlyList<string> BehaviorNodeDataTypeOrder => s_behaviorNodeDataTypeOrder;

    public static IReadOnlyList<FactoryTypeInfo> BehaviorNodeDataTypeInfos => s_behaviorNodeDataTypeInfos;

    public static bool ContainsBehaviorNodeDataKey(string key)
    {
        return s_behaviorNodeDataTypes.ContainsKey(key ?? string.Empty);
    }

    public static bool TryGetBehaviorNodeDataType(string key, out Type type)
    {
        return s_behaviorNodeDataTypes.TryGetValue(key ?? string.Empty, out type);
    }

    public static bool TryGetBehaviorNodeDataKey(Type type, out string key)
    {
        return s_behaviorNodeDataKeys.TryGetValue(type, out key);
    }

    public static string GetBehaviorNodeDataDisplayName(string key)
    {
        return s_behaviorNodeDataDisplayNames.TryGetValue(key ?? string.Empty, out string displayName)
            ? displayName
            : key ?? "Unknown";
    }

    public static void RegisterAll(BehaviorNodeDataFactory factory)
    {
        if (factory == null)
            return;

        factory.Register("Root", static () => new CrystalMagic.Game.Data.RootBehaviorNodeData());
        factory.Register("Selector", static () => new CrystalMagic.Game.Data.SelectorBehaviorNodeData());
        factory.Register("Sequence", static () => new CrystalMagic.Game.Data.SequenceBehaviorNodeData());
        factory.Register("Parallel", static () => new CrystalMagic.Game.Data.ParallelBehaviorNodeData());
        factory.Register("CheckCondition", static () => new CrystalMagic.Game.Data.CheckConditionBehaviorNodeData());
        factory.Register("MoveToTarget", static () => new CrystalMagic.Game.Data.MoveToTargetBehaviorNodeData());
        factory.Register("CastToTarget", static () => new CrystalMagic.Game.Data.CastToTargetBehaviorNodeData());
        factory.Register("Wander", static () => new CrystalMagic.Game.Data.WanderBehaviorNodeData());
        factory.Register("Idle", static () => new CrystalMagic.Game.Data.IdleBehaviorNodeData());
        factory.Register("Inverter", static () => new CrystalMagic.Game.Data.InverterBehaviorNodeData());
        factory.Register("Succeeder", static () => new CrystalMagic.Game.Data.SucceederBehaviorNodeData());
        factory.Register("Failer", static () => new CrystalMagic.Game.Data.FailerBehaviorNodeData());
        factory.Register("Repeater", static () => new CrystalMagic.Game.Data.RepeaterBehaviorNodeData());
        factory.Register("UntilSuccess", static () => new CrystalMagic.Game.Data.UntilSuccessBehaviorNodeData());
        factory.Register("UntilFailure", static () => new CrystalMagic.Game.Data.UntilFailureBehaviorNodeData());
        factory.Register("Cooldown", static () => new CrystalMagic.Game.Data.CooldownBehaviorNodeData());
        factory.Register("Timeout", static () => new CrystalMagic.Game.Data.TimeoutBehaviorNodeData());
    }

    public static void RegisterAll(BehaviorNodeFactory factory)
    {
        if (factory == null)
            return;

        factory.Register(typeof(CrystalMagic.Game.Data.RootBehaviorNodeData), static data => new RootBehaviorNode((CrystalMagic.Game.Data.RootBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.SelectorBehaviorNodeData), static data => new SelectorBehaviorNode((CrystalMagic.Game.Data.SelectorBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.SequenceBehaviorNodeData), static data => new SequenceBehaviorNode((CrystalMagic.Game.Data.SequenceBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.ParallelBehaviorNodeData), static data => new ParallelBehaviorNode((CrystalMagic.Game.Data.ParallelBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.CheckConditionBehaviorNodeData), static data => new CheckConditionBehaviorNode((CrystalMagic.Game.Data.CheckConditionBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.MoveToTargetBehaviorNodeData), static data => new MoveToTargetBehaviorNode((CrystalMagic.Game.Data.MoveToTargetBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.CastToTargetBehaviorNodeData), static data => new CastToTargetBehaviorNode((CrystalMagic.Game.Data.CastToTargetBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.WanderBehaviorNodeData), static data => new WanderBehaviorNode((CrystalMagic.Game.Data.WanderBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.IdleBehaviorNodeData), static data => new IdleBehaviorNode((CrystalMagic.Game.Data.IdleBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.InverterBehaviorNodeData), static data => new InverterBehaviorNode((CrystalMagic.Game.Data.InverterBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.SucceederBehaviorNodeData), static data => new SucceederBehaviorNode((CrystalMagic.Game.Data.SucceederBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.FailerBehaviorNodeData), static data => new FailerBehaviorNode((CrystalMagic.Game.Data.FailerBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.RepeaterBehaviorNodeData), static data => new RepeaterBehaviorNode((CrystalMagic.Game.Data.RepeaterBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.UntilSuccessBehaviorNodeData), static data => new UntilSuccessBehaviorNode((CrystalMagic.Game.Data.UntilSuccessBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.UntilFailureBehaviorNodeData), static data => new UntilFailureBehaviorNode((CrystalMagic.Game.Data.UntilFailureBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.CooldownBehaviorNodeData), static data => new CooldownBehaviorNode((CrystalMagic.Game.Data.CooldownBehaviorNodeData)data));
        factory.Register(typeof(CrystalMagic.Game.Data.TimeoutBehaviorNodeData), static data => new TimeoutBehaviorNode((CrystalMagic.Game.Data.TimeoutBehaviorNodeData)data));
    }
}
