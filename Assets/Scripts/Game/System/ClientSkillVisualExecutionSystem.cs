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
    private SkillEffectIdentity _executingIdentity;

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
        if (runtime.HasPendingRollback != 0)
        {
            CleanupRollback(runtime.RollbackFrame, runtimeEntity);
            runtime.HasPendingRollback = 0;
            EntityManager.SetComponentData(runtimeEntity, runtime);
        }

        DynamicBuffer<ClientSkillVisualRequestElement> requestBuffer =
            EntityManager.GetBuffer<ClientSkillVisualRequestElement>(runtimeEntity);
        using NativeArray<ClientSkillVisualRequestElement> requests =
            requestBuffer.ToNativeArray(Allocator.Temp);
        requestBuffer.Clear();
        for (int index = 0; index < requests.Length; index++)
            ExecuteRequest(requests[index], runtimeEntity);

        if (FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame))
            TrimJournal(frame.currentFrame, EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity));
    }

    private void ExecuteRequest(
        in ClientSkillVisualRequestElement visualRequest,
        Entity runtimeEntity)
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

        SkillContent context = SkillContentReferencePool.Get();
        try
        {
            context.EntityManager = EntityManager;
            context.TriggerSource = SkillTriggerSource.ActiveCast;
            context.HookType = SkillHookType.None;
            context.HasOriginEntity = request.OriginEntity != Entity.Null;
            context.OriginEntity = request.OriginEntity;
            context.HasOriginPositionSnapshot = true;
            context.OriginPositionSnapshot = new Vector3(
                request.OriginPosition.x,
                request.OriginPosition.y,
                request.OriginPosition.z);
            context.SourceSkillId = request.SkillId;
            context.EffectIdentity = SkillEffectIdentity.Create(EntityManager, request.OriginEntity,
                visualRequest.Frame, visualRequest.RequestOrdinal);
            context.HasTargetEntity = request.HasTargetEntity;
            context.TargetEntity = request.TargetEntity;
            context.HasPosition = request.HasTargetPosition;
            context.Position = new Vector3(
                request.TargetPosition.x,
                request.TargetPosition.y,
                request.TargetPosition.z);
            SkillExecutor.ExecuteEffects(resolvedSkill.EffectChain, context);
        }
        finally
        {
            SkillContentReferencePool.Return(context);
        }
    }

    // Presentation is the client leaf of the common effect graph. Searches and
    // gameplay-safe containers continue through SkillExecutor on both peers.
    public bool TryExecutePresentation(EffectData effect, SkillContent context)
    {
        Entity runtimeEntity = _runtimeQuery.GetSingletonEntity();
        SkillEffectIdentity identity = context.EffectIdentity;
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal =
            EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity);
        for (int index = 0; index < journal.Length; index++)
            if (journal[index].Identity.Equals(identity)) return true;
        ClientSkillVisualRequestElement request = new()
        {
            Frame = identity.CastFrame, RequestOrdinal = identity.CastOrdinal,
            Request = new SkillReleaseRequest { SkillId = context.SourceSkillId, OriginEntity = context.OriginEntity },
        };
        int ordinal = unchecked((int)identity.Path);
        SkillEffectIdentity previous = _executingIdentity;
        _executingIdentity = identity;
        try
        {
            switch (effect)
            {
                case SpawnVfxEffectData data: SpawnVfx(data, context, request, ordinal, runtimeEntity); return true;
                case SpawnFollowVfxEffectData data: SpawnFollowVfx(data, context, request, ordinal, runtimeEntity); return true;
                case SpawnLineVfxEffectData data: SpawnLineVfx(data, context, request, ordinal, runtimeEntity); return true;
                case MoveVfxEffectData data: SpawnMoveVfx(data, context, request, ordinal, runtimeEntity); return true;
                case SpawnProjectileEffectData data: new SpawnProjectileEffect(data).Execute(context); return true;
                case SpawnSoundEffectData data:
                    new SpawnSoundEffect(data).Execute(context);
                    AddJournalEntry(runtimeEntity, request, ordinal, ClientPresentationEventType.Sound,
                        data.AudioPath, context.Position, Entity.Null, Entity.Null, false);
                    return true;
                case CameraShakeEffectData data:
                    new CameraShakeEffect(data).Execute(context);
                    AddJournalEntry(runtimeEntity, request, ordinal, ClientPresentationEventType.CameraShake,
                        string.Empty, context.Position, Entity.Null, Entity.Null, false);
                    return true;
                case PersistentEffectData data:
                    // Random placements need an authoritative choice, as before.
                    if (data.PlacementCount > 1 || data.PlacementRadius > 0f) return true;
                    if (TryGetPersistentReleasePosition(context, out float3 position) &&
                        (!data.ValidatePlacementPosition || SpawnPositionUtility.IsValid(EntityManager, position, data.PlacementClearanceRadius)))
                    {
                        SkillContent child = SkillContentReferencePool.Get(context);
                        try
                        {
                            child.HasPosition = true; child.Position = position;
                            child.HasTargetEntity = false; child.TargetEntity = Entity.Null;
                            SkillExecutor.ExecuteEffects(data.OnStartEffects, child);
                        }
                        finally { SkillContentReferencePool.Return(child); }
                    }
                    return true;
                default: return false;
            }
        }
        finally { _executingIdentity = previous; }
    }

    public void RecordProjectile(SkillContent context, SpawnProjectileEffectData data, Entity anchor, Entity visual)
    {
        SkillEffectIdentity previous = _executingIdentity;
        _executingIdentity = context.EffectIdentity;
        try
        {
            ClientSkillVisualRequestElement request = new()
            {
                Frame = context.EffectIdentity.CastFrame, RequestOrdinal = context.EffectIdentity.CastOrdinal,
                Request = new SkillReleaseRequest { SkillId = context.SourceSkillId, OriginEntity = context.OriginEntity },
            };
            int ordinal = unchecked((int)context.EffectIdentity.Path);
            TagVisual(anchor, request, ordinal, 0);
            if (visual != Entity.Null) TagVisual(visual, request, ordinal, 1);
            AddJournalEntry(_runtimeQuery.GetSingletonEntity(), request, ordinal,
                ClientPresentationEventType.SpawnFollowVfx, data.VisualPrefabName,
                EntityManager.GetComponentData<LocalTransform>(anchor).Position, visual, anchor, true);
        }
        finally { _executingIdentity = previous; }
    }

    private void SpawnVfx(
        SpawnVfxEffectData data,
        SkillContent context,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        Entity runtimeEntity)
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
            runtimeEntity,
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
        Entity runtimeEntity)
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
            runtimeEntity,
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
        Entity runtimeEntity)
    {
        if (string.IsNullOrWhiteSpace(data.VfxPrefabName) ||
            !context.HasPosition ||
            !SpriteEffectSpawnUtility.TryGetOriginPosition(context, out float3 originPosition))
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
            runtimeEntity,
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
        Entity runtimeEntity)
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
        float duration = math.max(0f, data.Duration);
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
                DestroyOnArrival = 1,
            });
        TagVisual(visualEntity, request, effectOrdinal, 0);
        AddJournalEntry(
            runtimeEntity,
            request,
            effectOrdinal,
            ClientPresentationEventType.MoveVfx,
            data.VfxPrefabName,
            startPosition,
            visualEntity,
            Entity.Null,
            false);
    }

    private void CleanupRollback(
        uint rollbackFrame,
        Entity runtimeEntity)
    {
        using NativeArray<ClientPredictedSkillVisualEventElement> snapshot =
            EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity).ToNativeArray(Allocator.Temp);
        using EntityQuery query = EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ClientPredictedSkillVisualComponent>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (!EntityManager.Exists(entity)) continue;
            ClientPredictedSkillVisualComponent tag = EntityManager.GetComponentData<ClientPredictedSkillVisualComponent>(entity);
            if (tag.Frame > rollbackFrame && !IsConfirmedVisual(snapshot, tag))
            {
                if (EntityManager.HasComponent<ClientPredictedProjectileComponent>(entity))
                    ClientProjectilePredictionUtility.Release(EntityManager, entity);
                else EntityManager.DestroyEntity(entity);
            }
        }

        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal =
            EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity);
        for (int index = journal.Length - 1; index >= 0; index--)
        {
            if (journal[index].Frame > rollbackFrame && journal[index].IsConfirmed == 0)
                journal.RemoveAt(index);
        }
    }

    private static bool IsConfirmedVisual(
        NativeArray<ClientPredictedSkillVisualEventElement> journal,
        in ClientPredictedSkillVisualComponent tag)
    {
        for (int index = 0; index < journal.Length; index++)
        {
            ClientPredictedSkillVisualEventElement entry = journal[index];
            if (entry.IsConfirmed != 0 && entry.Identity.Equals(tag.Identity))
                return true;
        }
        return false;
    }

    private static void TrimJournal(
        uint currentFrame,
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal)
    {
        for (int index = journal.Length - 1; index >= 0; index--)
        {
            uint eventFrame = journal[index].Frame;
            if (currentFrame >= eventFrame && currentFrame - eventFrame > JournalLifetimeFrames &&
                (journal[index].AnchorEntity == Entity.Null || journal[index].IsProjectile == 0))
                journal.RemoveAt(index);
        }
    }

    private void AddJournalEntry(
        Entity runtimeEntity,
        in ClientSkillVisualRequestElement request,
        int effectOrdinal,
        ClientPresentationEventType type,
        string assetName,
        float3 position,
        Entity visualEntity,
        Entity anchorEntity,
        bool isProjectile)
    {
        // Instantiation and tagging have invalidated any buffer acquired by the caller.
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal =
            EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtimeEntity);
        journal.Add(new ClientPredictedSkillVisualEventElement
        {
            Identity = _executingIdentity,
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
            Identity = _executingIdentity,
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

        return SpriteEffectSpawnUtility.TryGetOriginPosition(context, out position);
    }

}
