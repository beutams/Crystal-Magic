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
    public UnitFactionType Faction;
}

[Flags]
public enum UnitFactionMask : byte
{
    None = 0,
    Player = 1 << (int)UnitFactionType.Player,
    Friend = 1 << (int)UnitFactionType.Friend,
    Enemy = 1 << (int)UnitFactionType.Enemy,
    Boss = 1 << (int)UnitFactionType.Boss,
    Interactable = 1 << (int)UnitFactionType.Interactable,
    Combatants = Player | Friend | Enemy | Boss,
    All = Combatants | Interactable,
}

public enum UnitQueryShapeType : byte
{
    WholeWorld,
    Circle,
    AxisAlignedRect,
    ForwardRect,
    Cone,
}

/// <summary>
/// Unmanaged description of a spatial query. The same value can be used by managed code or Burst jobs.
/// </summary>
public struct UnitQueryShape
{
    public UnitQueryShapeType Type;
    public float2 Origin;
    public float2 Direction;
    public float2 Size;
    public float Radius;
    public float AngleDegrees;

    public static UnitQueryShape Circle(float3 center, float radius)
    {
        return new UnitQueryShape
        {
            Type = UnitQueryShapeType.Circle,
            Origin = center.xy,
            Radius = math.max(0f, radius),
        };
    }

    public static UnitQueryShape AxisAlignedRect(float3 center, float2 size)
    {
        return new UnitQueryShape
        {
            Type = UnitQueryShapeType.AxisAlignedRect,
            Origin = center.xy,
            Size = math.max(float2.zero, size),
        };
    }

    public static UnitQueryShape ForwardRect(float3 origin, float2 forward, float length, float width)
    {
        return new UnitQueryShape
        {
            Type = UnitQueryShapeType.ForwardRect,
            Origin = origin.xy,
            Direction = math.normalizesafe(forward),
            Size = new float2(math.max(0f, length), math.max(0f, width)),
        };
    }

    public static UnitQueryShape Cone(float3 origin, float2 forward, float radius, float angleDegrees)
    {
        return new UnitQueryShape
        {
            Type = UnitQueryShapeType.Cone,
            Origin = origin.xy,
            Direction = math.normalizesafe(forward),
            Radius = math.max(0f, radius),
            AngleDegrees = math.clamp(angleDegrees, 0f, 360f),
        };
    }

    public readonly bool TryGetBounds(out float2 minimum, out float2 maximum)
    {
        switch (Type)
        {
            case UnitQueryShapeType.WholeWorld:
                minimum = new float2(float.MinValue);
                maximum = new float2(float.MaxValue);
                return true;

            case UnitQueryShapeType.Circle:
                if (Radius <= 0f)
                    break;
                minimum = Origin - Radius;
                maximum = Origin + Radius;
                return true;

            case UnitQueryShapeType.AxisAlignedRect:
                if (math.any(Size <= 0f))
                    break;
                float2 halfSize = Size * 0.5f;
                minimum = Origin - halfSize;
                maximum = Origin + halfSize;
                return true;

            case UnitQueryShapeType.ForwardRect:
                if (math.any(Size <= 0f) || math.lengthsq(Direction) <= 0.0001f)
                    break;
                GetForwardRectBounds(out minimum, out maximum);
                return true;

            case UnitQueryShapeType.Cone:
                if (Radius <= 0f || AngleDegrees <= 0f || math.lengthsq(Direction) <= 0.0001f)
                    break;
                minimum = Origin - Radius;
                maximum = Origin + Radius;
                return true;
        }

        minimum = default;
        maximum = default;
        return false;
    }

