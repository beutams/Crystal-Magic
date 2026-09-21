using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed class NetworkStateApplyContext
{
    private readonly Dictionary<Guid, Entity> _entities = new();
    private Entity _clientPresentationEntity;

    public EntityManager EntityManager { get; }
    public uint Frame { get; }
    public int FrameInterval { get; }
    public bool IsClient { get; }
    public double ApplyRealtime { get; }

    public NetworkStateApplyContext(EntityManager entityManager, uint frame, int frameInterval)
    {
        EntityManager = entityManager;
        Frame = frame;
        FrameInterval = Math.Max(1, frameInterval);
        ApplyRealtime = Time.realtimeSinceStartupAsDouble;
        IsClient = GameWorldContextUtility.Get(entityManager).Role == GameWorldRole.Client;

        if (IsClient)
            UpdateClientPresentationClock();

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkIdentityComponent>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            Guid id = entityManager.GetComponentData<NetworkIdentityComponent>(entity).id;
            if (id != Guid.Empty)
                _entities[id] = entity;
        }
        query.Dispose();
    }

    public bool TryGetEntity(Guid unitId, out Entity entity)
    {
        if (unitId != Guid.Empty && _entities.TryGetValue(unitId, out entity) && EntityManager.Exists(entity))
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
