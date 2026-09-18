using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
public partial class StateScriptSystem : SystemBase
{
    private UnitSourceDispatcher _sourceDispatcher;

    protected override void OnCreate()
    {
        _sourceDispatcher.Initialize(this);
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        _sourceDispatcher.Update(this);

        EntityQuery activeQuery = SystemAPI.QueryBuilder()
            .WithAll<UnitStateScriptComponent>()
            .WithNone<UnitDeathComponent>()
            .Build();
        using NativeArray<Entity> activeEntities = activeQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < activeEntities.Length; entityIndex++)
        {
            Entity entity = activeEntities[entityIndex];
            if (!EntityManager.Exists(entity) ||
                !EntityManager.HasComponent<UnitStateScriptComponent>(entity) ||
                EntityManager.HasComponent<UnitDeathComponent>(entity))
            {
                continue;
            }

            UnitStateScriptComponent component = EntityManager.GetComponentObject<UnitStateScriptComponent>(entity);
            if (component == null || !component.IsInitialized || component.IsStoppedForDeath)
                continue;

            for (int i = 0; i < component.Runtimes.Count; i++)
            {
                component.Runtimes[i].Sources.Update(entity, EntityManager, in _sourceDispatcher);
                component.Runtimes[i].Tick(deltaTime);
            }
        }

        EntityQuery deadQuery = SystemAPI.QueryBuilder()
            .WithAll<UnitStateScriptComponent, UnitDeathComponent>()
            .Build();
        using NativeArray<Entity> deadEntities = deadQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < deadEntities.Length; entityIndex++)
        {
            Entity entity = deadEntities[entityIndex];
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<UnitStateScriptComponent>(entity))
                continue;

            UnitStateScriptComponent component = EntityManager.GetComponentObject<UnitStateScriptComponent>(entity);
            if (component == null || component.IsStoppedForDeath)
                continue;

            for (int i = 0; i < component.Runtimes.Count; i++)
                component.Runtimes[i].StopAllWithoutOutput();

            component.IsStoppedForDeath = true;
        }
    }
}
