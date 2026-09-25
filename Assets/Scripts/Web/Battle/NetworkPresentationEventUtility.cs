using System;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

public static class NetworkPresentationEventUtility
{
    public static bool TryEnqueueDamage(
        EntityManager entityManager,
        Entity target,
        float3 position,
        float amount,
        bool isLethal)
    {
        return TryEnqueue(entityManager, new NetworkPresentationEventStateData
        {
            eventType = ClientPresentationEventType.Damage,
            targetUnitId = GetNetworkId(entityManager, target),
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            valueA = amount,
            flagA = isLethal ? (byte)1 : (byte)0,
        });
    }

    public static bool TryEnqueueVfx(
        EntityManager entityManager,
        string prefabName,
        float3 position,
        quaternion rotation,
        float scale,
        float duration,
        bool preservePrefabRotation = false,
        Entity source = default,
        int sourceSkillId = -1)
    {
        return TryEnqueue(entityManager, CreateVfxEvent(
            entityManager,
            ClientPresentationEventType.SpawnVfx,
            prefabName,
            position,
            rotation,
            scale,
            duration,
            preservePrefabRotation,
            source,
            sourceSkillId));
    }

    public static bool TryEnqueueFollowVfx(
        EntityManager entityManager,
        string prefabName,
        Entity target,
        float3 position,
        float3 offset,
        quaternion rotation,
        float scale,
        float duration,
        bool alignRotation,
        Entity source = default,
        int sourceSkillId = -1)
    {
        NetworkPresentationEventStateData state = CreateVfxEvent(
            entityManager,
            ClientPresentationEventType.SpawnFollowVfx,
            prefabName,
            position,
            rotation,
            scale,
            duration,
            false,
            source,
            sourceSkillId);
        state.targetUnitId = GetNetworkId(entityManager, target);
        state.secondaryX = offset.x;
        state.secondaryY = offset.y;
        state.secondaryZ = offset.z;
        state.flagA = alignRotation ? (byte)1 : (byte)0;
        return TryEnqueue(entityManager, state);
    }

    public static bool TryEnqueueLineVfx(
        EntityManager entityManager,
        string prefabName,
        float3 firstPosition,
        float2 direction,
        quaternion rotation,
        float length,
        float spacing,
        float scale,
        float duration,
        bool alignRotation,
        Entity source = default,
        int sourceSkillId = -1)
    {
        NetworkPresentationEventStateData state = CreateVfxEvent(
            entityManager,
            ClientPresentationEventType.SpawnLineVfx,
            prefabName,
            firstPosition,
            rotation,
            scale,
            duration,
            !alignRotation,
            source,
            sourceSkillId);
        state.secondaryX = direction.x;
        state.secondaryY = direction.y;
        state.valueA = length;
        state.valueB = spacing;
        state.flagA = alignRotation ? (byte)1 : (byte)0;
        return TryEnqueue(entityManager, state);
    }

    public static bool TryEnqueueMoveVfx(
        EntityManager entityManager,
        string prefabName,
        float3 startPosition,
        float3 endPosition,
        float scale,
        float duration,
        bool preservePrefabRotation,
        Entity source = default,
        int sourceSkillId = -1)
    {
        NetworkPresentationEventStateData state = CreateVfxEvent(
            entityManager,
            ClientPresentationEventType.MoveVfx,
            prefabName,
            startPosition,
            quaternion.identity,
            scale,
            duration,
            preservePrefabRotation,
            source,
            sourceSkillId);
        state.secondaryX = endPosition.x;
        state.secondaryY = endPosition.y;
        state.secondaryZ = endPosition.z;
        return TryEnqueue(entityManager, state);
    }

    public static bool TryEnqueueSound(
        EntityManager entityManager,
        string assetPath,
        int channel,
        Entity source,
        float3 position,
        float volume,
        float pitch,
        float spatialBlend,
        float delaySeconds,
        bool followSource)
    {
        return TryEnqueue(entityManager, new NetworkPresentationEventStateData
        {
            eventType = ClientPresentationEventType.Sound,
            sourceUnitId = GetNetworkId(entityManager, source),
            assetName = assetPath,
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            valueA = volume,
            valueB = pitch,
            valueC = spatialBlend,
            duration = delaySeconds,
            intValue = channel,
            flagA = followSource ? (byte)1 : (byte)0,
        });
    }

    public static bool TryEnqueueCameraShake(
        EntityManager entityManager,
        float3 position,
        float duration,
        float amplitude,
        float frequency,
        bool useDistanceAttenuation,
        float radius)
    {
        return TryEnqueue(entityManager, new NetworkPresentationEventStateData
        {
            eventType = ClientPresentationEventType.CameraShake,
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            duration = duration,
            valueA = amplitude,
            valueB = frequency,
            valueC = radius,
            flagA = useDistanceAttenuation ? (byte)1 : (byte)0,
        });
    }

    public static bool TryEnqueuePickupFeedback(
        EntityManager entityManager,
        Entity player,
        PickupFeedbackType type,
        int itemId,
        int amount)
    {
        return TryEnqueue(entityManager, new NetworkPresentationEventStateData
        {
            eventType = ClientPresentationEventType.PickupFeedback,
            targetUnitId = GetNetworkId(entityManager, player),
            intValue = itemId,
            valueA = amount,
            flagA = (byte)type,
        });
    }

    private static NetworkPresentationEventStateData CreateVfxEvent(
        EntityManager entityManager,
        ClientPresentationEventType type,
        string prefabName,
        float3 position,
        quaternion rotation,
        float scale,
        float duration,
        bool preservePrefabRotation,
        Entity source,
        int sourceSkillId)
    {
        return new NetworkPresentationEventStateData
        {
            eventType = type,
            sourceUnitId = GetNetworkId(entityManager, source),
            sourceSkillId = sourceSkillId,
            assetName = prefabName,
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            rotationX = rotation.value.x,
            rotationY = rotation.value.y,
            rotationZ = rotation.value.z,
            rotationW = rotation.value.w,
            scale = scale,
            duration = duration,
            flagB = preservePrefabRotation ? (byte)1 : (byte)0,
        };
    }

    private static bool TryEnqueue(EntityManager entityManager, NetworkPresentationEventStateData state)
    {
        if (GameWorldContextUtility.Get(entityManager).Role != GameWorldRole.Server)
        {
            return false;
        }

        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<NetworkPresentationEventQueueComponent>());
        NetworkPresentationEventQueueComponent queue;
        if (query.IsEmptyIgnoreFilter)
        {
            Entity queueEntity = entityManager.CreateEntity();
            queue = new NetworkPresentationEventQueueComponent();
            entityManager.AddComponentObject(queueEntity, queue);
        }
        else
        {
            queue = entityManager.GetComponentObject<NetworkPresentationEventQueueComponent>(
                query.GetSingletonEntity());
        }
        query.Dispose();

        queue.NextSequence++;
        if (queue.NextSequence == 0u)
            queue.NextSequence = 1u;
        state.sequence = queue.NextSequence;
        queue.Events.Add(state);
        return true;
    }

    private static Guid GetNetworkId(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity) ||
            !entityManager.HasComponent<NetworkIdentityComponent>(entity))
        {
            return Guid.Empty;
        }

        return entityManager.GetComponentData<NetworkIdentityComponent>(entity).id;
    }
}