    public readonly bool Contains(float2 position)
    {
        switch (Type)
        {
            case UnitQueryShapeType.WholeWorld:
                return true;

            case UnitQueryShapeType.Circle:
                return math.lengthsq(position - Origin) <= Radius * Radius;

            case UnitQueryShapeType.AxisAlignedRect:
                float2 halfSize = Size * 0.5f;
                return !math.any(position < Origin - halfSize) &&
                       !math.any(position > Origin + halfSize);

            case UnitQueryShapeType.ForwardRect:
                float2 difference = position - Origin;
                float forwardDistance = math.dot(difference, Direction);
                float2 right = new(-Direction.y, Direction.x);
                return forwardDistance >= 0f &&
                       forwardDistance <= Size.x &&
                       math.abs(math.dot(difference, right)) <= Size.y * 0.5f;

            case UnitQueryShapeType.Cone:
                float2 coneDifference = position - Origin;
                float distanceSq = math.lengthsq(coneDifference);
                if (distanceSq > Radius * Radius)
                    return false;
                if (distanceSq <= 0.0001f)
                    return true;
                float minimumDot = math.cos(math.radians(AngleDegrees * 0.5f));
                return math.dot(Direction, coneDifference * math.rsqrt(distanceSq)) >= minimumDot;
        }

        return false;
    }

    private readonly void GetForwardRectBounds(out float2 minimum, out float2 maximum)
    {
        float2 right = new(-Direction.y, Direction.x);
        float halfWidth = Size.y * 0.5f;
        float2 end = Origin + Direction * Size.x;
        float2 corner0 = Origin - right * halfWidth;
        float2 corner1 = Origin + right * halfWidth;
        float2 corner2 = end - right * halfWidth;
        float2 corner3 = end + right * halfWidth;
        minimum = math.min(math.min(corner0, corner1), math.min(corner2, corner3));
        maximum = math.max(math.max(corner0, corner1), math.max(corner2, corner3));
    }
}

public interface IUnitQueryVisitor
{
    bool Visit(in UnitQueryEntry entry);
}

/// <summary>
/// Read-only view of the current frame's quadtree.
/// </summary>
public struct UnitQueryTree
{
    [ReadOnly]
    private NativeArray<UnitQueryNode> _nodes;

    [ReadOnly]
    private NativeArray<UnitQueryEntry> _entries;

    public UnitQueryTree(NativeArray<UnitQueryNode> nodes, NativeArray<UnitQueryEntry> entries)
    {
        _nodes = nodes;
        _entries = entries;
    }

    public readonly bool IsCreated => _nodes.IsCreated && _entries.IsCreated;

    public readonly void Query<TVisitor>(
        in UnitQueryShape shape,
        UnitFactionType faction,
        ref TVisitor visitor,
        bool includeDead = false)
        where TVisitor : struct, IUnitQueryVisitor
    {
        Query(in shape, UnitQueryUtility.GetMask(faction), ref visitor, includeDead);
    }

    public readonly void Query<TVisitor>(
        in UnitQueryShape shape,
        UnitFactionMask factions,
        ref TVisitor visitor,
        bool includeDead = false)
        where TVisitor : struct, IUnitQueryVisitor
    {
        if (!IsCreated || _nodes.Length == 0 || factions == UnitFactionMask.None ||
            !shape.TryGetBounds(out float2 queryMin, out float2 queryMax))
        {
            return;
        }

        FixedList512Bytes<int> pendingNodes = default;
        pendingNodes.Add(0);
        while (pendingNodes.Length > 0)
        {
            int lastIndex = pendingNodes.Length - 1;
            int nodeIndex = pendingNodes[lastIndex];
            pendingNodes.RemoveAt(lastIndex);
            UnitQueryNode node = _nodes[nodeIndex];
            if (node.Count <= 0 || !Overlaps(node.Min, node.Max, queryMin, queryMax))
                continue;

            if (!node.IsLeaf)
            {
                // Reverse push order keeps traversal stable from quadrant 0 to 3.
                pendingNodes.Add(node.FirstChildIndex + 3);
                pendingNodes.Add(node.FirstChildIndex + 2);
                pendingNodes.Add(node.FirstChildIndex + 1);
                pendingNodes.Add(node.FirstChildIndex);
                continue;
            }

            int endIndex = node.StartIndex + node.Count;
            for (int entryIndex = node.StartIndex; entryIndex < endIndex; entryIndex++)
            {
                UnitQueryEntry entry = _entries[entryIndex];
                if ((factions & UnitQueryUtility.GetMask(entry.Faction)) == 0 ||
                    !includeDead && entry.IsDead != 0 ||
                    !shape.Contains(entry.Position.xy))
                {
                    continue;
                }

                if (!visitor.Visit(in entry))
                    return;
            }
        }
    }

