using CrystalMagic.Core;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitSourceInitializationSystem))]
public partial class WorldStateSystem : SystemBase
{
    private Entity _worldEntity;

    protected override void OnCreate()
    {
        if (!WorldStateUtility.TryGetEntity(EntityManager, out _worldEntity))
            _worldEntity = EntityManager.CreateEntity(typeof(WorldStateComponent));

        if (!EntityManager.HasComponent<WorldVariableComponent>(_worldEntity))
            EntityManager.AddComponentObject(_worldEntity, new WorldVariableComponent());
    }

    protected override void OnUpdate()
    {
    }
}
