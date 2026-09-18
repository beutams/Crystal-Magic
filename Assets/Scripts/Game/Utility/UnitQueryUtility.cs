using System;
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
        EntityQuery singletonQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<UnitQuerySingleton>(),
            ComponentType.ReadOnly<UnitQueryRuntimeComponent>());

        try
        {
            if (singletonQuery.IsEmptyIgnoreFilter)
            {
                grid = null;
                return false;
            }

            Entity singletonEntity = singletonQuery.GetSingletonEntity();
            UnitQueryRuntimeComponent runtime = entityManager.GetComponentObject<UnitQueryRuntimeComponent>(singletonEntity);
            if (runtime == null)
            {
                grid = null;
                return false;
            }

            grid = gridKind == UnitQueryGridKind.Interactable
                ? runtime.InteractableGrid
                : runtime.UnitGrid;
            return grid != null && grid.IsCreated;
        }
        finally
        {
            singletonQuery.Dispose();
        }
    }
}

public sealed class UnitQueryGrid : IDisposable
{
    public const float DefaultCellSize = 4f;

    private static readonly UnitQueryHitComparer HitComparer = new();

    private NativeParallelMultiHashMap<long, UnitQueryHit> _entries;
    private readonly float _inverseCellSize;

    public bool IsCreated => _entries.IsCreated;
    public float InverseCellSize => _inverseCellSize;

    public UnitQueryGrid(int initialCapacity = 16, float cellSize = DefaultCellSize)
    {
        float resolvedCellSize = math.max(0.01f, cellSize);
        _inverseCellSize = 1f / resolvedCellSize;
        _entries = new NativeParallelMultiHashMap<long, UnitQueryHit>(
            math.max(1, initialCapacity),
            Allocator.Persistent);
    }

    public void PrepareForBuild(int entryCount)
    {
        if (!_entries.IsCreated)
            return;

        if (entryCount > _entries.Capacity)
            _entries.Capacity = math.max(entryCount, _entries.Capacity * 2);

        _entries.Clear();
    }

    public NativeParallelMultiHashMap<long, UnitQueryHit>.ParallelWriter AsParallelWriter()
    {
        return _entries.AsParallelWriter();
    }

    public NativeParallelMultiHashMap<long, UnitQueryHit>.ReadOnly AsReadOnly()
    {
        return _entries.AsReadOnly();
    }

    public void QueryCircle(float3 center, float radius, List<UnitQueryHit> results, bool reportDebug = true)
    {
        results.Clear();
        if (!_entries.IsCreated || radius <= 0f)
            return;

        float2 queryCenter = center.xy;
        float radiusSq = radius * radius;
        int2 minCell = GetCell(queryCenter - radius, _inverseCellSize);
        int2 maxCell = GetCell(queryCenter + radius, _inverseCellSize);

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
                AddCircleHits(GetCellKey(new int2(x, y)), queryCenter, radiusSq, results);
        }

        SortResults(results);
        if (reportDebug)
        {
            DebugQueryShapeReporter.ReportCircle(center, radius);
            ReportHits(center, results);
        }
    }

    public void QueryForwardRect(float3 origin, float2 forward, float length, float width, List<UnitQueryHit> results)
    {
        results.Clear();
        if (!_entries.IsCreated || length <= 0f || width <= 0f || math.lengthsq(forward) <= 0.0001f)
            return;

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

    public void QueryAxisAlignedRect(float3 center, float2 size, List<UnitQueryHit> results)
    {
        results.Clear();
        if (!_entries.IsCreated || math.any(size <= 0f))
            return;

        float2 halfSize = size * 0.5f;
        float2 rectMin = center.xy - halfSize;
        float2 rectMax = center.xy + halfSize;
        int2 minCell = GetCell(rectMin, _inverseCellSize);
        int2 maxCell = GetCell(rectMax, _inverseCellSize);

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
                AddAxisAlignedRectHits(GetCellKey(new int2(x, y)), rectMin, rectMax, results);
        }

        SortResults(results);
        DebugQueryShapeReporter.ReportForwardRect(
            new float3(rectMin.x, center.y, center.z),
            new float2(1f, 0f),
            size.x,
            size.y);
        ReportHits(center, results);
    }

    public void QueryCone(float3 origin, float2 forward, float radius, float angleDegrees, List<UnitQueryHit> results)
    {
        results.Clear();
        if (!_entries.IsCreated || radius <= 0f || angleDegrees <= 0f || math.lengthsq(forward) <= 0.0001f)
            return;

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

    public void Dispose()
    {
        if (_entries.IsCreated)
            _entries.Dispose();
    }

    public static int2 GetCell(float2 position, float inverseCellSize)
    {
        return (int2)math.floor(position * inverseCellSize);
    }

    public static long GetCellKey(int2 cell)
    {
        return ((long)cell.x << 32) | (uint)cell.y;
    }

    private void AddCircleHits(long cellKey, float2 center, float radiusSq, List<UnitQueryHit> results)
    {
        if (!_entries.TryGetFirstValue(cellKey, out UnitQueryHit hit, out NativeParallelMultiHashMapIterator<long> iterator))
            return;

        do
        {
            if (math.lengthsq(hit.Position.xy - center) <= radiusSq)
                results.Add(hit);
        } while (_entries.TryGetNextValue(out hit, ref iterator));
    }

    private void AddForwardRectHits(
        long cellKey,
        float2 origin,
        float2 normalizedForward,
        float2 right,
        float length,
        float halfWidth,
        List<UnitQueryHit> results)
    {
        if (!_entries.TryGetFirstValue(cellKey, out UnitQueryHit hit, out NativeParallelMultiHashMapIterator<long> iterator))
            return;

        do
        {
            float2 difference = hit.Position.xy - origin;
            float forwardDistance = math.dot(difference, normalizedForward);
            if (forwardDistance < 0f || forwardDistance > length)
                continue;

            float lateralDistance = math.abs(math.dot(difference, right));
            if (lateralDistance <= halfWidth)
                results.Add(hit);
        } while (_entries.TryGetNextValue(out hit, ref iterator));
    }

    private void AddAxisAlignedRectHits(long cellKey, float2 rectMin, float2 rectMax, List<UnitQueryHit> results)
    {
        if (!_entries.TryGetFirstValue(cellKey, out UnitQueryHit hit, out NativeParallelMultiHashMapIterator<long> iterator))
            return;

        do
        {
            float2 position = hit.Position.xy;
            if (!math.any(position < rectMin) && !math.any(position > rectMax))
                results.Add(hit);
        } while (_entries.TryGetNextValue(out hit, ref iterator));
    }

    private void AddConeHits(
        long cellKey,
        float2 origin,
        float2 normalizedForward,
        float radiusSq,
        float minDot,
        List<UnitQueryHit> results)
    {
        if (!_entries.TryGetFirstValue(cellKey, out UnitQueryHit hit, out NativeParallelMultiHashMapIterator<long> iterator))
            return;

        do
        {
            float2 difference = hit.Position.xy - origin;
            float distanceSq = math.lengthsq(difference);
            if (distanceSq > radiusSq)
                continue;

            if (distanceSq <= 0.0001f ||
                math.dot(normalizedForward, difference * math.rsqrt(distanceSq)) >= minDot)
            {
                results.Add(hit);
            }
        } while (_entries.TryGetNextValue(out hit, ref iterator));
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
