using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateAfter(typeof(UnitControlSystem))]
[UpdateAfter(typeof(UnitSkillSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class UnitCastAvailabilitySystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (GameGateComponent.Instance.IsSimulationLocked)
            return;

        UpdateUnitAvailability();
        UpdatePlayerAvailability();
    }

    private void UpdateUnitAvailability()
    {
        foreach (var (unitSkillRef, availabilityRef, intentRef, perceptionRef, entity) in
                 SystemAPI.Query<RefRW<UnitSkillComponent>, RefRW<UnitCastAvailabilityComponent>, RefRO<UnitIntentComponent>, RefRO<UnitPerceptionComponent>>()
                     .WithNone<PlayerTag>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            UnitSkillComponent unitSkill = unitSkillRef.ValueRW;
            UnitCastAvailabilityComponent availability = availabilityRef.ValueRW;
            RefreshUnitAvailability(entity, ref unitSkill, intentRef.ValueRO, perceptionRef.ValueRO, ref availability);
            unitSkillRef.ValueRW = unitSkill;
            availabilityRef.ValueRW = availability;
        }
    }

    private void UpdatePlayerAvailability()
    {
        foreach (var (availabilityRef, entity) in
                 SystemAPI.Query<RefRW<UnitCastAvailabilityComponent>>()
                     .WithAll<PlayerTag>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            UnitCastAvailabilityComponent availability = availabilityRef.ValueRW;
            RefreshPlayerAvailability(entity, ref availability);
            availabilityRef.ValueRW = availability;
        }
    }

    private void RefreshUnitAvailability(
        Entity entity,
        ref UnitSkillComponent unitSkill,
        in UnitIntentComponent intent,
        in UnitPerceptionComponent perception,
        ref UnitCastAvailabilityComponent availability)
    {
        bool canCast = !IsCastLocked(entity);
        bool hasTarget = perception.HasTarget;
        availability.CastableSkillIndices.Clear();

        for (int i = 0; i < unitSkill.Skills.Length; i++)
        {
            UnitSkillEntry entry = unitSkill.Skills[i];
            bool isAvailable = EvaluateUnitSkillAvailability(entity, entry, hasTarget, perception.TargetDistance);
            entry.IsAvailable = isAvailable ? (byte)1 : (byte)0;
            unitSkill.Skills[i] = entry;

            if (!isAvailable)
                continue;

            if (!MatchesUnitSkillRequest(intent, entry))
                continue;

            if (availability.CastableSkillIndices.Length < availability.CastableSkillIndices.Capacity)
                availability.CastableSkillIndices.Add(i);
        }

        availability.CanStartCast = canCast && availability.CastableSkillIndices.Length > 0 ? (byte)1 : (byte)0;
    }

    private void RefreshPlayerAvailability(Entity entity, ref UnitCastAvailabilityComponent availability)
    {
        bool canCast = !IsCastLocked(entity);
        bool hasAvailableSkill = false;
        availability.CastableSkillIndices.Clear();

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
        SkillChainSlotData slotData = SkillChainResolver.GetFirstSlot(skillConfig, runtimeSkillData);
        if (slotData != null)
        {
            SkillData baseSkill = SkillChainResolver.GetSkillData(slotData);
            if (SkillAnalysisUtility.TryAnalyzeSkill(EntityManager, entity, baseSkill, slotData.SkillAdditionId, out ResolvedSkillData resolvedSkill))
                hasAvailableSkill = HasEnoughMana(entity, resolvedSkill.MpCost);
        }

        if (hasAvailableSkill)
            availability.CastableSkillIndices.Add(0);

        availability.CanStartCast = canCast && hasAvailableSkill ? (byte)1 : (byte)0;
    }

    private bool EvaluateUnitSkillAvailability(Entity entity, in UnitSkillEntry entry, bool hasTarget, float targetDistance)
    {
        if (!hasTarget || entry.SkillId < 0 || entry.CooldownRemaining > 0f)
            return false;

        if (targetDistance < entry.MinDistance)
            return false;

        if (entry.MaxDistance > 0f && targetDistance > entry.MaxDistance)
            return false;

        if (!SkillAnalysisUtility.TryAnalyzeSkill(EntityManager, entity, entry.SkillId, -1, out ResolvedSkillData resolvedSkill))
            return false;

        return HasEnoughMana(entity, resolvedSkill.MpCost);
    }

    private bool HasEnoughMana(Entity entity, int mpCost)
    {
        if (!EntityManager.HasComponent<UnitManaComponent>(entity))
            return mpCost <= 0;

        UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(entity);
        return mana.CurrentMana >= mpCost;
    }

    private bool IsCastLocked(Entity entity)
    {
        if (!EntityManager.HasComponent<UnitControlRuntimeComponent>(entity))
            return false;

        UnitControlRuntimeComponent control = EntityManager.GetComponentData<UnitControlRuntimeComponent>(entity);
        return control.HasControl != 0 && control.LockCast != 0;
    }

    private static bool MatchesUnitSkillRequest(in UnitIntentComponent intent, in UnitSkillEntry entry)
    {
        return intent.SkillRequestMode switch
        {
            UnitSkillSelectionMode.RandomAll => true,
            UnitSkillSelectionMode.RandomTagMask => intent.RequestedTagMask != 0 && (entry.TagMask & intent.RequestedTagMask) != 0,
            UnitSkillSelectionMode.ExactSkillId => entry.SkillId == intent.RequestedSkillId,
            _ => false,
        };
    }
}
