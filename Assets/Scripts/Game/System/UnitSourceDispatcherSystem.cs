using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(WorldStateSystem))]
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
        return true;
    }
}
