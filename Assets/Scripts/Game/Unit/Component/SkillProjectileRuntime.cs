using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public sealed class SkillProjectileSpawnRequest
{
    public FixedString128Bytes ProjectileName;
    public float3 StartPosition;
    public float3 Direction;
    public quaternion Rotation;
    public float Speed;
    public float MaxRange;
    public float HitRadius;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
    public SkillContent Context;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}

public static class SkillProjectileSpawnQueue
{
    private static readonly System.Collections.Generic.Queue<SkillProjectileSpawnRequest> s_requests = new();

    public static void Enqueue(SkillProjectileSpawnRequest request)
    {
        if (request != null)
            s_requests.Enqueue(request);
    }

    public static bool TryDequeue(out SkillProjectileSpawnRequest request)
    {
        if (s_requests.Count > 0)
        {
            request = s_requests.Dequeue();
            return true;
        }

        request = null;
        return false;
    }

    public static bool HasPendingRequests => s_requests.Count > 0;
}

public struct SkillProjectileHitEntityElement : IBufferElementData
{
    public Entity Value;
}

public sealed class SkillProjectilePayloadComponent : IComponentData
{
    public SkillContent Context;
    public EffectData[] OnCollisionEffects;
    public EffectData[] OnDestroyEffects;
}
