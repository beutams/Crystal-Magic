using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientTransformInterpolationSystem))]
public partial class ClientBuffPresentationSystem : SystemBase
{
    private EntityQuery _buffQuery;

    protected override void OnCreate()
    {
        RequireForUpdate<ClientPresentationClockComponent>();
        _buffQuery = GetEntityQuery(ComponentType.ReadWrite<ClientBuffPresentationElement>());
    }

    protected override void OnUpdate()
    {
        ClientPresentationClockComponent clock = SystemAPI.GetSingleton<ClientPresentationClockComponent>();
        double realtime = UnityEngine.Time.realtimeSinceStartupAsDouble;
        List<Entity> visualsToEnd = null;

        using NativeArray<Entity> entities = _buffQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            DynamicBuffer<ClientBuffPresentationElement> buffs =
                EntityManager.GetBuffer<ClientBuffPresentationElement>(entities[entityIndex]);
            for (int index = buffs.Length - 1; index >= 0; index--)
            {
                ClientBuffPresentationElement buff = buffs[index];
                buff.DisplayRemainingTime = ClientPresentationTimeUtility.GetRemainingSeconds(
                    clock,
                    buff.EndFrame,
                    realtime);
                if (buff.DisplayRemainingTime == 0f)
                {
                    if (buff.VisualEntity != Entity.Null)
                    {
                        visualsToEnd ??= new List<Entity>();
                        visualsToEnd.Add(buff.VisualEntity);
                    }

                    buffs.RemoveAt(index);
                    continue;
                }

                buffs[index] = buff;
            }
        }

        EndVisuals(visualsToEnd);
    }

    private void EndVisuals(List<Entity> visuals)
    {
        if (visuals == null)
            return;

        for (int index = 0; index < visuals.Count; index++)
        {
            Entity visual = visuals[index];
            if (!EntityManager.Exists(visual))
                continue;

            if (EntityManager.HasComponent<SpriteEffectAnimationComponent>(visual))
            {
                SpriteEffectAnimationSystem.RequestEnd(EntityManager, visual);
                continue;
            }

            if (!EntityManager.HasComponent<DestroyEntityFlag>(visual))
                EntityManager.AddComponent<DestroyEntityFlag>(visual);
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(visual, true);
        }
    }
}
