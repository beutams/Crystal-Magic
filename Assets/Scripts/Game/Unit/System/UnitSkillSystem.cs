using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitControlSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class UnitSkillSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (RefRW<UnitSkillComponent> unitSkillRef in SystemAPI.Query<RefRW<UnitSkillComponent>>().WithNone<PlayerTag>())
        {
            UnitSkillComponent unitSkill = unitSkillRef.ValueRW;
            TickCooldowns(deltaTime, ref unitSkill);

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

}
