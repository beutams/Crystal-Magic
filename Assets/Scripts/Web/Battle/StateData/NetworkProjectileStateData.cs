using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[Serializable]
public sealed class NetworkProjectileStateData : NetworkStateData
{
    public float directionX;
    public float directionY;
    public float directionZ;
    public float speed;
    public float maxRange;
    public float traveledDistance;
    public float hitRadius;
    public byte canPierce;
    public byte triggerDestroyEffectsOnMaxRange;
    public float positionX;
    public float positionY;
    public float positionZ;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        context.SetOrAdd(entity, new SkillProjectileComponent
        {
            Direction = new float3(directionX, directionY, directionZ),
            Speed = speed,
            MaxRange = maxRange,
            TraveledDistance = traveledDistance,
            HitRadius = hitRadius,
            CanPierce = canPierce,
            TriggerDestroyEffectsOnMaxRange = triggerDestroyEffectsOnMaxRange,
            NetworkDirty = 0,
        });

        if (context.EntityManager.HasComponent<LocalTransform>(entity))
        {
            LocalTransform transform = context.EntityManager.GetComponentData<LocalTransform>(entity);
            transform.Position = new float3(positionX, positionY, positionZ);
            context.EntityManager.SetComponentData(entity, transform);
        }
    }
}
