using Server;
using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(StateScriptSystem))]
[UpdateBefore(typeof(StateScriptManagedCommandSystem))]
public partial class ClientSkillVisualCaptureSystem : SystemBase
{
    private EntityQuery _localPlayerQuery;

    protected override void OnCreate()
    {
        ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(EntityManager);
        _localPlayerQuery = GetEntityQuery(
            ComponentType.ReadOnly<NetworkPlayerComponent>(),
            ComponentType.ReadOnly<StateScriptManagedCommandElement>());
    }

    protected override void OnUpdate()
    {
        if (_localPlayerQuery.IsEmptyIgnoreFilter ||
            !FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame))
        {
            return;
        }

        Dependency.Complete();
        using NativeArray<Entity> players = _localPlayerQuery.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < players.Length; index++)
        {
            ClientSkillVisualPredictionUtility.CaptureSkillRequests(
                EntityManager,
                players[index],
                frame.currentFrame);
        }
    }
}
