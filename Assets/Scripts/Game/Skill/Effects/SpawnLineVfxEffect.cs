using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Unit;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class SpawnLineVfxEffect : Effect
    {
        public new SpawnLineVfxEffectData Data { get; }

        public SpawnLineVfxEffect(SpawnLineVfxEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null ||
                context == null ||
                string.IsNullOrWhiteSpace(Data.VfxPrefabName) ||
                !context.HasPosition)
            {
                return;
            }

            EntityManager entityManager = context.EntityManager;
            if (!TryGetOriginPosition(context, entityManager, out float3 originPosition))
            {
                return;
            }

            float3 targetPosition = new(context.Position.x, context.Position.y, context.Position.z);
            float2 direction = targetPosition.xy - originPosition.xy;
            if (math.lengthsq(direction) <= 0.0001f)
                return;

            direction = math.normalize(direction);
            float3 firstPosition = originPosition +
                                   new float3(direction.x, direction.y, 0f) * Data.OriginOffsetDistance;
            float spacing = math.max(0.01f, Data.SegmentSpacing);
            int segmentCount = math.max(1, (int)math.ceil(math.max(0f, Data.Length) / spacing));
            quaternion rotation = Data.AlignToLineDirection
                ? UnitFacingUtility.CreateRotation(direction)
                : quaternion.identity;

            if (NetworkPresentationEventUtility.TryEnqueueLineVfx(
                    entityManager,
                    Data.VfxPrefabName,
                    firstPosition,
                    direction,
                    rotation,
                    Data.Length,
                    spacing,
                    Data.Scale,
                    Data.Duration,
                    Data.AlignToLineDirection))
            {
                return;
            }

            for (int index = 0; index < segmentCount; index++)
            {
                float3 position = firstPosition + new float3(direction.x, direction.y, 0f) * (spacing * index);
                SpriteEffectSpawnUtility.TrySpawn(
                    entityManager,
                    Data.VfxPrefabName,
                    position,
                    rotation,
                    Data.Scale,
                    Data.Duration,
                    preservePrefabRotation: !Data.AlignToLineDirection,
                    out _);
            }
        }

        private static bool TryGetOriginPosition(SkillContent context, EntityManager entityManager, out float3 position)
        {
            if (context.HasOriginPositionSnapshot)
            {
                position = new float3(
                    context.OriginPositionSnapshot.x,
                    context.OriginPositionSnapshot.y,
                    context.OriginPositionSnapshot.z);
                return true;
            }

            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                entityManager.Exists(context.OriginEntity) &&
                entityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                position = entityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
                return true;
            }

            position = float3.zero;
            return false;
        }
    }
}
