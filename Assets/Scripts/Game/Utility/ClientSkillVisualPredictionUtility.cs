using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class ClientSkillVisualPredictionUtility
{
    public static Entity GetOrCreateRuntimeEntity(EntityManager entityManager)
    {
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ClientSkillVisualRuntimeComponent>());
        Entity entity;
        if (query.IsEmptyIgnoreFilter)
        {
            entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, default(ClientSkillVisualRuntimeComponent));
        }
        else
        {
            entity = query.GetSingletonEntity();
        }

        if (!entityManager.HasBuffer<ClientSkillVisualRequestElement>(entity))
            entityManager.AddBuffer<ClientSkillVisualRequestElement>(entity);
        if (!entityManager.HasBuffer<ClientPredictedSkillVisualEventElement>(entity))
            entityManager.AddBuffer<ClientPredictedSkillVisualEventElement>(entity);
        return entity;
    }

    public static void RequestRollback(EntityManager entityManager, uint authoritativeFrame)
    {
        Entity runtimeEntity = GetOrCreateRuntimeEntity(entityManager);
        ClientSkillVisualRuntimeComponent runtime =
            entityManager.GetComponentData<ClientSkillVisualRuntimeComponent>(runtimeEntity);
        if (runtime.HasPendingRollback == 0 || authoritativeFrame < runtime.RollbackFrame)
            runtime.RollbackFrame = authoritativeFrame;
        runtime.HasPendingRollback = 1;
        entityManager.SetComponentData(runtimeEntity, runtime);

        DynamicBuffer<ClientSkillVisualRequestElement> requests =
            entityManager.GetBuffer<ClientSkillVisualRequestElement>(runtimeEntity);
        for (int index = requests.Length - 1; index >= 0; index--)
        {
            if (requests[index].Frame > runtime.RollbackFrame)
                requests.RemoveAt(index);
        }
    }

    public static void CaptureSkillRequests(
        EntityManager entityManager,
        Entity player,
        uint frame)
    {
        if (player == Entity.Null ||
            !entityManager.Exists(player) ||
            !entityManager.HasComponent<UnitSkillReleaseComponent>(player))
        {
            return;
        }

        Entity commandQueueEntity = StateScriptManagedCommandQueueUtility.GetOrCreateEntity(entityManager);
        Entity runtimeEntity = GetOrCreateRuntimeEntity(entityManager);
        DynamicBuffer<ClientSkillVisualRequestElement> requests =
            entityManager.GetBuffer<ClientSkillVisualRequestElement>(runtimeEntity);
        DynamicBuffer<StateScriptManagedCommandElement> commands =
            entityManager.GetBuffer<StateScriptManagedCommandElement>(commandQueueEntity, true);
        int requestOrdinal = 0;
        for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
        {
            StateScriptManagedCommandElement command = commands[commandIndex];
            if (command.SourceEntity != player)
                continue;

            bool withAddition = command.Type == StateScriptManagedCommandType.RequestSkillWithAddition;
            if (!withAddition && command.Type != StateScriptManagedCommandType.RequestSkill)
                continue;

            SkillModifierSet modifiers = withAddition
                ? PlayerCurrentSkillUtility.ConsumePendingExtraModifiers(entityManager, player)
                : default;
            SkillReleaseRequest request = SkillReleaseRequestUtility.Create(
                entityManager,
                player,
                command.IntValue,
                modifiers,
                command.Position,
                command.TargetEntity);
            request.CastFrame = frame;
            request.CastOrdinal = requestOrdinal;
            request.HasCastIdentity = 1;
            if (entityManager.HasComponent<UnitMoveComponent>(player))
            {
                UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(player);
                if (move.HasPredictedPosition != 0)
                    request.OriginPosition = move.PredictedPosition;
            }

            requests.Add(new ClientSkillVisualRequestElement
            {
                Frame = frame,
                RequestOrdinal = requestOrdinal++,
                Request = request,
            });
        }
    }

    public static bool TryReconcileNetworkEvent(
        EntityManager entityManager,
        in ClientPresentationEventElement presentationEvent)
    {
        if (presentationEvent.Identity.Valid == 0 ||
            !TryGetRuntimeEntity(entityManager, out Entity runtimeEntity)) return false;
        DynamicBuffer<ClientPredictedSkillVisualEventElement> events =
            entityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity);
        for (int index = 0; index < events.Length; index++)
        {
            ClientPredictedSkillVisualEventElement predicted = events[index];
            if (!predicted.Identity.Equals(presentationEvent.Identity) || predicted.Type != presentationEvent.Type)
                continue;
            predicted.IsConfirmed = 1;
            if (predicted.IsProjectile != 0 && predicted.AnchorEntity != Entity.Null &&
                entityManager.Exists(predicted.AnchorEntity))
            {
                // Confirmation links identities; the local collision simulation and
                // its follow anchor continue until the authoritative impact ends them.
                ClientProjectilePredictionUtility.Link(entityManager, predicted.AnchorEntity, presentationEvent.Target);
                events = entityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity);
            }
            else if (predicted.Identity.Phase != 0 && predicted.VisualEntity != Entity.Null &&
                     entityManager.Exists(predicted.VisualEntity) &&
                     entityManager.HasComponent<LocalTransform>(predicted.VisualEntity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(predicted.VisualEntity);
                transform.Position = presentationEvent.Position;
                entityManager.SetComponentData(predicted.VisualEntity, transform);
            }
            events[index] = predicted;
            // A completed effect with this identity has already been shown, even
            // when its short animation finished before the confirmation arrived.
            return true;
        }
        return false;
    }

    private static bool TryGetRuntimeEntity(EntityManager entityManager, out Entity entity)
    {
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ClientSkillVisualRuntimeComponent>());
        if (query.IsEmptyIgnoreFilter)
        {
            entity = Entity.Null;
            return false;
        }

        entity = query.GetSingletonEntity();
        return entityManager.HasBuffer<ClientPredictedSkillVisualEventElement>(entity);
    }

}
