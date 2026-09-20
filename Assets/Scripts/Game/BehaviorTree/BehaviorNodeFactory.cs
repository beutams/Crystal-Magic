using System;
using CrystalMagic.Game.Data;

public sealed class BehaviorNodeDataFactory : GeneratedFactory<string, BehaviorNodeData>
{
    public BehaviorNodeDataFactory()
        : base(StringComparer.Ordinal)
    {
    }

    public BehaviorNodeData CreateNode(string typeName, bool assignGuid = true)
    {
        BehaviorNodeData node = Create(typeName);
        if (node == null)
            return null;

        node.Type = typeName;
        if (assignGuid)
            node.Guid = Guid.NewGuid().ToString("N");

        node.ChildGuids ??= new System.Collections.Generic.List<string>();
        return node;
    }
}
