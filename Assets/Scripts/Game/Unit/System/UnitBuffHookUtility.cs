using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using Unity.Entities;

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
            !UnitBuffUtility.TryGetRuntimeComponent(entityManager, targetEntity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return;
        }

        PendingEffectExecutionQueueComponent effectExecutionQueue = PendingEffectExecutionQueueUtility.GetOrCreate(entityManager);
        BuffHookContext context = new()
        {
            EntityManager = entityManager,
            TargetEntity = targetEntity,
            EffectExecutionQueue = effectExecutionQueue,
            HookType = hookType,
            TriggerSource = triggerSource,
            HasOriginEntity = hasOriginEntity,
            OriginEntity = hasOriginEntity ? originEntity : Entity.Null,
            SourceSkillId = sourceSkillId,
            HasOtherEntity = hasOtherEntity,
            OtherEntity = hasOtherEntity ? otherEntity : Entity.Null,
            HasPosition = hasPosition,
            Position = position,
            TriggerValue = triggerValue,
        };

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (!buffs[i].OnHook(context))
                buffs.RemoveAt(i);
        }
    }
}
