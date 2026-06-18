using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(NPCInteractPromptSystem))]
[UpdateBefore(typeof(NPCInteractionSystem))]
partial class WorldDropPickupSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerTag>();
        RequireForUpdate<PlayerInteractionRuntimeComponent>();
    }

    protected override void OnUpdate()
    {
        RefRW<PlayerInteractionRuntimeComponent> runtime = SystemAPI.GetSingletonRW<PlayerInteractionRuntimeComponent>();
        if (runtime.ValueRO.CurrentKind != PlayerInteractionKind.Drop || runtime.ValueRO.CurrentTarget == Entity.Null)
            return;

        Entity playerEntity = Entity.Null;
        bool wantToInteract = false;
        foreach ((RefRO<PlayerTag> _, RefRO<UnitIntentComponent> intentRef, Entity entity) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<UnitIntentComponent>>().WithEntityAccess())
        {
            playerEntity = entity;
            wantToInteract = !UnitControlUtility.IsInControlledState(EntityManager, entity) && intentRef.ValueRO.WantToInteract;
            break;
        }

        if (playerEntity == Entity.Null || !wantToInteract)
            return;

        Entity target = runtime.ValueRO.CurrentTarget;
        if (!EntityManager.Exists(target) || !EntityManager.HasComponent<WorldDropComponent>(target))
        {
            runtime.ValueRW.CurrentTarget = Entity.Null;
            runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
            return;
        }

        if (EntityManager.HasComponent<DestroyEntityFlag>(target) &&
            EntityManager.IsComponentEnabled<DestroyEntityFlag>(target))
        {
            runtime.ValueRW.CurrentTarget = Entity.Null;
            runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
            return;
        }

        BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
        WorldDropComponent dropData = EntityManager.GetComponentData<WorldDropComponent>(target);

        ConsumeInteract(playerEntity);

        if (!TryPickup(dropData, backpackData))
            return;

        if (!EntityManager.HasComponent<DestroyEntityFlag>(target))
            EntityManager.AddComponent<DestroyEntityFlag>(target);

        EntityManager.SetComponentEnabled<DestroyEntityFlag>(target, true);
        runtime.ValueRW.CurrentTarget = Entity.Null;
        runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
    }

    private void ConsumeInteract(Entity playerEntity)
    {
        if (playerEntity == Entity.Null || !EntityManager.HasComponent<UnitIntentComponent>(playerEntity))
            return;

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(playerEntity);
        intent.WantToInteract = false;
        EntityManager.SetComponentData(playerEntity, intent);
    }

    private static bool TryPickup(in WorldDropComponent drop, BackpackData backpackData)
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
                if (!InventoryUtility.CanAddItemToBackpack(backpackData, drop.ItemId, drop.Amount))
                    return false;

                if (InventoryUtility.AddItemToBackpack(backpackData, drop.ItemId, drop.Amount) <= 0)
                    return false;

                SaveDataComponent.Instance.NotifyBackpackDataChanged();
                return true;
        }
    }
}
