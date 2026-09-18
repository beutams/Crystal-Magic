using Unity.Entities;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientTransformInterpolationSystem))]
public partial struct ClientControlPresentationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ClientPresentationClockComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        ClientPresentationClockComponent clock = SystemAPI.GetSingleton<ClientPresentationClockComponent>();
        double realtime = Time.realtimeSinceStartupAsDouble;
        foreach (RefRW<ClientControlPresentationComponent> presentationRef in
                 SystemAPI.Query<RefRW<ClientControlPresentationComponent>>())
        {
            ClientControlPresentationComponent presentation = presentationRef.ValueRO;
            presentation.DisplayRemainingTime = ClientPresentationTimeUtility.GetRemainingSeconds(
                clock,
                presentation.EndFrame,
                realtime);
            if (presentation.DisplayRemainingTime == 0f)
            {
                presentation.Active = 0;
                presentation.DisplayType = UnitControlType.None;
            }

            presentationRef.ValueRW = presentation;
        }
    }
}
