using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct UnitQueryHit
{
    public Entity Entity;
    public float3 Position;
}

public enum UnitQueryGridKind
{
    Unit,
    Interactable,
}

public static class UnitQueryUtility
{
    public static bool TryGetGrid(EntityManager entityManager, UnitQueryGridKind gridKind, out UnitQueryGrid grid)
    {
        using EntityQuery singletonQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<UnitQuerySingleton>());
        if (singletonQuery.IsEmptyIgnoreFilter)
        {
            grid = default;
            return false;
        }

        UnitQuerySingleton singleton =
            entityManager.GetComponentData<UnitQuerySingleton>(singletonQuery.GetSingletonEntity());
        Entity gridEntity = gridKind == UnitQueryGridKind.Interactable
            ? singleton.InteractableGridEntity
            : singleton.UnitGridEntity;
        if (gridEntity == Entity.Null ||
            !entityManager.Exists(gridEntity) ||
            !entityManager.HasBuffer<UnitQueryEntry>(gridEntity))
        {
            grid = default;
            return false;
        }

        grid = new UnitQueryGrid(
            entityManager.GetBuffer<UnitQueryEntry>(gridEntity, true),
            singleton.InverseCellSize);
        return grid.IsCreated;
    }
}

/// <summary>
/// Lightweight view over one sorted ECS query buffer.
/// </summary>
public struct UnitQueryGrid
{
    public const float DefaultCellSize = 4f;

    private static readonly UnitQueryHitComparer HitComparer = new();

    private DynamicBuffer<UnitQueryEntry> _entries;
    private float _inverseCellSize;

    public UnitQueryGrid(DynamicBuffer<UnitQueryEntry> entries, float inverseCellSize)
    {
        _entries = entries;
        _inverseCellSize = inverseCellSize;
    }

    public readonly bool IsCreated => _entries.IsCreated;
    public readonly float InverseCellSize => _inverseCellSize;

    public readonly NativeArray<UnitQueryEntry> AsNativeArray() => _entries.AsNativeArray();

    public readonly void QueryCircle(
        float3 center,
        float radius,
        List<UnitQueryHit> results,
        bool reportDebug = true)
    {
        results.Clear();
        if (!_entries.IsCreated || radius <= 0f)
            return;

        NativeArray<UnitQueryEntry> entries = _entries.AsNativeArray();
        float2 queryCenter = center.xy;
        float radiusSq = radius * radius;
        int2 minCell = GetCell(queryCenter - radius, _inverseCellSize);
        int2 maxCell = GetCell(queryCenter + radius, _inverseCellSize);

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
                AddCircleHits(entries, GetCellKey(new int2(x, y)), queryCenter, radiusSq, results);
        }

