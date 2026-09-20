using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Unmanaged references to the two world query buffers.
/// </summary>
public struct UnitQuerySingleton : IComponentData
{
    public Entity UnitGridEntity;
    public Entity InteractableGridEntity;
    public float InverseCellSize;
}

/// <summary>
/// One spatial-query entry. Buffers are sorted by CellKey and then Entity.
/// </summary>
public struct UnitQueryEntry : IBufferElementData
{
    public long CellKey;
    public Entity Entity;
    public float3 Position;
}
