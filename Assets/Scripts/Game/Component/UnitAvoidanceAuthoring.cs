using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(UnitNavigationAuthoring))]
public sealed class UnitAvoidanceAuthoring : MonoBehaviour
{
    private sealed class UnitAvoidanceBaker : Baker<UnitAvoidanceAuthoring>
    {
        public override void Bake(UnitAvoidanceAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float neighborDistance = 4f;
            int maxNeighbors = 12;
            float timeHorizon = 0.8f;
            float radiusPadding = 0.05f;
            UnitAvoidanceModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitAvoidanceModuleData>(authoring);
            if (data != null)
            {
                neighborDistance = math.max(0f, data.NeighborDistance);
                maxNeighbors = math.max(0, data.MaxNeighbors);
                timeHorizon = math.max(0.01f, data.TimeHorizon);
                radiusPadding = math.max(0f, data.RadiusPadding);
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitAvoidanceComponent
            {
                NeighborDistance = neighborDistance,
                MaxNeighbors = maxNeighbors,
                TimeHorizon = timeHorizon,
                RadiusPadding = radiusPadding,
                ResolvedVelocity = float2.zero,
                HasResolvedVelocity = 0,
            });
        }
    }
}

public struct UnitAvoidanceComponent : IComponentData
{
    public float NeighborDistance;
    public int MaxNeighbors;
    public float TimeHorizon;
    public float RadiusPadding;
    public float2 ResolvedVelocity;
    public byte HasResolvedVelocity;
}
