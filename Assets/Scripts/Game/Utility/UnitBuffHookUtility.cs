using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

public static class UnitBuffHookUtility
{
    public static void Dispatch(
        EntityManager entityManager,
        Entity targetEntity,
        SkillHookType hookType,
        SkillTriggerSource triggerSource,
        bool hasOriginEntity = false,
        Entity originEntity = default,
        int sourceSkillId = -1,
        bool hasOtherEntity = false,
        Entity otherEntity = default,
        bool hasPosition = false,
        UnityEngine.Vector3 position = default,
        float triggerValue = 0f)
    {
        if (targetEntity == Entity.Null ||
            !entityManager.Exists(targetEntity) ||
            !entityManager.HasBuffer<UnitBuffElement>(targetEntity) ||
            !entityManager.HasBuffer<UnitBuffHookRequestElement>(targetEntity))
        {
            return;
        }

        entityManager.GetBuffer<UnitBuffHookRequestElement>(targetEntity).Add(
            new UnitBuffHookRequestElement
            {
                HookType = hookType,
                TriggerSource = triggerSource,
                OriginEntity = hasOriginEntity ? originEntity : Entity.Null,
                OtherEntity = hasOtherEntity ? otherEntity : Entity.Null,
                SourceSkillId = sourceSkillId,
                Position = new float3(position.x, position.y, position.z),
                TriggerValue = triggerValue,
                HasOriginEntity = hasOriginEntity ? (byte)1 : (byte)0,
                HasOtherEntity = hasOtherEntity ? (byte)1 : (byte)0,
                HasPosition = hasPosition ? (byte)1 : (byte)0,
            });
    }
}
