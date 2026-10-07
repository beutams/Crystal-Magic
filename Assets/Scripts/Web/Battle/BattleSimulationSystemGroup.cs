using Server;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;

// Ordinary game systems use the parent world's deltaTime once per update.
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class BattleSimulationSystemGroup : ComponentSystemGroup
{
    private FrameManager frame;
    private bool configured;

    protected override void OnUpdate()
    {
        if (frame == null) return;
        bool preparing = !frame.running;
        if (preparing)
            World.PushTime(new TimeData(World.Time.ElapsedTime, 0));
        try
        {
            base.OnUpdate();
        }
        finally
        {
            if (preparing)
                World.PopTime();
        }
        // Inputs have already been predicted; urgent authority changes must not
        // wait for the ordinary snapshot batching timer.
        frame.FlushNetwork(NetworkTimer.Instance.TimeNow, frame is ClientFrameManager);
    }

    public static void Bind(World world, FrameManager frame)
    {
        if (frame == null)
        {
            var existing = world.GetExistingSystemManaged<BattleSimulationSystemGroup>();
            if (existing != null)
            {
                existing.frame = null;
                existing.Enabled = false;
            }
            var existingPlayers = world.GetExistingSystemManaged<BattlePlayerSimulationSystemGroup>();
            if (existingPlayers != null) existingPlayers.RateManager = null;
            return;
        }
        var group = Prepare(world, frame is ServerFrameManager);
        if (group.frame == frame)
            return;
        group.frame = frame;
        group.Enabled = true;
        world.GetExistingSystemManaged<BattlePlayerSimulationSystemGroup>().RateManager = new BattleFrameRateManager(frame);
    }

    public static BattleSimulationSystemGroup Prepare(World world, bool isServer)
    {
        var group = world.GetOrCreateSystemManaged<BattleSimulationSystemGroup>();
        if (group.configured) return group;
        group.Enabled = false;
        group.EnableSystemSorting = false;
        var root = world.GetExistingSystemManaged<SimulationSystemGroup>();
        root.AddSystemToUpdateList(group);
        var players = world.GetOrCreateSystemManaged<BattlePlayerSimulationSystemGroup>();
        using EntityQuery scopeQuery = world.EntityManager.CreateEntityQuery(typeof(BattleSimulationScope));
        Entity scope = scopeQuery.IsEmptyIgnoreFilter
            ? world.EntityManager.CreateEntity(typeof(BattleSimulationScope)) : scopeQuery.GetSingletonEntity();
        world.EntityManager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.World });
        players.SetScope(scope);

        // Bind corrections before receiving the first authoritative snapshot.
        var prediction = world.GetExistingSystemManaged<ClientPlayerPredictionSystemGroup>();
        root.RemoveSystemFromUpdateList(prediction);
        var record = world.GetExistingSystemManaged<ClientPlayerStateRecordSystem>();
        if (record != null)
            prediction.RemoveSystemFromUpdateList(record);
        group.AddSystemToUpdateList(prediction);
        var receive = world.GetExistingSystemManaged<FrameReceiveSystem>();
        root.RemoveSystemFromUpdateList(receive);
        (isServer ? players : (ComponentSystemGroup)group).AddSystemToUpdateList(receive);
        // Preparation still applies scene snapshots in the world pass. During play,
        // corrections and replay must finish together inside the player tick.
        if (!isServer)
            players.AddSystemToUpdateList(receive);
        var input = world.GetExistingSystemManaged<ClientInputSystemGroup>();
        Move(root, group, input);
        var sendInput = world.GetExistingSystemManaged<ClientPlayerInputStateSendSystem>();
        if (sendInput != null)
            Move(input, players, sendInput);
        Move(root, group, world.GetExistingSystemManaged<UnitInitializationSystemGroup>());
        Move(root, group, world.GetExistingSystemManaged<UnitSimulationGateSystem>());
        Move(root, group, world.GetExistingSystemManaged<UnitDecisionSystemGroup>());
        Move(root, group, world.GetExistingSystemManaged<UnitExecutionSystemGroup>());
        if (!isServer)
        {
            // Predicted casts, shared collision, impact effects and confirmation are
            // one ordered presentation pass, including updates with no player tick.
            var execution = world.GetExistingSystemManaged<SkillProjectileSimulationSystemGroup>();
            var presentation = world.GetExistingSystemManaged<ClientNetworkPresentationSystemGroup>();
            if (presentation != null)
            {
                Move(execution, presentation, world.GetExistingSystemManaged<SkillProjectileCleanupSystem>());
                Move(execution, presentation, world.GetExistingSystemManaged<EffectExecutionSystem>());
            }
        }

        if (isServer)
        {
            AddUnmanaged<UnitBuffSystem>(world, players);
            AddUnmanaged<UnitModifierSystem>(world, players);
        }
        AddManaged<StateScriptSystem>(world, players);
        var capture = world.GetExistingSystemManaged<ClientSkillVisualCaptureSystem>();
        if (capture != null)
            Move(world.GetExistingSystemManaged<UnitDecisionSystemGroup>(), players, capture);
        AddManaged<StateScriptManagedCommandSystem>(world, players);
        AddUnmanaged<UnitControlSystem>(world, players);
        if (isServer)
        {
            var cooldown = world.GetExistingSystem<PlayerPropCooldownSystem>();
            world.GetExistingSystemManaged<UnitExecutionSystemGroup>().RemoveSystemFromUpdateList(cooldown);
            players.AddSystemToUpdateList(cooldown);
        }
        AddUnmanaged<UnitMoveSystem>(world, players);
        var physics = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
        if (physics != null)
        {
            root.RemoveSystemFromUpdateList(physics);
            // Both authority and local prediction simulate collisions exactly once per
            // player tick. Presentation frames must never advance the physics state.
            physics.RateManager = null;
            players.AddSystemToUpdateList(physics);
        }
        if (isServer)
        {
            AddManaged<ServerNetworkEntitySpawnCollectSystem>(world, players);
            AddManaged<ServerNetworkStateCollectSystem>(world, players);
        }
        if (record != null)
            players.AddSystemToUpdateList(record);
        var post = world.GetExistingSystemManaged<UnitPostProcessSystemGroup>();
        var resetInput = world.GetExistingSystem<PlayerInputPulseResetSystem>();
        post.RemoveSystemFromUpdateList(resetInput);
        players.AddSystemToUpdateList(resetInput);
        group.AddSystemToUpdateList(players);
        Move(root, group, world.GetExistingSystemManaged<SkillProjectileSimulationSystemGroup>());
        Move(root, group, post);
        if (isServer)
        {
            var destroy = world.GetExistingSystem<DestroyEntitySystem>();
            world.GetExistingSystemManaged<GamePresentationSystemGroup>().RemoveSystemFromUpdateList(destroy);
            group.AddSystemToUpdateList(destroy);
        }
        root.SortSystems();
        group.configured = true;
        return group;
    }

    private static void Move(ComponentSystemGroup from, ComponentSystemGroup to, SystemBase system)
    {
        if (system == null)
            return;
        from.RemoveSystemFromUpdateList(system);
        to.AddSystemToUpdateList(system);
    }

    private static void AddManaged<T>(World world, ComponentSystemGroup group) where T : SystemBase
    {
        var system = world.GetExistingSystemManaged<T>();
        if (system != null)
            group.AddSystemToUpdateList(system);
    }

    private static void AddUnmanaged<T>(World world, ComponentSystemGroup group) where T : unmanaged, ISystem
    {
        var system = world.GetExistingSystem<T>();
        if (system != SystemHandle.Null)
            group.AddSystemToUpdateList(system);
    }
}
