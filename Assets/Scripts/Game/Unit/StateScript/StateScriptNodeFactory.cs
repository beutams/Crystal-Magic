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

public readonly struct StateScriptNodeBuildRequest
{
    public StateScriptNodeBuildRequest(StateScriptNodeData data, StateScriptRuntime runtime)
    {
        Data = data;
        Runtime = runtime;
    }

    public StateScriptNodeData Data { get; }
    public StateScriptRuntime Runtime { get; }
}

public sealed class StateScriptNodeRuntimeFactory
    : GeneratedFactory<Type, StateScriptNodeBuildRequest, StateScriptNode>
{
    public StateScriptNode CreateNode(StateScriptNodeData data, StateScriptRuntime runtime)
    {
        return data == null ? null : Create(data.GetType(), new StateScriptNodeBuildRequest(data, runtime));
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
