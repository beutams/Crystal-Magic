using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class ChainSearchEffect : Effect
    {
        private static ComparatorFactory _comparatorFactory;
        private readonly List<UnitQueryHit> _hits = new();
        private readonly HashSet<Entity> _visitedEntities = new();

        public new ChainSearchEffectData Data { get; }

        public ChainSearchEffect(ChainSearchEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null ||
                context == null ||
                Data.MaxJumps <= 0)
            {
                return;
            }

            EntityManager entityManager = context.EntityManager;
            if (!TryGetSearchCenter(context, entityManager, out float3 currentCenter))
                return;

            _visitedEntities.Clear();
            if (context.HasTargetEntity)
                _visitedEntities.Add(context.TargetEntity);

            for (int jumpIndex = 0; jumpIndex < Data.MaxJumps; jumpIndex++)
            {
                if (!TryGetNextTarget(context, entityManager, currentCenter, out UnitQueryHit nextHit))
                    break;

                _visitedEntities.Add(nextHit.Entity);
                currentCenter = nextHit.Position;

                Vector3 targetPosition = new(nextHit.Position.x, nextHit.Position.y, nextHit.Position.z);
                SkillContent targetContext = context.CloneForTarget(nextHit.Entity, targetPosition);
                targetContext.EntityManager = entityManager;
                SkillExecutor.ExecuteEffects(Data.OnAfterSearch, targetContext);
            }
        }

        private bool TryGetNextTarget(
            SkillContent context,
            EntityManager entityManager,
            float3 center,
            out UnitQueryHit nextHit)
        {
            nextHit = default;
            if (!UnitQueryUtility.TryQueryCircle(entityManager, center, Data.Radius, _hits))
                return false;

            bool found = false;
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (_visitedEntities.Contains(hit.Entity))
                    continue;

                if (!PassTargetConditions(
                        Data.TargetConditions,
                        hit.Entity,
                        entityManager,
                        context.OriginEntity,
                        context.HasOriginEntity))
                {
                    continue;
                }

                float distanceSq = math.lengthsq(hit.Position.xy - center.xy);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                nextHit = hit;
                found = true;
            }

            return found;
        }

        private static bool TryGetSearchCenter(SkillContent context, EntityManager entityManager, out float3 center)
        {
            if (context.HasTargetEntity &&
                entityManager.Exists(context.TargetEntity) &&
                entityManager.HasComponent<LocalTransform>(context.TargetEntity))
            {
                center = entityManager.GetComponentData<LocalTransform>(context.TargetEntity).Position;
                return true;
            }

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
            StateMachineRegistry.RegisterAll(new StateMachineFactory(), _comparatorFactory);
            return _comparatorFactory;
        }
    }
}
