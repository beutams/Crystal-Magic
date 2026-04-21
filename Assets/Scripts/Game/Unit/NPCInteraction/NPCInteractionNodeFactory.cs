using System;
using CrystalMagic.Game.Data;

public sealed class NPCInteractionNodeFactory
    : GeneratedFactory<Type, NPCInteractionNodeData, NPCInteractionNodeRunner>
{
    public void Register<TNode>(Func<TNode, NPCInteractionNodeRunner> factory)
        where TNode : NPCInteractionNodeData
    {
        Register(typeof(TNode), node => factory((TNode)node));
    }

    public NPCInteractionNodeRunner Create(NPCInteractionNodeData node)
    {
        return node == null ? null : Create(node.GetType(), node);
    }
}
