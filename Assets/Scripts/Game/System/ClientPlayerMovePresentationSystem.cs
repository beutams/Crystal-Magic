using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Server;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(ClientTransformInterpolationSystem))]
public partial struct ClientPlayerMovePresentationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float interpolation = 1f;
        if (FrameManagerUtility.TryGet(state.EntityManager, out ClientFrameManager frame) && frame.running)
            interpolation = math.saturate((float)(frame.clock.AccumulatedMilliseconds / frame.frameInterval));
        state.Dependency = new ClientPlayerMovePresentationJob
        {
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
            Interpolation = interpolation,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(NetworkPlayerComponent))]
public partial struct ClientPlayerMovePresentationJob : IJobEntity
{
    private const float CorrectionHalfLife = 0.06f;
    private const float SettledDistanceSq = 0.000001f;

    public float DeltaTime;
    public float Interpolation;

    private void Execute(
        in UnitMoveComponent move,
        ref ClientPlayerMovePresentationComponent presentation,
        ref LocalTransform transform)
    {
        if (move.HasPredictedPosition == 0)
            return;

        if (presentation.Initialized == 0)
        {
            presentation.CurrentPosition = transform.Position;
            presentation.Initialized = 1;
        }

        if (presentation.ReconciliationPending != 0)
        {
            presentation.CompleteReconciliation(move.PredictedPosition);
        }
        float3 targetPosition = presentation.HasPredictionSamples != 0
            ? math.lerp(presentation.PreviousPredictionPosition, move.PredictedPosition, Interpolation)
            : move.PredictedPosition;

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
