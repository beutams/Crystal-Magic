using CrystalMagic.Game.Data.Effects;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class SpawnVfxEffect : Effect
    {
        public new SpawnVfxEffectData Data { get; }

        public SpawnVfxEffect(SpawnVfxEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || string.IsNullOrWhiteSpace(Data.VfxPrefabName) ||
                !SpriteEffectSpawnUtility.TryGetReleasePosition(context, out float3 position))
            {
                return;
            }

            quaternion rotation = SpriteEffectSpawnUtility.GetFacingRotation(context, Data.AlignToCasterForward);
            position += math.rotate(rotation, new float3(Data.SpawnOffset.x, Data.SpawnOffset.y, Data.SpawnOffset.z));
            SpriteEffectSpawnUtility.TrySpawn(
                context.EntityManager,
                Data.VfxPrefabName,
                position,
                rotation,
                Data.Scale,
                Data.Duration,
                out _);
        }
    }
}
