using Unity.Entities;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientTransformInterpolationSystem))]
public partial struct ClientCooldownPresentationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ClientPresentationClockComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        ClientPresentationClockComponent clock = SystemAPI.GetSingleton<ClientPresentationClockComponent>();
        double realtime = Time.realtimeSinceStartupAsDouble;
        foreach (RefRW<ClientCooldownPresentationComponent> presentationRef in
                 SystemAPI.Query<RefRW<ClientCooldownPresentationComponent>>())
        {
            ClientCooldownPresentationComponent presentation = presentationRef.ValueRO;
            presentation.PropCooldownRemaining = ClientPresentationTimeUtility.GetRemainingSeconds(
                clock,
                presentation.PropCooldownEndFrame,
                realtime);
            presentationRef.ValueRW = presentation;
        }
    }
}
