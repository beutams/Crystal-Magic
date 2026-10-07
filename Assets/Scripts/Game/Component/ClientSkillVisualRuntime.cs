using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct ClientSkillVisualRuntimeComponent : IComponentData
{
    public uint RollbackFrame;
    public byte HasPendingRollback;
}

[InternalBufferCapacity(0)]
public struct ClientSkillVisualRequestElement : IBufferElementData
{
    public uint Frame;
    public int RequestOrdinal;
    public SkillReleaseRequest Request;
}

[InternalBufferCapacity(0)]
public struct ClientPredictedSkillVisualEventElement : IBufferElementData
{
    public SkillEffectIdentity Identity;
    public uint Frame;
    public int RequestOrdinal;
    public int EffectOrdinal;
    public int SkillId;
    public ClientPresentationEventType Type;
    public Entity Source;
    public Entity VisualEntity;
    public Entity AnchorEntity;
    public FixedString128Bytes AssetName;
    public float3 Position;
    public byte IsProjectile;
    public byte IsConfirmed;
}

public struct ClientPredictedSkillVisualComponent : IComponentData
{
    public SkillEffectIdentity Identity;
    public uint Frame;
    public int RequestOrdinal;
    public int EffectOrdinal;
    public int SegmentOrdinal;
}

public struct ClientPredictedProjectileComponent : IComponentData
{
    public SkillEffectIdentity Identity;
    public Entity Authority;
    public FixedString128Bytes VisualPrefab;
    public float3 VisualOffset;
    public float VisualScale;
    public uint PredictedEndFrame;
    public byte HasPredictedEnd;
}

public struct ClientPredictedProjectileLinkComponent : IComponentData
{
    public Entity Prediction;
}

// Ballistic presentation for remote projectiles without a local prediction payload.
public struct ClientProjectilePresentationComponent : IComponentData
{
    public float3 SnapshotPosition;
    public double SnapshotRealtime;
    public float PredictionLeadSeconds;
    public byte Initialized;

    public float3 GetTargetPosition(in SkillProjectileComponent projectile, double realtime)
    {
        float age = math.max(0f, (float)(realtime - SnapshotRealtime)) + PredictionLeadSeconds;
        float distance = projectile.Speed * age;
        if (projectile.MaxRange > 0f)
        {
            float remaining = math.max(0f, projectile.MaxRange - projectile.TraveledDistance);
            distance = math.clamp(distance, -remaining, remaining);
        }
        return SnapshotPosition + projectile.Direction * distance;
    }

    public void Advance(in SkillProjectileComponent projectile, ref LocalTransform transform,
        double realtime, float deltaTime)
    {
        if (Initialized == 0 || deltaTime <= 0f)
            return;

        float3 velocity = projectile.Direction * projectile.Speed;
        float3 previous = transform.Position;
        float3 next = previous + velocity * deltaTime;
        float3 target = GetTargetPosition(in projectile, realtime);
        next = math.lerp(next, target, 1f - math.exp(-18f * deltaTime));

        // Delayed snapshots may slow a flying visual, but must not send it backwards.
        // The authoritative despawn supplies the final impact position separately.
        float3 forward = math.normalizesafe(velocity);
        float backwards = math.min(0f, math.dot(next - previous, forward));
        next -= forward * backwards;
        if (projectile.MaxRange > 0f)
        {
            float remaining = math.max(0f, projectile.MaxRange - projectile.TraveledDistance);
            float excess = math.max(0f, math.dot(next - SnapshotPosition, forward) - remaining);
            next -= forward * excess;
        }
        transform.Position = next;
    }
}
