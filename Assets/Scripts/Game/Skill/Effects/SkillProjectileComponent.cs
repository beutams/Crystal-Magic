using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill.Effects
{
    public struct SkillProjectileComponent : IComponentData
    {
        public float3 Direction;
        public float Speed;
        public float MaxRange;
        public float TraveledDistance;
        public float HitRadius;
        public int RegistryId;
        public byte CanPierce;
        public byte TriggerDestroyEffectsOnMaxRange;
    }
}
