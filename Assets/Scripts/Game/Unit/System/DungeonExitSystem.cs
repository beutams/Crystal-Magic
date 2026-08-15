using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
partial struct DungeonExitSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DungeonExitComponent>();
    }

    public void OnDestroy(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        NativeParallelHashSet<int> blockedRegions = new(16, Allocator.Temp);
        foreach ((RefRO<DungeonMonsterSpawnComponent> regionRef, Entity entity) in
                 SystemAPI.Query<RefRO<DungeonMonsterSpawnComponent>>().WithEntityAccess())
        {
            if (state.EntityManager.HasComponent<DestroyEntityFlag>(entity) &&
                state.EntityManager.IsComponentEnabled<DestroyEntityFlag>(entity))
            {
                continue;
            }

            blockedRegions.Add(regionRef.ValueRO.RegionId);
        }

        foreach (RefRW<DungeonExitComponent> exitRef in
                 SystemAPI.Query<RefRW<DungeonExitComponent>>())
        {
            DungeonExitComponent exit = exitRef.ValueRO;
            bool wasOpen = exit.IsOpen != 0;
            bool shouldOpen = exit.RequiresRoomClear == 0 || !blockedRegions.Contains(exit.RegionId);
            byte openValue = shouldOpen ? (byte)1 : (byte)0;
            if (exit.IsOpen == openValue)
                continue;

            exit.IsOpen = openValue;
            exitRef.ValueRW = exit;

            if (!wasOpen && shouldOpen)
            {
                int currentFloor = SaveDataComponent.Instance?.GetLocationData()?.DungeonFloor ?? 1;
                SaveDataComponent.Instance?.UnlockDungeonStartFloorAfterBossClear(currentFloor);
            }
        }
    }
}
