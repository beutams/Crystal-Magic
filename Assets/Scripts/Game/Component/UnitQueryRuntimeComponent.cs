using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// References the buffers that contain the world spatial quadtree.
/// </summary>
public struct UnitQuerySingleton : IComponentData
{
    public Entity TreeEntity;
}

/// <summary>
/// One point stored in the spatial tree.
/// </summary>
public struct UnitQueryEntry : IBufferElementData
{
    public Entity Entity;
    public float3 Position;
    public UnitFactionType Faction;
    public int UnitDataId;
    public byte IsDead;
}

/// <summary>
/// Scratch storage used while rebuilding the tree. It is never queried by gameplay systems.
/// </summary>
public struct UnitQueryScratchEntry : IBufferElementData
{
    public UnitQueryEntry Value;
}

/// <summary>
/// Array-based quadtree node. Children, when present, occupy four consecutive indices.
/// </summary>
public struct UnitQueryNode : IBufferElementData
{
    public float2 Min;
    public float2 Max;
    public int StartIndex;
    public int Count;
    public int FirstChildIndex;
    public byte Depth;

    public readonly bool IsLeaf => FirstChildIndex < 0;
}