        SortResults(results);
        if (reportDebug)
        {
            DebugQueryShapeReporter.ReportCircle(center, radius);
            ReportHits(center, results);
        }
    }

    public readonly void QueryForwardRect(
        float3 origin,
        float2 forward,
        float length,
        float width,
        List<UnitQueryHit> results)
    {
        results.Clear();
        if (!_entries.IsCreated || length <= 0f || width <= 0f || math.lengthsq(forward) <= 0.0001f)
            return;

        NativeArray<UnitQueryEntry> entries = _entries.AsNativeArray();
        float2 normalizedForward = math.normalize(forward);
        float2 right = new(-normalizedForward.y, normalizedForward.x);
        float halfWidth = width * 0.5f;
        float2 start = origin.xy;
        float2 end = origin.xy + normalizedForward * length;

        float2 corner0 = start - right * halfWidth;
        float2 corner1 = start + right * halfWidth;
        float2 corner2 = end - right * halfWidth;
        float2 corner3 = end + right * halfWidth;
        float2 rectMin = math.min(math.min(corner0, corner1), math.min(corner2, corner3));
        float2 rectMax = math.max(math.max(corner0, corner1), math.max(corner2, corner3));
        int2 minCell = GetCell(rectMin, _inverseCellSize);
        int2 maxCell = GetCell(rectMax, _inverseCellSize);

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                AddForwardRectHits(
                    entries,
                    GetCellKey(new int2(x, y)),
                    origin.xy,
                    normalizedForward,
                    right,
                    length,
                    halfWidth,
                    results);
            }
        }

        SortResults(results);
        DebugQueryShapeReporter.ReportForwardRect(origin, normalizedForward, length, width);
        ReportHits(origin, results);
    }

    public readonly void QueryAxisAlignedRect(float3 center, float2 size, List<UnitQueryHit> results)
    {
        results.Clear();
        if (!_entries.IsCreated || math.any(size <= 0f))
            return;

        NativeArray<UnitQueryEntry> entries = _entries.AsNativeArray();
        float2 halfSize = size * 0.5f;
        float2 rectMin = center.xy - halfSize;
        float2 rectMax = center.xy + halfSize;
        int2 minCell = GetCell(rectMin, _inverseCellSize);
        int2 maxCell = GetCell(rectMax, _inverseCellSize);

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                AddAxisAlignedRectHits(
                    entries,
                    GetCellKey(new int2(x, y)),
                    rectMin,
                    rectMax,
                    results);
            }
        }

        SortResults(results);
        DebugQueryShapeReporter.ReportForwardRect(
            new float3(rectMin.x, center.y, center.z),
            new float2(1f, 0f),
            size.x,
            size.y);
        ReportHits(center, results);
    }

    public readonly void QueryCone(
        float3 origin,
        float2 forward,
        float radius,
        float angleDegrees,
        List<UnitQueryHit> results)
    {
        results.Clear();
        if (!_entries.IsCreated || radius <= 0f || angleDegrees <= 0f || math.lengthsq(forward) <= 0.0001f)
            return;

        NativeArray<UnitQueryEntry> entries = _entries.AsNativeArray();
        float2 normalizedForward = math.normalize(forward);
        float radiusSq = radius * radius;
        float minDot = math.cos(math.radians(math.clamp(angleDegrees, 0f, 360f) * 0.5f));
        int2 minCell = GetCell(origin.xy - radius, _inverseCellSize);
        int2 maxCell = GetCell(origin.xy + radius, _inverseCellSize);

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                AddConeHits(
                    entries,
                    GetCellKey(new int2(x, y)),
                    origin.xy,
                    normalizedForward,
                    radiusSq,
                    minDot,
                    results);
            }
        }

        SortResults(results);
        DebugQueryShapeReporter.ReportCone(origin, normalizedForward, radius, angleDegrees);
        ReportHits(origin, results);
    }

    public static int2 GetCell(float2 position, float inverseCellSize)
    {
        return (int2)math.floor(position * inverseCellSize);
    }

    public static long GetCellKey(int2 cell)
    {
        return ((long)cell.x << 32) | (uint)cell.y;
    }

    public static bool TryGetCellRange(
        NativeArray<UnitQueryEntry> entries,
        long cellKey,
        out int startIndex,
        out int endIndex)
    {
        int low = 0;
        int high = entries.Length;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (entries[middle].CellKey < cellKey)
                low = middle + 1;
            else
                high = middle;
        }

        startIndex = low;
        if (startIndex >= entries.Length || entries[startIndex].CellKey != cellKey)
        {
            endIndex = startIndex;
            return false;
        }

        low = startIndex;
        high = entries.Length;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (entries[middle].CellKey <= cellKey)
                low = middle + 1;
            else
                high = middle;
        }

        endIndex = low;
        return true;
    }

    private static void AddCircleHits(
        NativeArray<UnitQueryEntry> entries,
        long cellKey,
        float2 center,
        float radiusSq,
        List<UnitQueryHit> results)
    {
        if (!TryGetCellRange(entries, cellKey, out int startIndex, out int endIndex))
            return;

        for (int index = startIndex; index < endIndex; index++)
        {
            UnitQueryEntry entry = entries[index];
            if (math.lengthsq(entry.Position.xy - center) <= radiusSq)
                results.Add(new UnitQueryHit { Entity = entry.Entity, Position = entry.Position });
        }
    }

    private static void AddForwardRectHits(
        NativeArray<UnitQueryEntry> entries,
        long cellKey,
        float2 origin,
        float2 normalizedForward,
        float2 right,
        float length,
        float halfWidth,
        List<UnitQueryHit> results)
    {
        if (!TryGetCellRange(entries, cellKey, out int startIndex, out int endIndex))
            return;

        for (int index = startIndex; index < endIndex; index++)
        {
            UnitQueryEntry entry = entries[index];
            float2 difference = entry.Position.xy - origin;
            float forwardDistance = math.dot(difference, normalizedForward);
            if (forwardDistance < 0f || forwardDistance > length)
                continue;

            if (math.abs(math.dot(difference, right)) <= halfWidth)
                results.Add(new UnitQueryHit { Entity = entry.Entity, Position = entry.Position });
        }
    }

    private static void AddAxisAlignedRectHits(
        NativeArray<UnitQueryEntry> entries,
        long cellKey,
        float2 rectMin,
        float2 rectMax,
        List<UnitQueryHit> results)
    {
        if (!TryGetCellRange(entries, cellKey, out int startIndex, out int endIndex))
            return;

        for (int index = startIndex; index < endIndex; index++)
        {
            UnitQueryEntry entry = entries[index];
            float2 position = entry.Position.xy;
            if (!math.any(position < rectMin) && !math.any(position > rectMax))
                results.Add(new UnitQueryHit { Entity = entry.Entity, Position = entry.Position });
        }
    }

    private static void AddConeHits(
        NativeArray<UnitQueryEntry> entries,
        long cellKey,
        float2 origin,
        float2 normalizedForward,
        float radiusSq,
        float minDot,
        List<UnitQueryHit> results)
    {
        if (!TryGetCellRange(entries, cellKey, out int startIndex, out int endIndex))
            return;

        for (int index = startIndex; index < endIndex; index++)
        {
            UnitQueryEntry entry = entries[index];
            float2 difference = entry.Position.xy - origin;
            float distanceSq = math.lengthsq(difference);
            if (distanceSq > radiusSq)
                continue;

            if (distanceSq <= 0.0001f ||
                math.dot(normalizedForward, difference * math.rsqrt(distanceSq)) >= minDot)
            {
                results.Add(new UnitQueryHit { Entity = entry.Entity, Position = entry.Position });
            }
        }
    }

    private static void SortResults(List<UnitQueryHit> results)
    {
        if (results.Count > 1)
            results.Sort(HitComparer);
    }

    private static void ReportHits(float3 origin, List<UnitQueryHit> results)
    {
        for (int i = 0; i < results.Count; i++)
            DebugQueryShapeReporter.ReportHit(origin, results[i].Position);
    }

    private sealed class UnitQueryHitComparer : IComparer<UnitQueryHit>
    {
        public int Compare(UnitQueryHit left, UnitQueryHit right)
        {
            int indexComparison = left.Entity.Index.CompareTo(right.Entity.Index);
            return indexComparison != 0
                ? indexComparison
                : left.Entity.Version.CompareTo(right.Entity.Version);
        }
    }
}
