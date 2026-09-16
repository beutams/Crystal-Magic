using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
[UpdateBefore(typeof(GameInteractionSystem))]
public partial class InteractionCandidateSystem : SystemBase
{
    private readonly List<UnitQueryHit> _hits = new();

    protected override void OnCreate()
    {
        RequireForUpdate<UnitFactionComponent>();
        Entity singleton = EntityManager.CreateEntity();
        EntityManager.AddComponentData(singleton, new InteractionCandidateComponent
        {
            Target = Entity.Null,
        });
    }

    protected override void OnUpdate()
    {
        RefRW<InteractionCandidateComponent> candidate = SystemAPI.GetSingletonRW<InteractionCandidateComponent>();
        byte isInteracting = candidate.ValueRO.IsInteracting;
        candidate.ValueRW = new InteractionCandidateComponent
        {
            IsInteracting = isInteracting,
        };

        if (isInteracting != 0 || GameGateComponent.Instance.IsPlayerInputLocked ||
            !TryGetFrontEndActor(out float3 actorPosition))
        {
            return;
        }

        if (!UnitQueryUtility.TryGetTree(EntityManager, UnitQueryTreeKind.Interactable, out UnitQueryTree tree))
            return;

        float queryRange = math.max(0f, ConfigComponent.Instance.Get<GameConfig>().InteractionRange);
        tree.QueryCircle(actorPosition, queryRange, _hits);

        Entity bestTarget = Entity.Null;
        UnitInteractionData bestData = default;
        float bestDistanceSq = float.MaxValue;
        for (int i = 0; i < _hits.Count; i++)
        {
            UnitQueryHit hit = _hits[i];
            if (!EntityManager.Exists(hit.Entity) || !EntityManager.HasComponent<UnitInteractableComponent>(hit.Entity))
                continue;

            UnitInteractableComponent interactable = EntityManager.GetComponentData<UnitInteractableComponent>(hit.Entity);
            if (!GameInteractionTargetUtility.IsAvailable(EntityManager, hit.Entity, interactable))
                continue;

            float distanceSq = math.lengthsq((actorPosition - hit.Position).xy);
            float rangeSq = interactable.RangeSq > 0f ? interactable.RangeSq : queryRange * queryRange;
            if (distanceSq > rangeSq)
                continue;

            if (!IsBetter(interactable.Data.Kind, distanceSq, bestData.Kind, bestDistanceSq))
                continue;

            bestTarget = hit.Entity;
            bestData = interactable.Data;
            bestDistanceSq = distanceSq;
        }

        candidate.ValueRW = new InteractionCandidateComponent
        {
            Target = bestTarget,
            Data = bestData,
            IsInteracting = isInteracting,
        };
    }

    private bool TryGetFrontEndActor(out float3 position)
    {
        foreach ((RefRO<UnitFactionComponent> faction, RefRO<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<UnitFactionComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (!UnitFactionUtility.IsPlayer(faction.ValueRO.Value))
                continue;

            if (UnitControlUtility.HasActiveControl(EntityManager, entity))
                break;

            position = transform.ValueRO.Position;
            return true;
        }

        position = default;
        return false;
    }

    private static bool IsBetter(InteractionKind candidateKind, float candidateDistanceSq, InteractionKind bestKind, float bestDistanceSq)
    {
        if (candidateDistanceSq + 0.0001f < bestDistanceSq)
            return true;

        if (math.abs(candidateDistanceSq - bestDistanceSq) > 0.0001f)
            return false;

        return GameInteractionTargetUtility.GetPriority(candidateKind) < GameInteractionTargetUtility.GetPriority(bestKind);
    }
}
