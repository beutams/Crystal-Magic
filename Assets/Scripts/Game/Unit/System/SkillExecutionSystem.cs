using Unity.Entities;

[UpdateAfter(typeof(PlayerSkillSystem))]
[UpdateAfter(typeof(UnitSkillSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class SkillExecutionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Cast states now own skill-chain progression and interruption decisions.
    }
}
