using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(ClientTransformInterpolationSystem))]
public partial struct ClientPlayerMovePresentationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ClientPlayerMovePresentationJob
        {
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(NetworkPlayerComponent))]
public partial struct ClientPlayerMovePresentationJob : IJobEntity
{
    private const float CorrectionHalfLife = 0.06f;
    private const float SnapDistanceSq = 36f;
    private const float SettledDistanceSq = 0.000001f;

    public float DeltaTime;

    private void Execute(
        in UnitMoveComponent move,
        ref ClientPlayerMovePresentationComponent presentation,
        ref LocalTransform transform)
    {
        if (move.HasPredictedPosition == 0)
            return;

        float3 targetPosition = move.PredictedPosition;
        if (presentation.Initialized == 0)
        {
            presentation.CurrentPosition = transform.Position;
            presentation.Initialized = 1;
        }

        if (presentation.ReconciliationPending != 0)
        {
            presentation.CorrectionOffset = presentation.CurrentPosition - targetPosition;
            presentation.ReconciliationPending = 0;
            if (math.lengthsq(presentation.CorrectionOffset) >= SnapDistanceSq)
                presentation.CorrectionOffset = float3.zero;
        }

        if (math.lengthsq(presentation.CorrectionOffset) > SettledDistanceSq)
        {
            float decay = math.exp2(-DeltaTime / CorrectionHalfLife);
            presentation.CorrectionOffset *= decay;
        }
        else
        {
            presentation.CorrectionOffset = float3.zero;
        }

        presentation.CurrentPosition = targetPosition + presentation.CorrectionOffset;
        transform.Position = presentation.CurrentPosition;
    }
}
