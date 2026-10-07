using Server;
using Unity.Entities;

// Release, movement, collision and impact effects share one world update. The
// player tick queues casts before this group; impacts are drained after movement.
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(UnitPostProcessSystemGroup))]
public partial class SkillProjectileSimulationSystemGroup : ComponentSystemGroup
{
    protected override void OnUpdate()
    {
        if (World.GetExistingSystem<SkillProjectileSystem>() != SystemHandle.Null)
        {
            var players = World.GetExistingSystemManaged<BattlePlayerSimulationSystemGroup>();
            if (players?.RateManager is BattleFrameRateManager rate && rate.LastStepCount > 0)
            {
                // Physics changed player positions after the perception snapshot.
                SystemHandle query = World.GetExistingSystem<UnitQueryBuildSystem>();
                if (query != SystemHandle.Null)
                    query.Update(World.Unmanaged);
            }
            World.GetExistingSystemManaged<EffectExecutionSystem>()?.Update();
        }
        base.OnUpdate();
    }
}
