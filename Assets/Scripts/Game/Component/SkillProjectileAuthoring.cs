using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public class SkillProjectileAuthoring : MonoBehaviour
{
    private sealed class SkillProjectileBaker : Baker<SkillProjectileAuthoring>
    {
        public override void Bake(SkillProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SkillProjectileComponent
            {
                Direction = float3.zero,
                Speed = 0f,
                MaxRange = 0f,
                TraveledDistance = 0f,
                HitRadius = 0f,
                CanPierce = 0,
                TriggerDestroyEffectsOnMaxRange = 0,
            });
        }
    }
}

public struct SkillProjectileComponent : IComponentData
{
    public float3 Direction;
    public float Speed;
    public float MaxRange;
    public float TraveledDistance;
    public float HitRadius;
    public byte CanPierce;
    public byte TriggerDestroyEffectsOnMaxRange;
}
