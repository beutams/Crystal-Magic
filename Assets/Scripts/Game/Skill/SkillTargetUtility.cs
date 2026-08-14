using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill
{
    public static class SkillTargetUtility
    {
        public static bool TryGetTargetPosition(EntityManager entityManager, Entity entity, out float2 targetPosition)
        {
            targetPosition = float2.zero;
            if (entity == Entity.Null || !entityManager.Exists(entity))
                return false;

            if (entityManager.HasComponent<UnitPerceptionComponent>(entity))
            {
                UnitPerceptionComponent perception = entityManager.GetComponentData<UnitPerceptionComponent>(entity);
                if (perception.HasTarget)
                {
                    targetPosition = perception.TargetPosition;
                    return true;
                }
            }

            if (entityManager.HasComponent<UnitIntentComponent>(entity))
            {
                targetPosition = entityManager.GetComponentData<UnitIntentComponent>(entity).CastTargetPosition;
                return true;
            }

            return false;
        }
    }
}
