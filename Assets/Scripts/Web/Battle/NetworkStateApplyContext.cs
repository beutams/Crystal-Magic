using System;
using System.Collections.Generic;
using Server;
using Unity.Collections;
using Unity.Entities;

public sealed class NetworkStateApplyContext
{
    private readonly Dictionary<Guid, Entity> _entities = new();

    public EntityManager EntityManager { get; }
    public uint Frame { get; }
    public int FrameInterval { get; }

    public NetworkStateApplyContext(EntityManager entityManager, uint frame, int frameInterval)
    {
        EntityManager = entityManager;
        Frame = frame;
        FrameInterval = Math.Max(1, frameInterval);

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkIdentityComponent>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            Guid id = entityManager.GetComponentData<NetworkIdentityComponent>(entity).id;
            if (id != Guid.Empty)
                _entities[id] = entity;
        }
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
}