    public readonly void Query(
        in UnitQueryShape shape,
        UnitFactionType faction,
        List<UnitQueryHit> results,
        bool reportDebug = true)
    {
        Query(in shape, UnitQueryUtility.GetMask(faction), results, reportDebug);
    }

    public readonly void Query(
        in UnitQueryShape shape,
        UnitFactionMask factions,
        List<UnitQueryHit> results,
        bool reportDebug = true)
    {
        results.Clear();
        UnitQueryListVisitor visitor = new() { Results = results };
        Query(in shape, factions, ref visitor);
        if (results.Count > 1)
            results.Sort(UnitQueryHitComparer.Instance);
        if (reportDebug)
            ReportQuery(in shape, results);
    }

    private static bool Overlaps(float2 leftMin, float2 leftMax, float2 rightMin, float2 rightMax)
    {
        return !math.any(leftMax < rightMin) && !math.any(leftMin > rightMax);
    }

    private static void ReportQuery(in UnitQueryShape shape, List<UnitQueryHit> results)
    {
        float3 origin = new(shape.Origin, 0f);
        switch (shape.Type)
        {
            case UnitQueryShapeType.Circle:
                DebugQueryShapeReporter.ReportCircle(origin, shape.Radius);
                break;
            case UnitQueryShapeType.WholeWorld:
                break;
            case UnitQueryShapeType.AxisAlignedRect:
                DebugQueryShapeReporter.ReportForwardRect(
                    new float3(shape.Origin - shape.Size * 0.5f, 0f),
                    new float2(1f, 0f),
                    shape.Size.x,
                    shape.Size.y);
                break;
            case UnitQueryShapeType.ForwardRect:
                DebugQueryShapeReporter.ReportForwardRect(
                    origin,
                    shape.Direction,
                    shape.Size.x,
                    shape.Size.y);
                break;
            case UnitQueryShapeType.Cone:
                DebugQueryShapeReporter.ReportCone(
                    origin,
                    shape.Direction,
                    shape.Radius,
                    shape.AngleDegrees);
                break;
        }

        for (int index = 0; index < results.Count; index++)
            DebugQueryShapeReporter.ReportHit(origin, results[index].Position);
    }

    private struct UnitQueryListVisitor : IUnitQueryVisitor
    {
        public List<UnitQueryHit> Results;

        public bool Visit(in UnitQueryEntry entry)
        {
            Results.Add(new UnitQueryHit
            {
                Entity = entry.Entity,
                Position = entry.Position,
                Faction = entry.Faction,
            });
            return true;
        }
    }

    private sealed class UnitQueryHitComparer : IComparer<UnitQueryHit>
    {
        public static readonly UnitQueryHitComparer Instance = new();

        public int Compare(UnitQueryHit left, UnitQueryHit right)
        {
            int indexComparison = left.Entity.Index.CompareTo(right.Entity.Index);
            return indexComparison != 0
                ? indexComparison
                : left.Entity.Version.CompareTo(right.Entity.Version);
        }
    }
}

public static class UnitQueryUtility
{
    public static UnitFactionMask GetMask(UnitFactionType faction)
    {
        int value = (int)faction;
        return value >= 0 && value < 8
            ? (UnitFactionMask)(1 << value)
            : UnitFactionMask.None;
    }

    public static UnitQueryTree GetTree(EntityManager entityManager)
    {
        UnitQuerySingleton singleton = GameSingletonUtility.Get<UnitQuerySingleton>(entityManager);
        return new UnitQueryTree(
            entityManager.GetBuffer<UnitQueryNode>(singleton.TreeEntity, true).AsNativeArray(),
            entityManager.GetBuffer<UnitQueryEntry>(singleton.TreeEntity, true).AsNativeArray());
    }
}
