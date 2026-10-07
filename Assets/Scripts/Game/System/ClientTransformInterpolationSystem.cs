using System.Collections.Generic;
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
        foreach ((RefRO<ClientProjectilePresentationComponent> presentation,
                 RefRO<SkillProjectileComponent> projectile, RefRW<LocalTransform> transform) in
                 SystemAPI.Query<RefRO<ClientProjectilePresentationComponent>,
                     RefRO<SkillProjectileComponent>, RefRW<LocalTransform>>())
        {
            LocalTransform presented = transform.ValueRO;
            presentation.ValueRO.Advance(in projectile.ValueRO, ref presented, realtime, SystemAPI.Time.DeltaTime);
            transform.ValueRW = presented;
        }
        List<Entity> arrivedEffects = null;
        foreach ((RefRO<ClientTransformInterpolationComponent> interpolationRef,
                 RefRW<LocalTransform> transformRef, Entity entity) in
                 SystemAPI.Query<RefRO<ClientTransformInterpolationComponent>, RefRW<LocalTransform>>()
                     .WithNone<NetworkPlayerComponent, ClientProjectilePresentationComponent>().WithEntityAccess())
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
            if (progress >= 1f && interpolation.DestroyOnArrival != 0)
            {
                arrivedEffects ??= new List<Entity>();
                arrivedEffects.Add(entity);
            }
        }

        if (arrivedEffects == null)
            return;
        foreach (Entity entity in arrivedEffects)
        {
            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
