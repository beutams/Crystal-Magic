using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Automatically requests nearby money drops for player entities.
/// The actual reward grant remains in the existing interaction/state-script flow,
/// so the server (or the local simulation) is still the only authority that adds money.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitNavigationSystem))]
[UpdateBefore(typeof(StateScriptSystem))]
public partial class AutoPickupSystem : SystemBase
{
    private Entity _interactionEntity;
    private EntityQuery _playerQuery;
    private EntityQuery _dropQuery;

    protected override void OnCreate()
    {
        _interactionEntity = GameSingletonUtility.GetEntity<GameInteractionComponent>(EntityManager);
        _playerQuery = GetEntityQuery(
            ComponentType.ReadOnly<PlayerInputComponent>(),
            ComponentType.ReadOnly<UnitStateScriptComponent>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.Exclude<UnitInitializationPendingTag>(),
            ComponentType.Exclude<UnitDeathComponent>(),
            ComponentType.Exclude<DestroyEntityFlag>());
        _dropQuery = GetEntityQuery(
            ComponentType.ReadOnly<UnitInteractableComponent>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.Exclude<UnitInitializationPendingTag>(),
            ComponentType.Exclude<UnitDeathComponent>(),
            ComponentType.Exclude<DestroyEntityFlag>());
    }

    protected override void OnUpdate()
    {
        if (_interactionEntity == Entity.Null ||
            !EntityManager.Exists(_interactionEntity) ||
            _playerQuery.IsEmptyIgnoreFilter ||
            _dropQuery.IsEmptyIgnoreFilter)
        {
            return;
        }

        using NativeArray<Entity> players = _playerQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<Entity> drops = _dropQuery.ToEntityArray(Allocator.Temp);

        for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
        {
            Entity player = players[playerIndex];
            float3 playerPosition = EntityManager.GetComponentData<LocalTransform>(player).Position;
            Entity nearestMoneyDrop = Entity.Null;
            float nearestDistanceSq = float.MaxValue;

            for (int dropIndex = 0; dropIndex < drops.Length; dropIndex++)
            {
                Entity drop = drops[dropIndex];
                if (!EntityManager.Exists(drop) ||
                    !EntityManager.HasComponent<UnitInteractableComponent>(drop))
                {
                    continue;
                }

                UnitInteractableComponent interactable =
                    EntityManager.GetComponentData<UnitInteractableComponent>(drop);
                UnitInteractionData data = interactable.Data;
                if (interactable.IsEnabled == 0 ||
                    data.Kind != InteractionKind.Drop ||
                    (DropRewardType)data.Variant != DropRewardType.Money ||
                    data.Amount <= 0)
                {
                    continue;
                }

                float3 dropPosition = EntityManager.GetComponentData<LocalTransform>(drop).Position;
                float distanceSq = math.lengthsq((playerPosition - dropPosition).xy);
                if (interactable.RangeSq > 0f && distanceSq > interactable.RangeSq)
                    continue;

                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestMoneyDrop = drop;
                }
            }

            if (nearestMoneyDrop != Entity.Null)
                GameInteractionUtility.TryRequest(
                    EntityManager,
                    _interactionEntity,
                    player,
                    nearestMoneyDrop);
        }
    }
}
