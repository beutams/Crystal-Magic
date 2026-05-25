using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class ConeSearchEffect : Effect
    {
        private static ComparatorFactory _comparatorFactory;
        private readonly List<UnitQueryHit> _hits = new();

        public new ConeSearchEffectData Data { get; }

        public ConeSearchEffect(ConeSearchEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null ||
                context == null ||
                !context.HasOriginEntity ||
                !context.HasPosition)
            {
                return;
            }

            EntityManager entityManager = context.EntityManager;
            if (!entityManager.Exists(context.OriginEntity) ||
                !entityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                return;
            }

            float3 originPosition = entityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
            float3 targetPosition = new float3(context.Position.x, context.Position.y, context.Position.z);
            float2 forward = targetPosition.xy - originPosition.xy;
            if (math.lengthsq(forward) <= 0.0001f)
                return;

            if (!UnitQueryUtility.TryQueryCone(entityManager, originPosition, forward, Data.Radius, Data.AngleDegrees, _hits))
                return;

            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (!PassTargetConditions(
                        Data.TargetConditions,
                        hit.Entity,
                        entityManager,
                        context.OriginEntity,
                        context.HasOriginEntity))
                {
                    continue;
                }

                Vector3 hitPosition = new(hit.Position.x, hit.Position.y, hit.Position.z);
                SkillContent targetContext = context.CloneForTarget(hit.Entity, hitPosition);
                targetContext.EntityManager = entityManager;
                SkillExecutor.ExecuteEffects(Data.OnAfterSearch, targetContext);
            }
        }

        private static bool PassTargetConditions(
            List<ConditionConfig> conditions,
            Entity target,
            EntityManager entityManager,
            Entity originEntity,
            bool hasOriginEntity)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            Comparator comparator = GetComparatorFactory().BuildComparator(
                conditions,
                target,
                entityManager,
                originEntity,
                hasOriginEntity);
            return comparator.GetResult();
        }

        private static ComparatorFactory GetComparatorFactory()
        {
            if (_comparatorFactory != null)
                return _comparatorFactory;

            _comparatorFactory = new ComparatorFactory();
            ComparatorRegistry.RegisterAll(_comparatorFactory);
            return _comparatorFactory;
        }
    }
}
