using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
public partial class UnitSourceDispatcherSystem : SystemBase
{
    public UnitSourceDispatcher Dispatcher { get; private set; }

    protected override void OnCreate()
    {
        UnitSourceDispatcher dispatcher = default;
        dispatcher.Initialize(this);
        Dispatcher = dispatcher;
    }

    protected override void OnUpdate()
    {
        UnitSourceDispatcher dispatcher = Dispatcher;
        dispatcher.Update(this);
        Dispatcher = dispatcher;
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
        // Managed effects can perform structural changes several times inside the
        // same system update. Refresh the safety handles at the point of use instead
        // of relying only on EntityOrderVersion, which can leave a copied dispatcher
        // holding an invalidated ComponentLookup during condition evaluation.
        dispatcher.Update(system);
        system.Dispatcher = dispatcher;

        return true;
    }
}
