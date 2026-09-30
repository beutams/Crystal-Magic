using Unity.Entities;
using Unity.Mathematics;

/// <summary>Opt-in solver telemetry. Only the navigation test World adds this data; no gameplay authoring.</summary>
public struct UnitAvoidanceDebugData : IComponentData
{
    public float2 Position;
    public float2 InputVelocity;
    public float2 PreferredVelocity;
    public float2 ResolvedVelocity;
    public float MaxSpeed;
    public float Radius;
    public float NeighborDistance;
    public float TimeHorizon;
    public int ConstraintCount;
    public int FirstFailedLine;
    public byte HasSample;
    public bool UsedFallback => HasSample != 0 && FirstFailedLine < ConstraintCount;
}

/// <summary>One actual solver line and its corresponding neighbor, sampled before movement/physics.</summary>
[InternalBufferCapacity(0)]
public struct UnitAvoidanceDebugConstraint : IBufferElementData
{
    public Entity Neighbor;
    public float2 NeighborPosition;
    public float2 NeighborVelocity;
    public float NeighborRadius;
    public float Distance;
    public float2 Point;
    public float2 Direction;

    // Solver feasibility: det(Direction, Point - velocity) <= 0.
    // Therefore the left normal points INTO the permitted half-plane.
    public float2 AllowedNormal => new(-Direction.y, Direction.x);
    public float SignedMargin(float2 velocity) => math.dot(AllowedNormal, velocity - Point);
}
