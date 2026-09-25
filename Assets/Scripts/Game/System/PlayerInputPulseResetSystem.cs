using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[WorldSystemFilter(
    WorldSystemFilterFlags.LocalSimulation |
    WorldSystemFilterFlags.ClientSimulation |
    WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup), OrderFirst = true)]
public partial struct PlayerInputPulseResetSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new PlayerInputPulseResetJob().ScheduleParallel();
    }
}

[BurstCompile]
public partial struct PlayerInputPulseResetJob : IJobEntity
{
    public void Execute(ref PlayerInputComponent input, ref DynamicBuffer<PlayerInputEventElement> events)
    {
        events.Clear();
        input.IsPrimaryHeld = input.ContinuousPrimaryHeld;
        input.IsInteractHeld = 0;
        input.IsSkillHeld = 0;
        input.IsUsePropHeld = 0;
        input.PropIndex = -1;
    }
}
