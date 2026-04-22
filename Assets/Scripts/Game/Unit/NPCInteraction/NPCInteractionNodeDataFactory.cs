using System;
using CrystalMagic.Game.Data;

public sealed class NPCInteractionNodeDataFactory : GeneratedFactory<string, NPCInteractionNodeData>
{
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

        node.Type = typeName;
        if (assignGuid)
        {
            node.Guid = Guid.NewGuid().ToString("N");
        }

        return node;
    }
}
