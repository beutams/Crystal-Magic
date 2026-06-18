using System;
using CrystalMagic.Game.Data;

public sealed class NPCInteractionNodeDataFactory : GeneratedFactory<string, NPCInteractionNodeData>
{
    public static NPCInteractionNodeDataFactory Default { get; } = CreateDefaultFactory();

    public NPCInteractionNodeDataFactory()
        : base(StringComparer.Ordinal)
    {
    }

    public NPCInteractionNodeData CreateNode(string typeName, bool assignGuid = true)
    {
        NPCInteractionNodeData node = Create(typeName);
        if (node == null)
        {
            return null;
        }

        if (assignGuid)
        {
            node.Guid = Guid.NewGuid().ToString("N");
        }

        return node;
    }

    private static NPCInteractionNodeDataFactory CreateDefaultFactory()
    {
        NPCInteractionNodeDataFactory factory = new();
        NPCInteractionNodeDataRegistry.RegisterAll(factory);
        return factory;
    }
}
