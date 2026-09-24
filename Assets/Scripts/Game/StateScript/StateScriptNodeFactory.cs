using System;
using CrystalMagic.Game.Data;

public sealed class StateScriptNodeDataFactory : GeneratedFactory<string, StateScriptNodeData>
{
    public StateScriptNodeDataFactory()
        : base(StringComparer.Ordinal)
    {
    }

    public StateScriptNodeData CreateNode(string typeName, bool assignGuid = true)
    {
        StateScriptNodeData node = Create(typeName);
        if (node == null)
            return null;

        node.Type = typeName;
        if (assignGuid)
            node.Guid = Guid.NewGuid().ToString("N");

        return node;
    }
}

public sealed class StateScriptNodeSchema
{
    public StateScriptNodeSchema(string[] inputs, string[] outputs)
    {
        Inputs = inputs ?? System.Array.Empty<string>();
        Outputs = outputs ?? System.Array.Empty<string>();
    }

    public System.Collections.Generic.IReadOnlyList<string> Inputs { get; }
    public System.Collections.Generic.IReadOnlyList<string> Outputs { get; }
}

public static class StateScriptNodeSchemaUtility
{
    private static readonly string[] s_actionInputs = { "In" };
    private static readonly string[] s_actionOutputs = { "Out" };
    private static readonly string[] s_stateInputs = { "Start", "Abort" };
    private static readonly string[] s_stateOutputs = { "OnStart", "OnTick", "OnComplete", "OnAbort", "OnStop" };

    public static StateScriptNodeSchema Create(StateScriptNodeData data)
    {
        return data switch
        {
            StateScriptEntryNodeData => new StateScriptNodeSchema(
                System.Array.Empty<string>(),
                s_actionOutputs),
            CompareStateScriptNodeData => new StateScriptNodeSchema(
                new[] { "Check" },
                new[] { "True", "False" }),
            KeepStateScriptNodeData => new StateScriptNodeSchema(
                new[] { "Start", "Abort", "Keep" },
                new[]
                {
                    "OnStart", "OnTick", "OnComplete", "OnAbort", "OnStop",
                    "OnTimeStart", "OnTimeTick", "OnTimeComplete", "OnTimeStop",
                }),
            MonitorStateScriptNodeData => new StateScriptNodeSchema(
                s_stateInputs,
                new[]
                {
                    "OnStart", "OnTick", "OnComplete", "OnAbort", "OnStop",
                    "True", "False", "OnChangeTrue", "OnChangeFalse",
                }),
            NumberMonitorStateScriptNodeData => new StateScriptNodeSchema(
                s_stateInputs,
                new[] { "OnStart", "OnTick", "OnComplete", "OnAbort", "OnStop", "OnValueChange" }),
            PlayerInputEventStateScriptNodeData => new StateScriptNodeSchema(
                s_stateInputs,
                new[] { "OnStart", "OnTick", "OnComplete", "OnAbort", "OnStop", "OnEvent" }),
            StateStateScriptNodeData => new StateScriptNodeSchema(s_stateInputs, s_stateOutputs),
            ActionStateScriptNodeData => new StateScriptNodeSchema(s_actionInputs, s_actionOutputs),
            _ => null,
        };
    }
}

public static class StateScriptNodeDataRegistry
{
    private static readonly StateScriptNodeDataFactory s_factory = CreateFactory();

    public static System.Collections.Generic.IReadOnlyList<FactoryTypeInfo> TypeInfos => StateScriptRegistry.NodeDataTypeInfos;

    public static bool TryGetNodeType(string typeName, out Type type)
    {
        return StateScriptRegistry.TryGetNodeDataType(typeName, out type);
    }

    public static string ResolveTypeName(StateScriptNodeData node)
    {
        if (node == null)
            return StateScriptRegistry.DefaultNodeDataKey;

        if (!string.IsNullOrWhiteSpace(node.Type) && StateScriptRegistry.ContainsNodeDataKey(node.Type))
            return node.Type;

        return StateScriptRegistry.TryGetNodeDataKey(node.GetType(), out string key)
            ? key
            : StateScriptRegistry.DefaultNodeDataKey;
    }

    public static StateScriptNodeData Create(string typeName, bool assignGuid = true)
    {
        if (!StateScriptRegistry.ContainsNodeDataKey(typeName))
            typeName = StateScriptRegistry.DefaultNodeDataKey;

        return s_factory.CreateNode(typeName, assignGuid);
    }

    public static string GetDisplayName(string typeName)
    {
        return StateScriptRegistry.GetNodeDataDisplayName(typeName);
    }

    private static StateScriptNodeDataFactory CreateFactory()
    {
        StateScriptNodeDataFactory factory = new();
        StateScriptRegistry.RegisterAll(factory);
        return factory;
    }
}
