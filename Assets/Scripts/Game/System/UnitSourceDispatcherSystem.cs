using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
public partial class UnitSourceDispatcherSystem : SystemBase
{
    private int _entityOrderVersion;

    public UnitSourceDispatcher Dispatcher { get; private set; }

    protected override void OnCreate()
    {
        UnitSourceDispatcher dispatcher = default;
        dispatcher.Initialize(this);
        Dispatcher = dispatcher;
        _entityOrderVersion = EntityManager.EntityOrderVersion;
    }

    protected override void OnUpdate()
    {
        UnitSourceDispatcher dispatcher = Dispatcher;
        dispatcher.Update(this);
        Dispatcher = dispatcher;
        _entityOrderVersion = EntityManager.EntityOrderVersion;
    }

    public static bool TryGet(EntityManager entityManager, out UnitSourceDispatcher dispatcher)
    {
        dispatcher = default;
        World world = entityManager.World;
        if (world == null || !world.IsCreated)
            return false;

        UnitSourceDispatcherSystem system = world.GetExistingSystemManaged<UnitSourceDispatcherSystem>();
        if (system == null)
            return false;

        dispatcher = system.Dispatcher;
        int entityOrderVersion = entityManager.EntityOrderVersion;
        if (system._entityOrderVersion != entityOrderVersion)
        {
            // Managed effects can perform structural changes several times after
            // the initialization group. Refresh once per structural-change version.
            dispatcher.Update(system);
            system.Dispatcher = dispatcher;
            system._entityOrderVersion = entityOrderVersion;
        }

        return true;
    }
}
