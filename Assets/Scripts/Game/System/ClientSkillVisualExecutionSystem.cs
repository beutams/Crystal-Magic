using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientTransformInterpolationSystem))]
[UpdateBefore(typeof(ClientPredictedProjectileSystem))]
public partial class ClientSkillVisualExecutionSystem : SystemBase
{
    private const uint JournalLifetimeFrames = 256;

    private EntityQuery _runtimeQuery;

    protected override void OnCreate()
    {
        Entity runtimeEntity = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(EntityManager);
        _runtimeQuery = GetEntityQuery(
            ComponentType.ReadWrite<ClientSkillVisualRuntimeComponent>(),
            ComponentType.ReadWrite<ClientSkillVisualRequestElement>(),
            ComponentType.ReadWrite<ClientPredictedSkillVisualEventElement>());
        RequireForUpdate(_runtimeQuery);
    }

    protected override void OnUpdate()
    {
        Entity runtimeEntity = _runtimeQuery.GetSingletonEntity();
        ClientSkillVisualRuntimeComponent runtime =
            EntityManager.GetComponentData<ClientSkillVisualRuntimeComponent>(runtimeEntity);
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal =
            EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity);
        if (runtime.HasPendingRollback != 0)
        {
            CleanupRollback(runtime.RollbackFrame, journal);
            runtime.HasPendingRollback = 0;
            EntityManager.SetComponentData(runtimeEntity, runtime);
        }

        DynamicBuffer<ClientSkillVisualRequestElement> requestBuffer =
            EntityManager.GetBuffer<ClientSkillVisualRequestElement>(runtimeEntity);
        using NativeArray<ClientSkillVisualRequestElement> requests =
            requestBuffer.ToNativeArray(Allocator.Temp);
        requestBuffer.Clear();
        for (int index = 0; index < requests.Length; index++)
            ExecuteRequest(requests[index], journal);

