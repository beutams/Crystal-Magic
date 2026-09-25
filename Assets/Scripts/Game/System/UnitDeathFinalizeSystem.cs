using CrystalMagic.Core;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup))]
[UpdateBefore(typeof(UnitDropOnDestroySystem))]
partial class UnitDeathFinalizeSystem : SystemBase
{
    private Entity _interactionEntity;
    private EntityQuery _deathQuery;
    private bool _isServer;

    protected override void OnCreate()
    {
        _interactionEntity = GameSingletonUtility.GetEntity<GameInteractionComponent>(EntityManager);
        _deathQuery = GetEntityQuery(ComponentType.ReadOnly<UnitDeathComponent>());
        _isServer = GameWorldContextUtility.Get(EntityManager).Role == GameWorldRole.Server;
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _deathQuery.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (!EntityManager.Exists(entity))
                continue;

            if (_isServer && EntityManager.HasComponent<BattlePlayerStatusComponent>(entity))
            {
                BattlePlayerStatusComponent currentStatus =
                    EntityManager.GetComponentData<BattlePlayerStatusComponent>(entity);
                if (currentStatus.LifeState == BattlePlayerLifeState.Dead)
                    continue;
            }

            if (EntityManager.HasComponent<DestroyEntityFlag>(entity) &&
                EntityManager.IsComponentEnabled<DestroyEntityFlag>(entity))
            {
                continue;
            }

            if (EntityManager.HasComponent<UnitOwnerComponent>(entity))
            {
                Entity owner = EntityManager.GetComponentData<UnitOwnerComponent>(entity).Owner;
                if (owner != Entity.Null)
                {
                    UnitOwnerMemberDeathEvent ownerEvent = new() { Owner = owner };
                    if (EntityManager.HasComponent<UnitOwnerMemberDeathEvent>(entity))
                        EntityManager.SetComponentData(entity, ownerEvent);
                    else
                        EntityManager.AddComponentData(entity, ownerEvent);
                }
            }

            GameInteractionUtility.FailTarget(EntityManager, _interactionEntity, entity);
            EventComponent.Instance?.Publish(new UnitDiedEvent(entity));

            if (_isServer && EntityManager.HasComponent<BattlePlayerStatusComponent>(entity))
            {
                BattlePlayerStatusComponent status =
                    EntityManager.GetComponentData<BattlePlayerStatusComponent>(entity);
                status.LifeState = BattlePlayerLifeState.Dead;
                status.NetworkDirty = 1;
                BattlePlayerStatusUtility.Apply(EntityManager, entity, status);

                if (EntityManager.HasComponent<PlayerInputComponent>(entity))
                {
                    PlayerInputComponent input = EntityManager.GetComponentData<PlayerInputComponent>(entity);
                    input.IsPrimaryHeld = 0;
                    input.ContinuousPrimaryHeld = 0;
                    input.IsInteractHeld = 0;
                    input.IsInventoryHeld = 0;
                    input.IsPropertyHeld = 0;
                    input.IsEscapeHeld = 0;
                    input.IsSkillHeld = 0;
                    input.IsUsePropHeld = 0;
                    EntityManager.SetComponentData(entity, input);
                }

                LoseRandomBackpackItems(entity);
                continue;
            }

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }

    private void LoseRandomBackpackItems(Entity entity)
    {
        if (!EntityManager.HasComponent<PlayerCharacterComponent>(entity))
            return;

        PlayerCharacterComponent character = EntityManager.GetComponentObject<PlayerCharacterComponent>(entity);
        List<InventoryItemData> items = character?.Data?.Backpack?.Items;
        if (items == null || items.Count == 0)
            return;

        List<int> occupiedSlots = new();
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index] != null && !items[index].IsEmpty)
                occupiedSlots.Add(index);
        }
        if (occupiedSlots.Count == 0)
            return;

        uint frame = Server.FrameManagerUtility.TryGet(EntityManager, out Server.FrameManager frameManager)
            ? frameManager.currentFrame
            : 0U;
        uint seed = math.hash(new uint3((uint)entity.Index + 1U, (uint)entity.Version + 1U, frame + 1U));
        Unity.Mathematics.Random random = new(seed == 0U ? 1U : seed);
        int lossCount = math.min(occupiedSlots.Count, random.NextInt(1, 4));
        for (int index = 0; index < lossCount; index++)
        {
            int selected = random.NextInt(index, occupiedSlots.Count);
            (occupiedSlots[index], occupiedSlots[selected]) = (occupiedSlots[selected], occupiedSlots[index]);
            items[occupiedSlots[index]].Clear();
        }

        character.MarkChanged();
    }
}
