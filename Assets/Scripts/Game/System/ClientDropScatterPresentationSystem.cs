using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientTransformInterpolationSystem))]
public partial struct ClientDropScatterPresentationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DropScatterComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ClientDropScatterPresentationJob
        {
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithNone(typeof(DestroyEntityFlag))]
    private partial struct ClientDropScatterPresentationJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref DropScatterComponent scatter, ref LocalTransform transform)
        {
            if (scatter.IsLanded != 0)
            {
                transform.Position = scatter.TargetPosition;
                return;
            }

            scatter.ElapsedSeconds = math.max(0f, scatter.ElapsedSeconds + DeltaTime);
            if (scatter.DurationSeconds <= 0.0001f || scatter.ElapsedSeconds >= scatter.DurationSeconds)
            {
                scatter.ElapsedSeconds = math.max(0f, scatter.DurationSeconds);
                scatter.IsLanded = 1;
                transform.Position = scatter.TargetPosition;
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
