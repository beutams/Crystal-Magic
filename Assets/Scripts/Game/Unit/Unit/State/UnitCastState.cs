using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Mathematics;
using UnityEngine;

[FactoryKey("UnitCastState")]
public class UnitCastState : AUnitState
{
    public override void OnEnter()
    {
        if (!EntityManager.HasComponent<UnitCastComponent>(Entity))
            return;

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        if (cast.HasPreparedCast || cast.IsCasting)
        {
            if (SkillTargetUtility.TryGetTargetPosition(EntityManager, Entity, out float2 preparedTargetPosition))
                UpdateFacing(preparedTargetPosition);

            return;
        }

        if (!EntityManager.HasComponent<UnitSkillComponent>(Entity) ||
            !EntityManager.HasComponent<UnitCastAvailabilityComponent>(Entity))
        {
            return;
        }

        UnitSkillComponent unitSkill = EntityManager.GetComponentData<UnitSkillComponent>(Entity);
        UnitCastAvailabilityComponent availability = EntityManager.GetComponentData<UnitCastAvailabilityComponent>(Entity);

        int selectedIndex = SelectSkillIndex(unitSkill, availability);
        if (selectedIndex < 0)
        {
            SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
            unitSkill.ClearPending();
            EntityManager.SetComponentData(Entity, cast);
            EntityManager.SetComponentData(Entity, unitSkill);
            return;
        }

        SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
        unitSkill.HasPendingCast = true;
        unitSkill.PendingSkillIndex = selectedIndex;
        EntityManager.SetComponentData(Entity, cast);
        EntityManager.SetComponentData(Entity, unitSkill);
        if (SkillTargetUtility.TryGetTargetPosition(EntityManager, Entity, out float2 targetPosition))
            UpdateFacing(targetPosition);
    }

    public override void OnUpdate(float deltaTime)
    {
        if (SkillTargetUtility.TryGetTargetPosition(EntityManager, Entity, out float2 targetPosition))
            UpdateFacing(targetPosition);
    }

    public override void OnExit()
    {
        if (!EntityManager.HasComponent<UnitCastComponent>(Entity))
            return;

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
        EntityManager.SetComponentData(Entity, cast);

        if (EntityManager.HasComponent<UnitSkillComponent>(Entity))
        {
            UnitSkillComponent unitSkill = EntityManager.GetComponentData<UnitSkillComponent>(Entity);
            unitSkill.ClearPending();
            EntityManager.SetComponentData(Entity, unitSkill);
        }
    }

    private void UpdateFacing(float2 targetPosition)
    {
        if (!EntityManager.HasComponent<Unity.Transforms.LocalTransform>(Entity))
            return;

        float2 selfPosition = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(Entity).Position.xy;
        float2 direction = targetPosition - selfPosition;
        if (math.lengthsq(direction) <= 0.0001f)
            return;

        UnitFacingUtility.SetFacing(EntityManager, Entity, direction);
    }

    private int SelectSkillIndex(UnitSkillComponent unitSkill, UnitCastAvailabilityComponent availability)
    {
        if (availability.CastableSkillIndices.Length <= 0)
            return -1;

        if (availability.CastableSkillIndices.Length == 1)
            return availability.CastableSkillIndices[0];

        int totalWeight = 0;
        for (int i = 0; i < availability.CastableSkillIndices.Length; i++)
        {
            int skillIndex = availability.CastableSkillIndices[i];
            if (skillIndex < 0 || skillIndex >= unitSkill.Skills.Length)
                continue;

            UnitSkillEntry entry = unitSkill.Skills[skillIndex];
            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (totalWeight <= 0)
            return -1;

        int random = UnityEngine.Random.Range(0, totalWeight);
        int accum = 0;
        for (int i = 0; i < availability.CastableSkillIndices.Length; i++)
        {
            int skillIndex = availability.CastableSkillIndices[i];
            if (skillIndex < 0 || skillIndex >= unitSkill.Skills.Length)
                continue;

            UnitSkillEntry entry = unitSkill.Skills[skillIndex];
            accum += Mathf.Max(1, entry.Weight);
            if (random < accum)
                return skillIndex;
        }

        return -1;
    }
}
