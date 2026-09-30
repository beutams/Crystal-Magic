using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Shared circle sampling and optional navigation-wall validation for authored spawns.</summary>
public static class SpawnPositionUtility
{
    public static float3 Sample(float3 center, float minimumRadius, float radius, ref Random random)
    {
        radius = math.max(0f, radius);
        minimumRadius = math.clamp(minimumRadius, 0f, radius);
        if (radius == 0f) return center;
        float2 direction = random.NextFloat2Direction();
        float distance = math.sqrt(random.NextFloat(minimumRadius * minimumRadius, radius * radius));
        return center + new float3(direction * distance, 0f);
    }

    public static bool TrySample(EntityManager manager, float3 center, float minimumRadius, float radius,
        bool validate, float clearance, int attempts, ref Random random, out float3 position)
    {
        position = center;
        if (!math.all(math.isfinite(center)) || !math.isfinite(radius) || !math.isfinite(minimumRadius)) return false;
        if (!validate)
        {
            position = Sample(center, minimumRadius, radius, ref random);
            return true;
        }
        using EntityQuery query = manager.CreateEntityQuery(typeof(DungeonNavigationMapComponent), typeof(DungeonNavigationCollisionWord));
        if (query.CalculateEntityCount() != 1) return false;
        Entity mapEntity = query.GetSingletonEntity();
        var map = manager.GetComponentData<DungeonNavigationMapComponent>(mapEntity);
        var words = manager.GetBuffer<DungeonNavigationCollisionWord>(mapEntity, true).AsNativeArray();
        for (int i = 0; i < math.clamp(attempts, 1, 128); i++)
        {
            float3 candidate = Sample(center, minimumRadius, radius, ref random);
            if (!CanOccupy(in map, words, candidate.xy, clearance)) continue;
            position = candidate;
            return true;
        }
        return false;
    }

    public static bool IsValid(EntityManager manager, float3 position, float clearance)
    {
        using EntityQuery query = manager.CreateEntityQuery(typeof(DungeonNavigationMapComponent), typeof(DungeonNavigationCollisionWord));
        if (query.CalculateEntityCount() != 1 || !math.all(math.isfinite(position))) return false;
        Entity entity = query.GetSingletonEntity();
        var map = manager.GetComponentData<DungeonNavigationMapComponent>(entity);
        return CanOccupy(in map, manager.GetBuffer<DungeonNavigationCollisionWord>(entity, true).AsNativeArray(), position.xy, clearance);
    }

    // Prefer the requested point, then search a bounded deterministic spiral around it.
    // Failure never returns a blocked fallback. Safe to call from movement jobs.
    public static bool TryFindNearby(in DungeonNavigationMapComponent map,
        NativeArray<DungeonNavigationCollisionWord> words, float3 center, float clearance,
        float searchRadius, out float3 position)
    {
        position = center;
        if (!math.all(math.isfinite(center)) || !math.isfinite(searchRadius)) return false;
        if (CanOccupy(in map, words, center.xy, clearance)) return true;
        if (searchRadius <= 0f) return false;
        const int attempts = 64;
        for (int i = 1; i <= attempts; i++)
        {
            math.sincos(i * 2.39996323f, out float sin, out float cos);
            float radius = searchRadius * math.sqrt((float)i / attempts);
            float3 candidate = center + new float3(new float2(cos, sin) * radius, 0f);
            if (!CanOccupy(in map, words, candidate.xy, clearance)) continue;
            position = candidate;
            return true;
        }
        return false;
    }

    // Unlike WorldToCell, do not clamp an out-of-map position onto a valid edge cell.
    // Check the actual world position, not the cell center, so a body cannot overlap a wall.
    public static bool CanOccupy(in DungeonNavigationMapComponent map,
        NativeArray<DungeonNavigationCollisionWord> words, float2 position, float clearance)
    {
        if (map.Width <= 0 || map.Height <= 0 || !math.isfinite(map.CellSize) || map.CellSize <= 0f ||
            !math.all(math.isfinite(position)) || !math.isfinite(clearance)) return false;
        float radius = math.max(0f, clearance);
        float2 relative = position - map.WorldOrigin;
        float2 size = new float2(map.Width, map.Height) * map.CellSize;
        if (math.any(relative - radius < 0f) || math.any(relative + radius >= size)) return false;
        int2 cell = (int2)math.floor(relative / map.CellSize);
        if (DungeonNavigationMapUtility.IsBlocked(in map, words, cell)) return false;
        int2 first = (int2)math.floor((relative - radius) / map.CellSize);
        int2 last = (int2)math.floor((relative + radius) / map.CellSize);
        for (int y = first.y; y <= last.y; y++)
        for (int x = first.x; x <= last.x; x++)
        {
            if (!DungeonNavigationMapUtility.IsBlocked(in map, words, new int2(x, y))) continue;
            float2 min = new float2(x, y) * map.CellSize;
            float2 nearest = math.clamp(relative, min, min + map.CellSize);
            if (math.distancesq(relative, nearest) < radius * radius) return false;
        }
        return true;
    }
}
