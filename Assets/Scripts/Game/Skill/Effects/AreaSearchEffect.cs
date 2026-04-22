using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 范围搜索效果，逻辑由目标查询系统接入
    /// </summary>
    public sealed class AreaSearchEffect : Effect
    {
        private static ComparatorFactory _comparatorFactory;
        private readonly List<UnitQueryHit> _hits = new();

        public new AreaSearchEffectData Data { get; }

        public AreaSearchEffect(AreaSearchEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            UnitQuerySystem querySystem = UnitQuerySystem.Default;
            if (querySystem == null || Data == null)
                return;

            EntityManager entityManager = querySystem.QueryEntityManager;
            if (!TryGetSearchCenter(context, entityManager, out float3 center))
                return;

            Vector3 offset = Data.CenterOffset;
            center += new float3(offset.x, offset.y, offset.z);

            querySystem.QueryCircle(center, Data.Radius, _hits);
            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (!PassTargetConditions(Data.TargetConditions, hit.Entity, entityManager))
                    continue;

                Vector3 targetPosition = new(hit.Position.x, hit.Position.y, hit.Position.z);
                SkillContent targetContext = context.CloneForTarget(hit.Entity, targetPosition);
                targetContext.EntityManager = entityManager;
                SkillExecutor.ExecuteEffects(Data.OnAfterSearch, targetContext);
            }
        }

        private static bool TryGetSearchCenter(SkillContent context, EntityManager entityManager, out float3 center)
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

        private static bool PassTargetConditions(List<ConditionConfig> conditions, Entity target, EntityManager entityManager)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            Comparator comparator = GetComparatorFactory().BuildComparator(conditions, target, entityManager);
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
