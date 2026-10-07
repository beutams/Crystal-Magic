using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

public sealed class NetworkStateApplyContext
{
    private static readonly ProfilerMarker BuildEntityMapMarker = new("Network.Receive.BuildEntityMap");
    private readonly Dictionary<Guid, Entity> _entities;
    private Entity _clientPresentationEntity;

    public EntityManager EntityManager { get; }
    public uint Frame { get; private set; }
    public int FrameInterval { get; private set; }
    public bool IsClient { get; }
    public double ApplyRealtime { get; private set; }

    public NetworkStateApplyContext(
        EntityManager entityManager,
        uint frame,
        int frameInterval,
        bool updateClientPresentationClock = true)
        : this(entityManager, new Dictionary<Guid, Entity>())
    {
        BeginFrame(frame, frameInterval, updateClientPresentationClock);
        using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkIdentityComponent>());
        RefreshEntityMap(query);
    }

    // FrameReceiveSystem owns this map and reuses its capacity across input frames.
    internal NetworkStateApplyContext(EntityManager entityManager, Dictionary<Guid, Entity> entities)
    {
        EntityManager = entityManager;
        _entities = entities;
        IsClient = GameWorldContextUtility.Get(entityManager).Role == GameWorldRole.Client;
    }

    internal void BeginFrame(uint frame, int frameInterval, bool updateClientPresentationClock = true)
    {
        Frame = frame;
        FrameInterval = Math.Max(1, frameInterval);
        ApplyRealtime = Time.realtimeSinceStartupAsDouble;

        if (IsClient && updateClientPresentationClock)
            UpdateClientPresentationClock();
    }

    internal void RefreshEntityMap(EntityQuery query)
    {
        using var profile = BuildEntityMapMarker.Auto();
        _entities.Clear();
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            Guid id = EntityManager.GetComponentData<NetworkIdentityComponent>(entity).id;
            if (id != Guid.Empty)
                _entities[id] = entity;
        }
    }

    public bool TryGetEntity(Guid unitId, out Entity entity)
    {
        if (unitId != Guid.Empty && _entities.TryGetValue(unitId, out entity) && EntityManager.Exists(entity) &&
            EntityManager.HasComponent<NetworkIdentityComponent>(entity) &&
            EntityManager.GetComponentData<NetworkIdentityComponent>(entity).id == unitId)
            return true;

        entity = Entity.Null;
        return false;
    }

    public void RegisterEntity(Guid unitId, Entity entity)
    {
        if (unitId != Guid.Empty && entity != Entity.Null && EntityManager.Exists(entity))
        {
            _entities[unitId] = entity;
        }
    }

    public void SetOrAdd<T>(Entity entity, T value) where T : unmanaged, IComponentData
    {
        if (EntityManager.HasComponent<T>(entity))
            EntityManager.SetComponentData(entity, value);
        else
            EntityManager.AddComponentData(entity, value);
    }

    public float GetRemainingSeconds(uint endFrame)
    {
        if (endFrame == uint.MaxValue)
            return -1f;

        if (endFrame <= Frame)
            return 0f;

        return (endFrame - Frame) * FrameInterval / 1000f;
    }

    public float GetElapsedSeconds(uint startFrame)
    {
        if (startFrame >= Frame)
            return 0f;

        return (Frame - startFrame) * FrameInterval / 1000f;
    }

    public Entity GetOrCreateClientPresentationEntity()
    {
        if (!IsClient)
            return Entity.Null;

        if (_clientPresentationEntity != Entity.Null && EntityManager.Exists(_clientPresentationEntity))
            return _clientPresentationEntity;

        EntityQuery query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ClientPresentationClockComponent>());
        bool isEmpty = query.IsEmptyIgnoreFilter;
        Entity existingEntity = isEmpty ? Entity.Null : query.GetSingletonEntity();
        query.Dispose();
        _clientPresentationEntity = isEmpty
            ? EntityManager.CreateEntity(typeof(ClientPresentationClockComponent))
            : existingEntity;
        return _clientPresentationEntity;
    }

    public void ApplyPosition(Entity entity, float3 position)
    {
        if (!EntityManager.HasComponent<LocalTransform>(entity))
        {
            EntityManager.AddComponentData(
                entity,
                LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
        }

        if (!IsClient)
        {
            LocalTransform authoritativeTransform = EntityManager.GetComponentData<LocalTransform>(entity);
            authoritativeTransform.Position = position;
            EntityManager.SetComponentData(entity, authoritativeTransform);
            return;
        }

        if (EntityManager.HasComponent<NetworkPlayerComponent>(entity))
        {
            ApplyLocalPlayerPosition(entity, position);
            return;
        }

        LocalTransform renderTransform = EntityManager.GetComponentData<LocalTransform>(entity);
        if (!EntityManager.HasComponent<ClientTransformInterpolationComponent>(entity))
        {
            renderTransform.Position = position;
            EntityManager.SetComponentData(entity, renderTransform);
            EntityManager.AddComponentData(entity, new ClientTransformInterpolationComponent
            {
                FromPosition = position,
                TargetPosition = position,
                FromFrame = Frame,
                TargetFrame = Frame,
                StartRealtime = ApplyRealtime,
                Duration = FrameInterval / 1000f,
                Initialized = 1,
            });
            return;
        }

        ClientTransformInterpolationComponent interpolation =
            EntityManager.GetComponentData<ClientTransformInterpolationComponent>(entity);
        interpolation.FromPosition = renderTransform.Position;
        interpolation.TargetPosition = position;
        interpolation.FromFrame = interpolation.TargetFrame;
        interpolation.TargetFrame = Frame;
        interpolation.StartRealtime = ApplyRealtime;
        interpolation.Duration = math.max(0.001f, FrameInterval / 1000f);
        interpolation.Initialized = 1;
        EntityManager.SetComponentData(entity, interpolation);
    }

    public void ApplyProjectilePosition(Entity entity, float3 position, bool finalPosition = false)
    {
        if (!IsClient)
        {
            ApplyPosition(entity, position);
            return;
        }

        if (!EntityManager.HasComponent<LocalTransform>(entity))
            EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
        // A ballistic visual must not also interpolate to a historical network position.
        if (EntityManager.HasComponent<ClientTransformInterpolationComponent>(entity))
            EntityManager.RemoveComponent<ClientTransformInterpolationComponent>(entity);
        LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
        bool hasPresentation = EntityManager.HasComponent<ClientProjectilePresentationComponent>(entity);
        if (finalPosition)
        {
            transform.Position = position;
            EntityManager.SetComponentData(entity, transform);
            if (hasPresentation)
                EntityManager.RemoveComponent<ClientProjectilePresentationComponent>(entity);
            return;
        }

        ClientProjectilePresentationComponent presentation = hasPresentation
            ? EntityManager.GetComponentData<ClientProjectilePresentationComponent>(entity) : default;
        if (presentation.Initialized == 0)
        {
            transform.Position = position;
            EntityManager.SetComponentData(entity, transform);
        }
        presentation.SnapshotPosition = position;
        presentation.SnapshotRealtime = ApplyRealtime;
        presentation.Initialized = 1;
        SetOrAdd(entity, presentation);
    }

    private void ApplyLocalPlayerPosition(Entity entity, float3 position)
    {
        LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
        if (!EntityManager.HasComponent<ClientPlayerMovePresentationComponent>(entity))
        {
            transform.Position = position;
            EntityManager.SetComponentData(entity, transform);
            EntityManager.AddComponentData(entity, new ClientPlayerMovePresentationComponent
            {
                CurrentPosition = position,
                PreviousPredictionPosition = position,
                LatestPredictionPosition = position,
                Initialized = 1,
            });
        }
        else
        {
            ClientPlayerMovePresentationComponent presentation =
                EntityManager.GetComponentData<ClientPlayerMovePresentationComponent>(entity);
            if (presentation.Initialized == 0)
            {
                presentation.CurrentPosition = transform.Position;
                presentation.Initialized = 1;
            }
            if (presentation.ReconciliationPending == 0)
            {
                UnitMoveComponent move = EntityManager.HasComponent<UnitMoveComponent>(entity)
                    ? EntityManager.GetComponentData<UnitMoveComponent>(entity) : default;
                presentation.ReconciliationFromPosition = move.HasPredictedPosition != 0
                    ? move.PredictedPosition : transform.Position;
                presentation.ReconciliationPending = 1;
            }
            EntityManager.SetComponentData(entity, presentation);
        }

        // 本地玩家由预测表现系统驱动，不能同时再吃远端实体插值。
        if (EntityManager.HasComponent<ClientTransformInterpolationComponent>(entity))
            EntityManager.RemoveComponent<ClientTransformInterpolationComponent>(entity);
    }

    private void UpdateClientPresentationClock()
    {
        Entity presentationEntity = GetOrCreateClientPresentationEntity();
        ClientPresentationClockComponent clock =
            EntityManager.GetComponentData<ClientPresentationClockComponent>(presentationEntity);
        clock.LatestAppliedFrame = Frame;
        clock.FrameIntervalSeconds = FrameInterval / 1000f;
        clock.AppliedAtRealtime = ApplyRealtime;
        clock.Revision++;
        EntityManager.SetComponentData(presentationEntity, clock);
    }
}
