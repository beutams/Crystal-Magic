using Unity.Entities;
using Unity.Mathematics;

public struct UnitAoiSingleton : IComponentData
{
    public uint Version;
    public float MaxQueryRadius;
}

public struct UnitAoiStateComponent : IComponentData
{
    public uint Version;
    public float NearestPlayerDistanceSq;
}

public struct UnitAoiActiveTag : IComponentData, IEnableableComponent
{
}

public struct UnitAoiObserverEntry : IBufferElementData
{
    public Entity Entity;
    public float3 Position;
}

public static class UnitAoiRanges
{
    public const float AnimationEnterRadius = 16f;
    public const float AnimationExitRadius = 18f;
    public const float MaxQueryRadius = AnimationExitRadius;
}
