using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

internal readonly struct PendingDungeonExitMaterialChange
{
    public PendingDungeonExitMaterialChange(Entity entity, string materialPath)
    {
        Entity = entity;
        MaterialPath = materialPath;
    }

    public Entity Entity { get; }
    public string MaterialPath { get; }
}

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(DungeonTreasureSystem))]
partial struct DungeonExitSystem : ISystem
{
    private NativeReference<bool> _interactRequested;
    private bool _subscribed;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DungeonExitComponent>();
        state.RequireForUpdate<PlayerTag>();
        _interactRequested = new NativeReference<bool>(false, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_subscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
            inputComponent.OnInteract -= HandleInteract;

        if (_interactRequested.IsCreated)
            _interactRequested.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        DungeonExitInteractionGraph.Tick(SystemAPI.Time.DeltaTime);
        System.Collections.Generic.List<PendingDungeonExitMaterialChange> pendingMaterialChanges = new();

        if (!_subscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
        {
            inputComponent.OnInteract += HandleInteract;
            _subscribed = true;
        }

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

        foreach ((RefRW<DungeonExitComponent> exitRef, Entity entity) in
                 SystemAPI.Query<RefRW<DungeonExitComponent>>().WithEntityAccess())
        {
            DungeonExitComponent exit = exitRef.ValueRO;
            bool wasOpen = exit.IsOpen != 0;
            bool shouldOpen = exit.RequiresRoomClear == 0 || !blockedRegions.Contains(exit.RegionId);
            byte openValue = shouldOpen ? (byte)1 : (byte)0;
            if (exit.IsOpen == openValue)
                continue;

            exit.IsOpen = openValue;
            exitRef.ValueRW = exit;
            pendingMaterialChanges.Add(new PendingDungeonExitMaterialChange(
                entity,
                shouldOpen ? exit.OpenMaterialPath.ToString() : exit.ClosedMaterialPath.ToString()));

            if (!wasOpen && shouldOpen)
            {
                int currentFloor = SaveDataComponent.Instance?.GetLocationData()?.DungeonFloor ?? 1;
                SaveDataComponent.Instance?.UnlockDungeonStartFloorAfterBossClear(currentFloor);
            }
        }

        for (int i = 0; i < pendingMaterialChanges.Count; i++)
        {
            PendingDungeonExitMaterialChange pending = pendingMaterialChanges[i];
            if (!state.EntityManager.Exists(pending.Entity))
                continue;

            DungeonSceneVisualUtility.ApplySceneObjectMaterial(state.EntityManager, pending.Entity, "Exit", pending.MaterialPath);
        }

        if (!_interactRequested.Value)
            return;

        _interactRequested.Value = false;
        if (GameGateComponent.TryGetInstance(out GameGateComponent gateComponent) && gateComponent.IsPlayerInputLocked)
            return;

        if (TransitionComponent.Instance != null && TransitionComponent.Instance.IsTransitioning)
            return;

        Entity nearestExit = Entity.Null;
        float nearestDistanceSq = float.MaxValue;

        foreach ((RefRO<PlayerTag> _, RefRO<LocalTransform> playerTransform) in SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>())
        {
            float3 playerPosition = playerTransform.ValueRO.Position;
            foreach ((RefRO<DungeonExitComponent> exitRef, RefRO<LocalTransform> exitTransform, Entity entity) in
                     SystemAPI.Query<RefRO<DungeonExitComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                DungeonExitComponent exit = exitRef.ValueRO;
                if (exit.IsOpen == 0)
                    continue;

                float distanceSq = math.lengthsq((playerPosition - exitTransform.ValueRO.Position).xy);
                float interactionRangeSq = exit.InteractionRange * exit.InteractionRange;
                if (distanceSq > interactionRangeSq || distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                nearestExit = entity;
            }

            break;
        }

        if (nearestExit == Entity.Null || !state.EntityManager.Exists(nearestExit))
            return;

        DungeonExitComponent nearestExitData = state.EntityManager.GetComponentData<DungeonExitComponent>(nearestExit);
        DungeonExitInteractionGraph.TryOpen(nearestExitData.TargetFloor);
    }

    private void HandleInteract()
    {
        _interactRequested.Value = true;
    }
}
