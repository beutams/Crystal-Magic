using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

public sealed class NPCInteractionNodeFactory
    : GeneratedFactory<Type, NPCInteractionNodeData, NPCInteractionNodeRunner>
{
    public NPCInteractionNodeFactory()
        : base(TypeComparer.Instance)
    {
    }

    public NPCInteractionNodeRunner Create(NPCInteractionNodeData node)
    {
        return node == null ? null : Create(node.GetType(), node);
    }

    private sealed class TypeComparer : IEqualityComparer<Type>
    {
        public static readonly TypeComparer Instance = new();

        public bool Equals(Type x, Type y) => x == y;

        public int GetHashCode(Type obj) => obj == null ? 0 : obj.GetHashCode();
    }
}
