using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;

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
            !entityManager.HasComponent<UnitSkillReleaseComponent>(player) ||
            !entityManager.HasBuffer<StateScriptManagedCommandElement>(player))
        {
            return;
        }

        Entity runtimeEntity = GetOrCreateRuntimeEntity(entityManager);
        DynamicBuffer<ClientSkillVisualRequestElement> requests =
            entityManager.GetBuffer<ClientSkillVisualRequestElement>(runtimeEntity);
        DynamicBuffer<StateScriptManagedCommandElement> commands =
            entityManager.GetBuffer<StateScriptManagedCommandElement>(player, true);
        for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
        {
            StateScriptManagedCommandElement command = commands[commandIndex];
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
            if (entityManager.HasComponent<UnitMoveComponent>(player))
            {
                UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(player);
                if (move.HasPredictedPosition != 0)
                    request.OriginPosition = move.PredictedPosition;
            }

            requests.Add(new ClientSkillVisualRequestElement
            {
                Frame = frame,
                RequestOrdinal = commandIndex,
                Request = request,
            });
        }
    }

    public static bool TryReconcileNetworkEvent(
        EntityManager entityManager,
        in ClientPresentationEventElement presentationEvent)
    {
        if (!IsVisualEvent(presentationEvent.Type) ||
            presentationEvent.Source == Entity.Null ||
            presentationEvent.SourceSkillId < 0 ||
            !TryGetRuntimeEntity(entityManager, out Entity runtimeEntity))
        {
            return false;
        }

        DynamicBuffer<ClientPredictedSkillVisualEventElement> events =
            entityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity);
        for (int index = 0; index < events.Length; index++)
        {
            ClientPredictedSkillVisualEventElement predicted = events[index];
            if (predicted.IsConfirmed != 0 ||
                !IsMatchingFrame(predicted.Frame, presentationEvent.Frame) ||
                predicted.Source != presentationEvent.Source ||
                predicted.SkillId != presentationEvent.SourceSkillId ||
                predicted.Type != presentationEvent.Type ||
                !predicted.AssetName.Equals(presentationEvent.AssetName))
            {
                continue;
            }

            predicted.IsConfirmed = 1;
            if (predicted.IsProjectile != 0)
            {
                if (predicted.VisualEntity == Entity.Null ||
                    !entityManager.Exists(predicted.VisualEntity) ||
                    presentationEvent.Target == Entity.Null ||
                    !entityManager.Exists(presentationEvent.Target))
                {
                    events[index] = predicted;
                    return false;
                }

                EffectVisualFollowComponent follow = new()
                {
                    Target = presentationEvent.Target,
                    Offset = presentationEvent.SecondaryPosition,
                    AlignRotation = presentationEvent.FlagA,
                    EndWhenTargetMissing = 1,
                };
                if (entityManager.HasComponent<EffectVisualFollowComponent>(predicted.VisualEntity))
                    entityManager.SetComponentData(predicted.VisualEntity, follow);
                else
                    entityManager.AddComponentData(predicted.VisualEntity, follow);

                if (predicted.AnchorEntity != Entity.Null && entityManager.Exists(predicted.AnchorEntity))
                    entityManager.DestroyEntity(predicted.AnchorEntity);
                predicted.AnchorEntity = Entity.Null;
            }

            events[index] = predicted;
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

    private static bool IsVisualEvent(ClientPresentationEventType type)
    {
        return type == ClientPresentationEventType.SpawnVfx ||
               type == ClientPresentationEventType.SpawnFollowVfx ||
               type == ClientPresentationEventType.SpawnLineVfx ||
               type == ClientPresentationEventType.MoveVfx;
    }

    private static bool IsMatchingFrame(uint predictedFrame, uint authoritativeFrame)
    {
        return authoritativeFrame >= predictedFrame && authoritativeFrame - predictedFrame <= 2u;
    }
}
