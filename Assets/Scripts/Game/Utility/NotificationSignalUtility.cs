using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Authoritative UI signals. Payload is captured at the gameplay change, never re-read by the UI.</summary>
public static class NotificationSignalUtility
{
    public static bool Publish(EntityManager manager, Entity source, string key, float3 values)
    {
        if (string.IsNullOrWhiteSpace(key) || !math.all(math.isfinite(values)) ||
            GameWorldContextUtility.Get(manager).Role == GameWorldRole.Client)
            return false;
        int scope = ResolveScope(manager, source);
        if (NetworkPresentationEventUtility.TryEnqueueNotification(manager, key, values, scope)) return true;
        EventComponent.Instance.Publish(new NotificationSignalEvent(key, values, scope));
        return true;
    }

    public static bool PublishSpawned(EntityManager manager, Entity source, string key, int spawnedCount)
    {
        if (spawnedCount <= 0 || string.IsNullOrWhiteSpace(key)) return false;
        return Publish(manager, source, key, new float3(0, spawnedCount, 0));
    }

    public static int ResolveScope(EntityManager manager, Entity source)
    {
        for (int depth = 0; depth < 16 && source != Entity.Null && manager.Exists(source); depth++)
        {
            if (manager.HasComponent<DungeonFloorControllerComponent>(source))
                return (int)math.max(1u, math.hash(new uint2((uint)source.Index, (uint)source.Version)));
            Entity other = UnitVariableSource.GetOther(manager, source);
            if (other == source) break;
            source = other;
        }
        return 0;
    }
}
