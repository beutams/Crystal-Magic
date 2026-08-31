using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class SpawnFollowVfxEffect : Effect
    {
        public new SpawnFollowVfxEffectData Data { get; }

        public SpawnFollowVfxEffect(SpawnFollowVfxEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || string.IsNullOrWhiteSpace(Data.VfxPrefabName) ||
                !SpriteEffectSpawnUtility.TryGetFollowTarget(
                    context,
                    Data.FollowTarget == SpawnVfxFollowTarget.TargetEntity,
                    out Entity target,
                    out LocalTransform targetTransform))
            {
                return;
            }

            quaternion rotation = SpriteEffectSpawnUtility.GetEntityFacingRotation(
                context.EntityManager,
                target,
                Data.AlignToTargetForward);
            float3 offset = new(Data.SpawnOffset.x, Data.SpawnOffset.y, Data.SpawnOffset.z);
            float3 position = targetTransform.Position + math.rotate(rotation, offset);
            if (!SpriteEffectSpawnUtility.TrySpawn(
                    context.EntityManager,
                    Data.VfxPrefabName,
                    position,
                    rotation,
                    Data.Scale,
                    Data.Duration,
                    out Entity effectEntity))
            {
                return;
            }

            SpriteEffectSpawnUtility.SetOrAddComponentData(
                context.EntityManager,
                effectEntity,
                new EffectVisualFollowComponent
                {
                    Target = target,
                    Offset = offset,
                    AlignRotation = Data.AlignToTargetForward ? (byte)1 : (byte)0,
                    EndWhenTargetMissing = 1,
                });
        }
    }
}
