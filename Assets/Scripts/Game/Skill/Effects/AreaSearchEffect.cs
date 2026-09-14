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
        private readonly List<UnitQueryHit> _hits = new();

        public new AreaSearchEffectData Data { get; }

        public AreaSearchEffect(AreaSearchEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null)
                return;

            EntityManager entityManager = GetEntityManager();
            context.EntityManager = entityManager;
            if (!TryGetSearchCenter(context, entityManager, out float3 center))
                return;

            Vector3 offset = Data.CenterOffset;
            center += new float3(offset.x, offset.y, offset.z);

            if (!UnitQueryUtility.TryGetTree(entityManager, UnitQueryTreeKind.Unit, out UnitQueryTree unitTree))
                return;
            unitTree.QueryCircle(center, Data.Radius, _hits);

            int nearestHitIndex = -1;
            float nearestDistanceSq = float.MaxValue;
            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (!EffectConditionUtility.Pass(Data.TargetConditions, context, hit.Entity))
                    continue;

                if (!Data.OnlyNearestTarget)
                {
                    ExecuteTargetEffects(entityManager, context, hit);
                    continue;
                }

                float distanceSq = math.lengthsq(hit.Position.xy - center.xy);
                if (distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                nearestHitIndex = i;
            }

            if (nearestHitIndex >= 0)
                ExecuteTargetEffects(entityManager, context, _hits[nearestHitIndex]);
        }

        private void ExecuteTargetEffects(EntityManager entityManager, SkillContent context, UnitQueryHit hit)
        {
            Vector3 targetPosition = new(hit.Position.x, hit.Position.y, hit.Position.z);
            SkillContent targetContext = context.CloneForTarget(hit.Entity, targetPosition);
            targetContext.EntityManager = entityManager;
            SkillExecutor.ExecuteEffects(Data.OnAfterSearch, targetContext);
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

        private static EntityManager GetEntityManager()
        {
            return World.DefaultGameObjectInjectionWorld.EntityManager;
        }
    }
}
