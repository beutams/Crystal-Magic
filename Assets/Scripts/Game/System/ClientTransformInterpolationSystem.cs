using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup), OrderFirst = true)]
public partial class ClientTransformInterpolationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        double realtime = UnityEngine.Time.realtimeSinceStartupAsDouble;
        foreach ((RefRO<ClientTransformInterpolationComponent> interpolationRef,
                  RefRW<LocalTransform> transformRef) in
                 SystemAPI.Query<RefRO<ClientTransformInterpolationComponent>, RefRW<LocalTransform>>())
        {
            ClientTransformInterpolationComponent interpolation = interpolationRef.ValueRO;
            if (interpolation.Initialized == 0)
                continue;

            float progress = interpolation.Duration <= 0f
                ? 1f
                : math.saturate((float)((realtime - interpolation.StartRealtime) / interpolation.Duration));
            LocalTransform transform = transformRef.ValueRO;
            transform.Position = math.lerp(interpolation.FromPosition, interpolation.TargetPosition, progress);
            transformRef.ValueRW = transform;
        }
    }
}