        if (FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame))
            TrimJournal(frame.currentFrame, journal);
    }

    private void ExecuteRequest(
        in ClientSkillVisualRequestElement visualRequest,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        SkillReleaseRequest request = visualRequest.Request;
        if (!SkillReleaseSnapshotUtility.TryCreate(
                EntityManager,
                in request,
                out ResolvedSkillData resolvedSkill) ||
            resolvedSkill?.EffectChain == null)
        {
            return;
        }

        SkillContent context = new()
        {
            EntityManager = EntityManager,
            TriggerSource = SkillTriggerSource.ActiveCast,
            HookType = SkillHookType.None,
            HasOriginEntity = request.OriginEntity != Entity.Null,
            OriginEntity = request.OriginEntity,
            HasOriginPositionSnapshot = true,
            OriginPositionSnapshot = new Vector3(
                request.OriginPosition.x,
                request.OriginPosition.y,
                request.OriginPosition.z),
            SourceSkillId = request.SkillId,
            HasTargetEntity = request.HasTargetEntity,
            TargetEntity = request.TargetEntity,
            HasPosition = request.HasTargetPosition,
            Position = new Vector3(
                request.TargetPosition.x,
                request.TargetPosition.y,
                request.TargetPosition.z),
        };
        ExecuteVisualEffects(
            resolvedSkill.EffectChain,
            context,
            visualRequest,
            journal,
            0);
    }

    private void ExecuteVisualEffects(
        EffectData[] effects,
        SkillContent context,
        in ClientSkillVisualRequestElement request,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal,
        int path)
    {
        if (effects == null)
            return;

        for (int index = 0; index < effects.Length; index++)
        {
            EffectData effect = effects[index];
            if (effect == null || !PassEffectConditions(effect, context))
                continue;

            int effectOrdinal = ComposeEffectOrdinal(path, index);
            if (HasJournalEntry(journal, request.Frame, request.RequestOrdinal, effectOrdinal))
                continue;

            switch (effect)
            {
                case SpawnVfxEffectData spawnVfx:
                    SpawnVfx(spawnVfx, context, request, effectOrdinal, journal);
                    break;
                case SpawnFollowVfxEffectData followVfx:
                    SpawnFollowVfx(followVfx, context, request, effectOrdinal, journal);
                    break;
                case SpawnLineVfxEffectData lineVfx:
                    SpawnLineVfx(lineVfx, context, request, effectOrdinal, journal);
                    break;
                case MoveVfxEffectData moveVfx:
                    SpawnMoveVfx(moveVfx, context, request, effectOrdinal, journal);
                    break;
                case SpawnProjectileEffectData projectile:
                    SpawnProjectileVfx(projectile, context, request, effectOrdinal, journal);
                    break;
                case PersistentEffectData persistent:
                    if (TryGetPersistentReleasePosition(context, out float3 persistentPosition))
                    {
                        SkillContent persistentContext = context.Clone();
                        persistentContext.HasPosition = true;
                        persistentContext.Position = new Vector3(
                            persistentPosition.x,
                            persistentPosition.y,
                            persistentPosition.z);
                        persistentContext.HasTargetEntity = false;
                        persistentContext.TargetEntity = Entity.Null;
                        ExecuteVisualEffects(
                            persistent.OnStartEffects,
                            persistentContext,
                            request,
                            journal,
                            effectOrdinal);
                    }
                    break;
            }
        }
    }

    private void SpawnVfx(
        SpawnVfxEffectData data,
        SkillContent context,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        if (string.IsNullOrWhiteSpace(data.VfxPrefabName) ||
            !SpriteEffectSpawnUtility.TryGetReleasePosition(context, out float3 position))
        {
            return;
        }

        quaternion rotation = SpriteEffectSpawnUtility.GetFacingRotation(context, data.AlignToCasterForward);
        position += math.rotate(rotation, new float3(data.SpawnOffset.x, data.SpawnOffset.y, data.SpawnOffset.z));
        if (!SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                data.VfxPrefabName,
                position,
                rotation,
                data.Scale,
                data.Duration,
                out Entity visualEntity))
        {
            return;
        }

        TagVisual(visualEntity, request, effectOrdinal, 0);
        AddJournalEntry(
            journal,
            request,
            effectOrdinal,
            ClientPresentationEventType.SpawnVfx,
            data.VfxPrefabName,
            position,
            visualEntity,
            Entity.Null,
            false);
    }

    private void SpawnFollowVfx(
        SpawnFollowVfxEffectData data,
        SkillContent context,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        if (string.IsNullOrWhiteSpace(data.VfxPrefabName) ||
            !SpriteEffectSpawnUtility.TryGetFollowTarget(
                context,
                data.FollowTarget == SpawnVfxFollowTarget.TargetEntity,
                out Entity target,
                out LocalTransform targetTransform))
        {
            return;
        }

        quaternion rotation = SpriteEffectSpawnUtility.GetEntityFacingRotation(
            EntityManager,
            target,
            data.AlignToTargetForward);
        float3 offset = new(data.SpawnOffset.x, data.SpawnOffset.y, data.SpawnOffset.z);
        float3 position = targetTransform.Position + math.rotate(rotation, offset);
        if (!SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                data.VfxPrefabName,
                position,
                rotation,
                data.Scale,
                data.Duration,
                out Entity visualEntity))
        {
            return;
        }

        SpriteEffectSpawnUtility.SetOrAddComponentData(
            EntityManager,
            visualEntity,
            new EffectVisualFollowComponent
            {
                Target = target,
                Offset = offset,
                AlignRotation = data.AlignToTargetForward ? (byte)1 : (byte)0,
                EndWhenTargetMissing = 1,
            });
        TagVisual(visualEntity, request, effectOrdinal, 0);
        AddJournalEntry(
            journal,
            request,
            effectOrdinal,
            ClientPresentationEventType.SpawnFollowVfx,
            data.VfxPrefabName,
            position,
            visualEntity,
            Entity.Null,
            false);
    }

    private void SpawnLineVfx(
        SpawnLineVfxEffectData data,
        SkillContent context,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        if (string.IsNullOrWhiteSpace(data.VfxPrefabName) ||
            !context.HasPosition ||
            !TryGetOriginPosition(context, out float3 originPosition))
        {
            return;
        }

        float3 targetPosition = new(context.Position.x, context.Position.y, context.Position.z);
        float2 direction = targetPosition.xy - originPosition.xy;
        if (math.lengthsq(direction) <= 0.0001f)
            return;

        direction = math.normalize(direction);
        float3 firstPosition = originPosition +
                               new float3(direction.x, direction.y, 0f) * data.OriginOffsetDistance;
        float spacing = math.max(0.01f, data.SegmentSpacing);
        int segmentCount = math.max(1, (int)math.ceil(math.max(0f, data.Length) / spacing));
        quaternion rotation = data.AlignToLineDirection
            ? UnitFacingUtility.CreateRotation(direction)
            : quaternion.identity;
        Entity firstVisual = Entity.Null;
        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            float3 position = firstPosition +
                              new float3(direction.x, direction.y, 0f) * (spacing * segmentIndex);
            if (!SpriteEffectSpawnUtility.TrySpawn(
                    EntityManager,
                    data.VfxPrefabName,
                    position,
                    rotation,
                    data.Scale,
                    data.Duration,
                    preservePrefabRotation: !data.AlignToLineDirection,
                    out Entity visualEntity))
            {
                continue;
            }

            if (firstVisual == Entity.Null)
                firstVisual = visualEntity;
            TagVisual(visualEntity, request, effectOrdinal, segmentIndex);
        }

        if (firstVisual == Entity.Null)
            return;
        AddJournalEntry(
            journal,
            request,
            effectOrdinal,
            ClientPresentationEventType.SpawnLineVfx,
            data.VfxPrefabName,
            firstPosition,
            firstVisual,
            Entity.Null,
            false);
    }

    private void SpawnMoveVfx(
        MoveVfxEffectData data,
        SkillContent context,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        if (string.IsNullOrWhiteSpace(data.VfxPrefabName) ||
            !SpriteEffectSpawnUtility.TryGetReleasePosition(context, out float3 releasePosition))
        {
            return;
        }

        float3 startPosition = releasePosition +
                               new float3(data.StartOffset.x, data.StartOffset.y, data.StartOffset.z);
        float3 endPosition = startPosition +
                             new float3(data.MoveOffset.x, data.MoveOffset.y, data.MoveOffset.z);
        float duration = math.max(0.001f, data.Duration);
        if (!SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                data.VfxPrefabName,
                startPosition,
                quaternion.identity,
                data.Scale,
                0f,
                data.PreservePrefabRotation,
                out Entity visualEntity))
        {
            return;
        }

        SpriteEffectSpawnUtility.SetOrAddComponentData(
            EntityManager,
            visualEntity,
            new ClientTransformInterpolationComponent
            {
                FromPosition = startPosition,
                TargetPosition = endPosition,
                FromFrame = request.Frame,
                TargetFrame = request.Frame,
                StartRealtime = UnityEngine.Time.realtimeSinceStartupAsDouble,
                Duration = duration,
                Initialized = 1,
            });
        TagVisual(visualEntity, request, effectOrdinal, 0);
        AddJournalEntry(
            journal,
            request,
            effectOrdinal,
            ClientPresentationEventType.MoveVfx,
            data.VfxPrefabName,
            startPosition,
            visualEntity,
            Entity.Null,
            false);
    }

    private void SpawnProjectileVfx(
        SpawnProjectileEffectData data,
        SkillContent context,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        if (string.IsNullOrWhiteSpace(data.VisualPrefabName) ||
            !TryGetOriginPosition(context, out float3 originPosition) ||
            !context.HasPosition)
        {
            return;
        }

        float3 targetPosition = new(context.Position.x, context.Position.y, context.Position.z);
        float3 direction = targetPosition - originPosition;
        direction.z = 0f;
        if (math.lengthsq(direction) <= 0.0001f)
            return;

        direction = math.normalize(direction);
        float3 startPosition = originPosition + direction * data.SpawnOffsetDistance;
        float traveledDistance = math.max(0f, data.Speed) * GetPredictionAgeSeconds(request.Frame);
        if (data.MaxRange > 0f && traveledDistance >= data.MaxRange)
            return;
        startPosition += direction * traveledDistance;
        quaternion rotation = quaternion.RotateZ(math.atan2(direction.y, direction.x));
        Entity anchorEntity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(
            anchorEntity,
            LocalTransform.FromPositionRotationScale(startPosition, rotation, 1f));
        EntityManager.AddComponentData(anchorEntity, new ClientPredictedProjectileComponent
        {
            Direction = direction,
            Speed = math.max(0f, data.Speed),
            MaxRange = math.max(0f, data.MaxRange),
            TraveledDistance = traveledDistance,
        });
        EntityManager.AddComponent<DungeonRuntimeOwnedEntity>(anchorEntity);
        EntityManager.AddComponent<DestroyEntityFlag>(anchorEntity);
        EntityManager.SetComponentEnabled<DestroyEntityFlag>(anchorEntity, false);
        TagVisual(anchorEntity, request, effectOrdinal, 0);

        if (!SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                data.VisualPrefabName,
                startPosition,
                rotation,
                data.VisualScale,
                0f,
                out Entity visualEntity))
        {
            EntityManager.DestroyEntity(anchorEntity);
            return;
        }

        float3 offset = new(data.VisualOffset.x, data.VisualOffset.y, data.VisualOffset.z);
        SpriteEffectSpawnUtility.SetOrAddComponentData(
            EntityManager,
            visualEntity,
            new EffectVisualFollowComponent
            {
                Target = anchorEntity,
                Offset = offset,
                AlignRotation = 1,
                EndWhenTargetMissing = 1,
            });
        TagVisual(visualEntity, request, effectOrdinal, 1);
        AddJournalEntry(
            journal,
            request,
            effectOrdinal,
            ClientPresentationEventType.SpawnFollowVfx,
            data.VisualPrefabName,
            startPosition,
            visualEntity,
            anchorEntity,
            true);
    }

    private void CleanupRollback(
        uint rollbackFrame,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        using EntityQuery query = EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ClientPredictedSkillVisualComponent>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (EntityManager.GetComponentData<ClientPredictedSkillVisualComponent>(entity).Frame > rollbackFrame &&
                EntityManager.Exists(entity))
            {
                EntityManager.DestroyEntity(entity);
            }
        }

        for (int index = journal.Length - 1; index >= 0; index--)
        {
            if (journal[index].Frame > rollbackFrame)
                journal.RemoveAt(index);
        }
    }

    private static void TrimJournal(
        uint currentFrame,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        for (int index = journal.Length - 1; index >= 0; index--)
        {
            uint eventFrame = journal[index].Frame;
            if (currentFrame >= eventFrame && currentFrame - eventFrame > JournalLifetimeFrames)
                journal.RemoveAt(index);
        }
    }

    private static bool HasJournalEntry(
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal,
        uint frame,
        int requestOrdinal,
        int effectOrdinal)
    {
        for (int index = 0; index < journal.Length; index++)
        {
            ClientPredictedSkillVisualEventElement entry = journal[index];
            if (entry.Frame == frame &&
                entry.RequestOrdinal == requestOrdinal &&
                entry.EffectOrdinal == effectOrdinal)
            {
                return true;
            }
        }

        return false;
    }

    private void AddJournalEntry(
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        ClientPresentationEventType type,
        string assetName,
        float3 position,
        Entity visualEntity,
        Entity anchorEntity,
        bool isProjectile)
    {
        journal.Add(new ClientPredictedSkillVisualEventElement
        {
            Frame = request.Frame,
            RequestOrdinal = request.RequestOrdinal,
            EffectOrdinal = effectOrdinal,
            SkillId = request.Request.SkillId,
            Type = type,
            Source = request.Request.OriginEntity,
            VisualEntity = visualEntity,
            AnchorEntity = anchorEntity,
            AssetName = new FixedString128Bytes(assetName ?? string.Empty),
            Position = position,
            IsProjectile = isProjectile ? (byte)1 : (byte)0,
        });
    }

    private void TagVisual(
        Entity entity,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        int segmentOrdinal)
    {
        ClientPredictedSkillVisualComponent tag = new()
        {
            Frame = request.Frame,
            RequestOrdinal = request.RequestOrdinal,
            EffectOrdinal = effectOrdinal,
            SegmentOrdinal = segmentOrdinal,
        };
        if (EntityManager.HasComponent<ClientPredictedSkillVisualComponent>(entity))
            EntityManager.SetComponentData(entity, tag);
        else
            EntityManager.AddComponentData(entity, tag);
    }

    private static bool PassEffectConditions(EffectData effect, SkillContent context)
    {
        if (effect.Conditions == null || effect.Conditions.Count == 0)
            return true;

        Entity conditionEntity = context.HasTargetEntity &&
                                 context.TargetEntity != Entity.Null &&
                                 context.EntityManager.Exists(context.TargetEntity)
            ? context.TargetEntity
            : context.HasOriginEntity &&
              context.OriginEntity != Entity.Null &&
              context.EntityManager.Exists(context.OriginEntity)
                ? context.OriginEntity
                : Entity.Null;
        return conditionEntity != Entity.Null &&
               EffectConditionUtility.Pass(effect.Conditions, context, conditionEntity);
    }

    private bool TryGetOriginPosition(SkillContent context, out float3 position)
    {
        if (context.HasOriginPositionSnapshot)
        {
            position = new float3(
                context.OriginPositionSnapshot.x,
                context.OriginPositionSnapshot.y,
                context.OriginPositionSnapshot.z);
            return true;
        }

        if (context.HasOriginEntity &&
            context.OriginEntity != Entity.Null &&
            EntityManager.Exists(context.OriginEntity) &&
            EntityManager.HasComponent<LocalTransform>(context.OriginEntity))
        {
            position = EntityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
            return true;
        }

        position = float3.zero;
        return false;
    }

    private bool TryGetPersistentReleasePosition(SkillContent context, out float3 position)
    {
        if (context.HasPosition)
        {
            position = new float3(context.Position.x, context.Position.y, context.Position.z);
            return true;
        }

        if (context.HasTargetEntity &&
            context.TargetEntity != Entity.Null &&
            EntityManager.Exists(context.TargetEntity) &&
            EntityManager.HasComponent<LocalTransform>(context.TargetEntity))
        {
            position = EntityManager.GetComponentData<LocalTransform>(context.TargetEntity).Position;
            return true;
        }

        return TryGetOriginPosition(context, out position);
    }

    private float GetPredictionAgeSeconds(uint predictionFrame)
    {
        if (!FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame) ||
            frame.currentFrame < predictionFrame)
        {
            return 0f;
        }

        uint completedMovementFrames = frame.currentFrame - predictionFrame;
        if (completedMovementFrames > 0u)
            completedMovementFrames--;
        return completedMovementFrames * math.max(1, frame.frameInterval) / 1000f;
    }

    private static int ComposeEffectOrdinal(int path, int index)
    {
        unchecked
        {
            return path * 397 ^ index + 1;
        }
    }
}
