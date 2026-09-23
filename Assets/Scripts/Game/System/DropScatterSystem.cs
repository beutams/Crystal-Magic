using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
public partial struct DropScatterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DropScatterComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new DropScatterJob
        {
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithNone(typeof(DestroyEntityFlag))]
    private partial struct DropScatterJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(
            ref DropScatterComponent scatter,
            ref LocalTransform transform,
            ref UnitInteractableComponent interactable)
        {
            if (scatter.IsLanded != 0)
                return;

            scatter.ElapsedSeconds = math.max(0f, scatter.ElapsedSeconds + DeltaTime);
            bool landed = scatter.DurationSeconds <= 0.0001f ||
                          scatter.ElapsedSeconds >= scatter.DurationSeconds;
            if (landed)
            {
                scatter.ElapsedSeconds = math.max(0f, scatter.DurationSeconds);
                scatter.IsLanded = 1;
                transform.Position = scatter.TargetPosition;
                interactable.IsEnabled = 1;
                interactable.NetworkDirty = 1;
                return;
            }

            transform.Position = DropScatterUtility.Evaluate(
                scatter.StartPosition,
                scatter.TargetPosition,
                scatter.ArcHeight,
                scatter.ElapsedSeconds,
                scatter.DurationSeconds);
        }
    }
}
