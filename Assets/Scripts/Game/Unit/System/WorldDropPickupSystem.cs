using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(NPCInteractionConsumeSystem))]
[UpdateBefore(typeof(DungeonTreasureSystem))]
partial class WorldDropPickupSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerTag>();
    }

    protected override void OnUpdate()
    {
        float pickupRadius = math.max(0f, ConfigComponent.Instance.Get<GameConfig>().WorldDropPickupRadius);
        float pickupRadiusSq = pickupRadius * pickupRadius;

        bool hasPlayer = false;
        float3 playerPosition = float3.zero;
        foreach ((RefRO<PlayerTag> _, RefRO<LocalTransform> transform) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>())
        {
            playerPosition = transform.ValueRO.Position;
            hasPlayer = true;
            break;
        }

        if (!hasPlayer)
            return;

        BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
        CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();

        foreach ((RefRO<WorldDropComponent> dropRef, RefRO<LocalTransform> transformRef, Entity entity) in
                 SystemAPI.Query<RefRO<WorldDropComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            WorldDropComponent drop = dropRef.ValueRO;
            float distanceSq = math.lengthsq((playerPosition - transformRef.ValueRO.Position).xy);
            if (distanceSq > pickupRadiusSq)
                continue;

            if (!TryPickup(drop, backpackData, propData))
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }

    private static bool TryPickup(in WorldDropComponent drop, BackpackData backpackData, CharacterPropData propData)
    {
        switch (drop.DropType)
        {
            case DropRewardType.Money:
                if (drop.Amount <= 0)
                    return false;

                CurrencyUtility.AddMoneyToCurrentArea(drop.Amount);
                return true;

            case DropRewardType.Item:
            default:
                if (!InventoryUtility.CanAddItemToCharacterInventory(backpackData, propData, drop.ItemId, drop.Amount))
                    return false;

                if (InventoryUtility.AddItemToCharacterInventory(backpackData, propData, drop.ItemId, drop.Amount) <= 0)
                    return false;

                if (PropInventoryUtility.IsPropItem(drop.ItemId))
                    SaveDataComponent.Instance.NotifyCharacterPropDataChanged();
                else
                    SaveDataComponent.Instance.NotifyBackpackDataChanged();
                return true;
        }
    }
}
