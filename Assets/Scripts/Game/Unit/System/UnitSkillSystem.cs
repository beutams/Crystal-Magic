using Unity.Entities;
using Unity.Mathematics;

[UpdateAfter(typeof(UnitStateMachineSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class UnitSkillSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (unitSkillRef, castRef, entity) in SystemAPI.Query<RefRW<UnitSkillComponent>, RefRW<UnitCastComponent>>().WithNone<PlayerTag>().WithEntityAccess())
        {
            UnitSkillComponent unitSkill = unitSkillRef.ValueRW;
            TickCooldowns(deltaTime, ref unitSkill);

            if (unitSkill.HasPendingCast && !castRef.ValueRO.IsCasting)
            {
                TryStartPendingUnitSkill(entity, ref unitSkill, ref castRef.ValueRW);
            }

            unitSkillRef.ValueRW = unitSkill;
        }
    }

    private void TickCooldowns(float deltaTime, ref UnitSkillComponent unitSkill)
    {
        for (int i = 0; i < unitSkill.Skills.Length; i++)
        {
            UnitSkillEntry entry = unitSkill.Skills[i];
            if (entry.CooldownRemaining <= 0f)
                continue;

            entry.CooldownRemaining = math.max(0f, entry.CooldownRemaining - deltaTime);
            unitSkill.Skills[i] = entry;
        }
    }

    private void TryStartPendingUnitSkill(Entity entity, ref UnitSkillComponent unitSkill, ref UnitCastComponent cast)
    {
        int skillIndex = unitSkill.PendingSkillIndex;
        if (skillIndex < 0 || skillIndex >= unitSkill.Skills.Length)
        {
            unitSkill.ClearPending();
            return;
        }

        UnitSkillEntry entry = unitSkill.Skills[skillIndex];
        Unity.Collections.FixedList64Bytes<int> skillIds = default;
        Unity.Collections.FixedList64Bytes<int> skillEffectIds = default;
        skillIds.Add(entry.SkillId);
        skillEffectIds.Add(entry.SkillEffectId);

        bool started = SkillExecutionUtility.TryBeginCast(
            EntityManager,
            entity,
            ref cast,
            skillIds,
            skillEffectIds,
            -1,
            unitSkill.HasLockedTarget,
            unitSkill.LockedTargetPosition);

        if (started)
        {
            entry.CooldownRemaining = math.max(0f, entry.CooldownSeconds);
            unitSkill.Skills[skillIndex] = entry;
        }

        unitSkill.ClearPending();
        unitSkill.ClearRequest();
    }
}
