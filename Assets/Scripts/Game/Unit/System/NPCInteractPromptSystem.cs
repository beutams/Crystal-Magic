using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
[UpdateBefore(typeof(WorldDropPickupSystem))]
[UpdateBefore(typeof(DungeonTreasureSystem))]
[UpdateBefore(typeof(DungeonExitSystem))]
[UpdateBefore(typeof(NPCInteractionSystem))]
partial class NPCInteractPromptSystem : SystemBase
{
    private readonly List<UnitQueryHit> _dropHits = new();

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerTag>();

        Entity singletonEntity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(singletonEntity, new PlayerInteractionRuntimeComponent
        {
            CurrentTarget = Entity.Null,
            CurrentKind = PlayerInteractionKind.None,
        });
    }

    protected override void OnUpdate()
    {
        RefRW<PlayerInteractionRuntimeComponent> runtime = SystemAPI.GetSingletonRW<PlayerInteractionRuntimeComponent>();
        HideLegacyNpcInteractEntities();
        float interactionRange = math.max(0f, ConfigComponent.Instance.Get<GameConfig>().InteractionRange);
        float interactionRangeSq = interactionRange * interactionRange;

        float3 playerPosition = float3.zero;
        Entity playerEntity = Entity.Null;

        foreach ((RefRO<PlayerTag> _, RefRO<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (UnitControlUtility.IsInControlledState(EntityManager, entity))
                break;

            playerPosition = transform.ValueRO.Position;
            playerEntity = entity;
            break;
        }

        if (playerEntity == Entity.Null || GameGateComponent.Instance.IsPlayerInputLocked)
        {
            runtime.ValueRW.CurrentTarget = Entity.Null;
            runtime.ValueRW.CurrentKind = PlayerInteractionKind.None;
            return;
        }

        Entity bestTarget = Entity.Null;
        PlayerInteractionKind bestKind = PlayerInteractionKind.None;
        float bestDistanceSq = float.MaxValue;

        CollectDropCandidate(playerPosition, interactionRange, ref bestTarget, ref bestKind, ref bestDistanceSq);
        CollectTreasureCandidate(playerPosition, interactionRangeSq, ref bestTarget, ref bestKind, ref bestDistanceSq);
        CollectNpcCandidate(playerPosition, interactionRangeSq, ref bestTarget, ref bestKind, ref bestDistanceSq);

        runtime.ValueRW.CurrentTarget = bestTarget;
        runtime.ValueRW.CurrentKind = bestKind;
    }

    private void CollectDropCandidate(
        float3 playerPosition,
        float interactionRange,
        ref Entity bestTarget,
        ref PlayerInteractionKind bestKind,
        ref float bestDistanceSq)
    {
        if (!UnitQueryUtility.TryGetTree(EntityManager, UnitQueryTreeKind.WorldDrop, out UnitQueryTree worldDropTree))
            return;

        worldDropTree.QueryCircle(playerPosition, interactionRange, _dropHits);
        for (int i = 0; i < _dropHits.Count; i++)
        {
            UnitQueryHit hit = _dropHits[i];
            if (EntityManager.HasComponent<DestroyEntityFlag>(hit.Entity) &&
                EntityManager.IsComponentEnabled<DestroyEntityFlag>(hit.Entity))
            {
                continue;
            }

            if (!EntityManager.Exists(hit.Entity) || !EntityManager.HasComponent<WorldDropComponent>(hit.Entity))
                continue;

            float distanceSq = math.lengthsq((playerPosition - hit.Position).xy);
            TrySelectCandidate(hit.Entity, PlayerInteractionKind.Drop, distanceSq, ref bestTarget, ref bestKind, ref bestDistanceSq);
        }
    }

    private void CollectTreasureCandidate(
        float3 playerPosition,
        float interactionRangeSq,
        ref Entity bestTarget,
        ref PlayerInteractionKind bestKind,
        ref float bestDistanceSq)
    {
        foreach ((RefRO<DungeonTreasureComponent> treasureRef, RefRO<LocalTransform> transformRef, Entity entity) in
                 SystemAPI.Query<RefRO<DungeonTreasureComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            DungeonTreasureComponent treasure = treasureRef.ValueRO;
            if (treasure.IsOpened != 0)
                continue;

            float distanceSq = math.lengthsq((playerPosition - transformRef.ValueRO.Position).xy);
            if (distanceSq > interactionRangeSq)
                continue;

            TrySelectCandidate(entity, PlayerInteractionKind.Treasure, distanceSq, ref bestTarget, ref bestKind, ref bestDistanceSq);
        }
    }

    private void CollectNpcCandidate(
        float3 playerPosition,
        float interactionRangeSq,
        ref Entity bestTarget,
        ref PlayerInteractionKind bestKind,
        ref float bestDistanceSq)
    {
        foreach ((RefRO<NPCInteractableComponent> _, RefRO<LocalTransform> transformRef, Entity entity) in
                 SystemAPI.Query<RefRO<NPCInteractableComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (EntityManager.HasComponent<DungeonExitComponent>(entity))
            {
                DungeonExitComponent exit = EntityManager.GetComponentData<DungeonExitComponent>(entity);
                if (exit.IsOpen == 0)
                    continue;
            }

            float distanceSq = math.lengthsq((playerPosition - transformRef.ValueRO.Position).xy);
            if (distanceSq > interactionRangeSq)
                continue;

            TrySelectCandidate(entity, PlayerInteractionKind.Npc, distanceSq, ref bestTarget, ref bestKind, ref bestDistanceSq);
        }
    }

    private void HideLegacyNpcInteractEntities()
    {
        foreach (RefRO<NPCInteractableComponent> interactableRef in SystemAPI.Query<RefRO<NPCInteractableComponent>>())
        {
            NPCInteractableComponent interactable = interactableRef.ValueRO;
            if (interactable.InteractEntity == Entity.Null || !EntityManager.HasComponent<LocalTransform>(interactable.InteractEntity))
                continue;

            LocalTransform interactTransform = EntityManager.GetComponentData<LocalTransform>(interactable.InteractEntity);
            if (math.abs(interactTransform.Scale) <= 0.0001f)
                continue;

            interactTransform.Scale = 0f;
            EntityManager.SetComponentData(interactable.InteractEntity, interactTransform);
        }
    }

    private static void TrySelectCandidate(
        Entity candidateEntity,
        PlayerInteractionKind candidateKind,
        float candidateDistanceSq,
        ref Entity bestTarget,
        ref PlayerInteractionKind bestKind,
        ref float bestDistanceSq)
    {
        if (candidateEntity == Entity.Null)
            return;

        if (candidateDistanceSq + 0.0001f < bestDistanceSq)
        {
            bestTarget = candidateEntity;
            bestKind = candidateKind;
            bestDistanceSq = candidateDistanceSq;
            return;
        }

        if (math.abs(candidateDistanceSq - bestDistanceSq) > 0.0001f)
            return;

        if (GetPriority(candidateKind) >= GetPriority(bestKind))
            return;

        bestTarget = candidateEntity;
        bestKind = candidateKind;
        bestDistanceSq = candidateDistanceSq;
    }

    private static int GetPriority(PlayerInteractionKind kind)
    {
        return kind switch
        {
            PlayerInteractionKind.Drop => 0,
            PlayerInteractionKind.Treasure => 1,
            PlayerInteractionKind.Npc => 2,
            _ => int.MaxValue,
        };
    }
}
