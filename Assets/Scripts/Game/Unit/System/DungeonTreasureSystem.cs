using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(WorldDropPickupSystem))]
partial struct DungeonTreasureSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DungeonTreasureComponent>();
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float3 playerPosition = default;
        bool hasPlayer = false;
        foreach ((RefRO<PlayerTag> _, RefRO<LocalTransform> playerTransform) in SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>())
        {
            playerPosition = playerTransform.ValueRO.Position;
            hasPlayer = true;
            break;
        }

        if (!hasPlayer)
            return;

        BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
        CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();
        bool inventoryChanged = false;
        bool propChanged = false;

        foreach ((RefRW<DungeonTreasureComponent> treasureRef, RefRO<LocalTransform> transformRef, DynamicBuffer<DungeonTreasureRewardElement> rewards, Entity entity) in
                 SystemAPI.Query<RefRW<DungeonTreasureComponent>, RefRO<LocalTransform>, DynamicBuffer<DungeonTreasureRewardElement>>().WithEntityAccess())
        {
            DungeonTreasureComponent treasure = treasureRef.ValueRO;
            if (treasure.IsOpened != 0)
                continue;

            float distanceSq = math.lengthsq((playerPosition - transformRef.ValueRO.Position).xy);
            if (distanceSq > treasure.InteractionRange * treasure.InteractionRange)
                continue;

            if (!TryOpenTreasure(entity, rewards, backpackData, propData, ref inventoryChanged, ref propChanged))
                continue;

            treasure.IsOpened = 1;
            treasureRef.ValueRW = treasure;
            if (!state.EntityManager.HasComponent<DestroyEntityFlag>(entity))
                state.EntityManager.AddComponent<DestroyEntityFlag>(entity);
            state.EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }

        if (propChanged)
            SaveDataComponent.Instance.NotifyCharacterPropDataChanged();
        else if (inventoryChanged)
            SaveDataComponent.Instance.NotifyBackpackDataChanged();
    }

    private static bool TryOpenTreasure(
        Entity entity,
        DynamicBuffer<DungeonTreasureRewardElement> rewards,
        BackpackData backpackData,
        CharacterPropData propData,
        ref bool inventoryChanged,
        ref bool propChanged)
    {
        if (rewards.Length == 0)
            return true;

        Unity.Mathematics.Random random = CreateRandom(entity);
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

            switch (reward.RewardType)
            {
                case DropRewardType.Money:
                    CurrencyUtility.AddMoneyToCurrentArea(quantity);
                    inventoryChanged = true;
                    break;

                case DropRewardType.Item:
                default:
                    if (!InventoryUtility.CanAddItemToCharacterInventory(backpackData, propData, reward.ItemId, quantity))
                        return false;

                    if (InventoryUtility.AddItemToCharacterInventory(backpackData, propData, reward.ItemId, quantity) <= 0)
                        return false;

                    if (PropInventoryUtility.IsPropItem(reward.ItemId))
                        propChanged = true;
                    else
                        inventoryChanged = true;
                    break;
            }
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
}
