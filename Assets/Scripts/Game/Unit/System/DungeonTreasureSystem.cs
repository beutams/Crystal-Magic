using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(WorldDropPickupSystem))]
[UpdateBefore(typeof(DungeonExitSystem))]
[UpdateBefore(typeof(NPCInteractionSystem))]
partial struct DungeonTreasureSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DungeonTreasureComponent>();
        state.RequireForUpdate<PlayerTag>();
        state.RequireForUpdate<PlayerInteractionRuntimeComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        RefRW<PlayerInteractionRuntimeComponent> runtime = SystemAPI.GetSingletonRW<PlayerInteractionRuntimeComponent>();
        if (runtime.ValueRO.CurrentKind != PlayerInteractionKind.Treasure || runtime.ValueRO.CurrentTarget == Entity.Null)
            return;

        Entity playerEntity = Entity.Null;
        bool wantToInteract = false;
        foreach ((RefRO<PlayerTag> _, RefRO<UnitIntentComponent> intentRef, Entity entity) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<UnitIntentComponent>>().WithEntityAccess())
        {
            if (UnitControlUtility.IsInControlledState(state.EntityManager, entity))
                break;

            playerEntity = entity;
            wantToInteract = intentRef.ValueRO.WantToInteract;
            break;
        }

        if (playerEntity == Entity.Null || !wantToInteract)
            return;

        Entity target = runtime.ValueRO.CurrentTarget;
        if (!state.EntityManager.Exists(target) || !state.EntityManager.HasComponent<DungeonTreasureComponent>(target))
        {
            runtime.ValueRW.CurrentTarget = Entity.Null;
            runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
            return;
        }

        DungeonTreasureComponent targetTreasure = state.EntityManager.GetComponentData<DungeonTreasureComponent>(target);
        if (targetTreasure.IsOpened != 0)
        {
            runtime.ValueRW.CurrentTarget = Entity.Null;
            runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
            return;
        }

        DynamicBuffer<DungeonTreasureRewardElement> rewards = state.EntityManager.GetBuffer<DungeonTreasureRewardElement>(target);
        float3 treasurePosition = state.EntityManager.GetComponentData<LocalTransform>(target).Position;
        if (!TryOpenTreasure(ref state, target, treasurePosition, rewards))
            return;

        targetTreasure.IsOpened = 1;
        state.EntityManager.SetComponentData(target, targetTreasure);
        runtime.ValueRW.CurrentTarget = Entity.Null;
        runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
        ConsumeInteract(ref state, playerEntity);
    }

    private static bool TryOpenTreasure(
        ref SystemState state,
        Entity entity,
        float3 position,
        DynamicBuffer<DungeonTreasureRewardElement> rewards)
    {
        if (rewards.Length == 0)
            return true;

        Unity.Mathematics.Random random = CreateRandom(entity);
        using NativeList<PendingTreasureReward> pendingRewards = new NativeList<PendingTreasureReward>(rewards.Length, Allocator.Temp);
        for (int i = 0; i < rewards.Length; i++)
        {
            DungeonTreasureRewardElement reward = rewards[i];
            if (reward.Chance <= 0f || random.NextFloat() > math.clamp(reward.Chance, 0f, 1f))
                continue;

            int minQuantity = math.max(0, reward.MinQuantity);
            int maxQuantity = math.max(minQuantity, reward.MaxQuantity);
            int quantity = maxQuantity > minQuantity ? random.NextInt(minQuantity, maxQuantity + 1) : minQuantity;
            if (quantity <= 0)
                continue;

            pendingRewards.Add(new PendingTreasureReward
            {
                RewardType = reward.RewardType,
                ItemId = reward.ItemId,
                Quantity = quantity,
            });
        }

        if (pendingRewards.Length == 0)
            return true;

        if (!WorldDropSpawnUtility.CanSpawnDrop(state.EntityManager))
            return false;

        for (int i = 0; i < pendingRewards.Length; i++)
        {
            PendingTreasureReward reward = pendingRewards[i];
            if (!WorldDropSpawnUtility.TrySpawnDrop(state.EntityManager, reward.RewardType, reward.ItemId, reward.Quantity, position))
                return false;
        }

        return true;
    }

    private static Unity.Mathematics.Random CreateRandom(Entity entity)
    {
        uint seed = math.hash(new int2(entity.Index, entity.Version));
        if (seed == 0)
            seed = 1u;
        return new Unity.Mathematics.Random(seed);
    }

    private static void ConsumeInteract(ref SystemState state, Entity playerEntity)
    {
        if (playerEntity == Entity.Null || !state.EntityManager.HasComponent<UnitIntentComponent>(playerEntity))
            return;

        UnitIntentComponent intent = state.EntityManager.GetComponentData<UnitIntentComponent>(playerEntity);
        intent.WantToInteract = false;
        state.EntityManager.SetComponentData(playerEntity, intent);
    }

    private struct PendingTreasureReward
    {
        public DropRewardType RewardType;
        public int ItemId;
        public int Quantity;
    }
}
