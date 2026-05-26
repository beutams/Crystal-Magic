using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class RandomAreaPointEffect : Effect
    {
        public new RandomAreaPointEffectData Data { get; }

        public RandomAreaPointEffect(RandomAreaPointEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || Data.PointCount <= 0)
                return;

            EntityManager entityManager = context.EntityManager;
            if (!TryGetCenter(context, entityManager, out float3 center))
                return;

            Vector3 offset = Data.CenterOffset;
            center += new float3(offset.x, offset.y, offset.z);

            float maxRadius = math.max(0f, Data.Radius);
            float minRadius = math.clamp(Data.MinRadius, 0f, maxRadius);
            for (int i = 0; i < Data.PointCount; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle;
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = Vector2.right;

                float radius = Mathf.Sqrt(UnityEngine.Random.Range(minRadius * minRadius, maxRadius * maxRadius));
                Vector3 point = new(center.x + direction.x * radius, center.y + direction.y * radius, center.z);

                SkillContent pointContext = context.Clone();
                pointContext.EntityManager = entityManager;
                pointContext.HasPosition = true;
                pointContext.Position = point;
                SkillExecutor.ExecuteEffects(Data.OnEachPointEffects, pointContext);
            }
        }

        private static bool TryGetCenter(SkillContent context, EntityManager entityManager, out float3 center)
        {
            if (context.HasPosition)
            {
                Vector3 position = context.Position;
                center = new float3(position.x, position.y, position.z);
                return true;
            }

            if (context.HasOriginEntity &&
                entityManager.Exists(context.OriginEntity) &&
                entityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                center = entityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
                return true;
            }

            center = float3.zero;
            return false;
        }
    }
}
