using Unity.Entities;

[UpdateAfter(typeof(UnitStateMachineSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class PlayerSkillSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // PlayerCastState now owns skill-chain selection and cast progression.
    }
}
