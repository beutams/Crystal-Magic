using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CrystalMagic.Game.Skill;

[Serializable]
public sealed class NetworkProjectileStateData : NetworkStateData
{
    public SkillEffectIdentity identity;
    public uint hitSequence;
    public byte ended;
    public float repeatHitIntervalSeconds;
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

        SkillProjectileComponent projectile = new()
        {
            Identity = identity,
            HitSequence = hitSequence,
            Ended = ended,
            RepeatHitIntervalSeconds = repeatHitIntervalSeconds,
            Direction = new float3(directionX, directionY, directionZ),
            Speed = speed,
            MaxRange = maxRange,
            TraveledDistance = traveledDistance,
            HitRadius = hitRadius,
            CanPierce = canPierce,
            TriggerDestroyEffectsOnMaxRange = triggerDestroyEffectsOnMaxRange,
            NetworkDirty = 0,
        };
        context.SetOrAdd(entity, projectile);

        context.ApplyProjectilePosition(entity, new float3(positionX, positionY, positionZ));
        // Remote projectiles have no local collision payload. Their follow VFX
        // still need the same rotation as the shared movement job.
        LocalTransform transform = context.EntityManager.GetComponentData<LocalTransform>(entity);
        transform.Rotation = UnitFacingUtility.CreateRotation(new float2(directionX, directionY));
        context.EntityManager.SetComponentData(entity, transform);
        if (context.IsClient)
            ClientProjectilePredictionUtility.ApplySnapshot(context, entity, identity, projectile,
                new float3(positionX, positionY, positionZ), hitSequence);
    }
}
