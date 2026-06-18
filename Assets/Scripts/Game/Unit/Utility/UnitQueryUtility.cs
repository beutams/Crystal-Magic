using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public enum UnitQueryTreeKind
{
    Unit,
    WorldDrop,
}

public static class UnitQueryUtility
{
    public static bool TryGetTree(EntityManager entityManager, UnitQueryTreeKind treeKind, out UnitQueryTree tree)
    {
        EntityQuery singletonQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<UnitQuerySingleton>(),
            ComponentType.ReadOnly<UnitQueryRuntimeComponent>());
        if (singletonQuery.IsEmptyIgnoreFilter)
        {
            tree = null;
            return false;
        }

        Entity singletonEntity = singletonQuery.GetSingletonEntity();
        UnitQueryRuntimeComponent runtime = entityManager.GetComponentObject<UnitQueryRuntimeComponent>(singletonEntity);
        if (runtime == null)
        {
            tree = null;
            return false;
        }

        tree = treeKind == UnitQueryTreeKind.WorldDrop
            ? runtime.WorldDropTree
            : runtime.UnitTree;
        return tree != null;
    }
}

public sealed class UnitQueryTree
{
    private const int MaxDepth = 6;
    private const int LeafCapacity = 8;
    private const float MinimumRootSize = 1f;
    private const float SplitEpsilon = 0.01f;
    private const float RootPadding = 0.25f;

    private readonly List<UnitQueryHit> _entries = new();
    private readonly List<Node> _nodes = new();
    private int _activeNodeCount;
    private int _rootIndex = -1;

    public void Rebuild(List<UnitQueryHit> sourceEntries)
    {
        Reset();
        if (sourceEntries == null || sourceEntries.Count == 0)
            return;

        _entries.AddRange(sourceEntries);

        float2 min = _entries[0].Position.xy;
        float2 max = _entries[0].Position.xy;
        for (int i = 1; i < _entries.Count; i++)
        {
            float2 position = _entries[i].Position.xy;
            min = math.min(min, position);
            max = math.max(max, position);
        }

        float2 size = max - min;
        float extent = math.max(MinimumRootSize, math.max(size.x, size.y) + RootPadding * 2f);
        float2 center = (min + max) * 0.5f;
        float2 halfExtent = new(extent * 0.5f);

        _rootIndex = AllocateNode(center - halfExtent, center + halfExtent);
        for (int i = 0; i < _entries.Count; i++)
            Insert(_rootIndex, i, 0);
    }

    public void QueryCircle(float3 center, float radius, List<UnitQueryHit> results)
    {
        results.Clear();
        if (_rootIndex < 0 || radius <= 0f)
            return;

        QueryCircle(_rootIndex, center.xy, radius * radius, results);
    }

    public void QueryForwardRect(float3 origin, float2 forward, float length, float width, List<UnitQueryHit> results)
    {
        results.Clear();
        if (_rootIndex < 0 || length <= 0f || width <= 0f || math.lengthsq(forward) <= 0.0001f)
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

        QueryForwardRect(_rootIndex, origin.xy, normalizedForward, right, length, halfWidth, rectMin, rectMax, results);
    }

    public void QueryCone(float3 origin, float2 forward, float radius, float angleDegrees, List<UnitQueryHit> results)
    {
        results.Clear();
        if (_rootIndex < 0 || radius <= 0f || angleDegrees <= 0f || math.lengthsq(forward) <= 0.0001f)
            return;

        float2 normalizedForward = math.normalize(forward);
        float radiusSq = radius * radius;
        float minDot = math.cos(math.radians(math.clamp(angleDegrees, 0f, 360f) * 0.5f));
        QueryCone(_rootIndex, origin.xy, normalizedForward, radiusSq, minDot, results);
    }

    private void Reset()
    {
        for (int i = 0; i < _activeNodeCount; i++)
            _nodes[i].Reset(float2.zero, float2.zero);

        _entries.Clear();
        _activeNodeCount = 0;
        _rootIndex = -1;
    }

    private int AllocateNode(float2 min, float2 max)
    {
        Node node;
        if (_activeNodeCount < _nodes.Count)
        {
            node = _nodes[_activeNodeCount];
        }
        else
        {
            node = new Node();
            _nodes.Add(node);
        }

        node.Reset(min, max);
        return _activeNodeCount++;
    }

    private void Insert(int nodeIndex, int entryIndex, int depth)
    {
        Node node = _nodes[nodeIndex];
        if (!node.HasChildren)
        {
            if (node.EntryIndices.Count < LeafCapacity || !CanSplit(node, depth))
            {
                node.EntryIndices.Add(entryIndex);
                return;
            }

            Split(nodeIndex);
            RedistributeEntries(nodeIndex, depth);
        }

        int childIndex = GetChildIndex(node, _entries[entryIndex].Position.xy);
        Insert(childIndex, entryIndex, depth + 1);
    }

    private bool CanSplit(Node node, int depth)
    {
        if (depth >= MaxDepth)
            return false;

        float2 size = node.Max - node.Min;
        return size.x > SplitEpsilon && size.y > SplitEpsilon;
    }

    private void Split(int nodeIndex)
    {
        Node node = _nodes[nodeIndex];
        float2 center = (node.Min + node.Max) * 0.5f;

        node.Child0 = AllocateNode(node.Min, center);
        node.Child1 = AllocateNode(new float2(center.x, node.Min.y), new float2(node.Max.x, center.y));
        node.Child2 = AllocateNode(new float2(node.Min.x, center.y), new float2(center.x, node.Max.y));
        node.Child3 = AllocateNode(center, node.Max);
    }

    private void RedistributeEntries(int nodeIndex, int depth)
    {
        Node node = _nodes[nodeIndex];
        for (int i = 0; i < node.EntryIndices.Count; i++)
        {
            int existingEntryIndex = node.EntryIndices[i];
            int childIndex = GetChildIndex(node, _entries[existingEntryIndex].Position.xy);
            Insert(childIndex, existingEntryIndex, depth + 1);
        }

        node.EntryIndices.Clear();
    }

    private static int GetChildIndex(Node node, float2 position)
    {
        float2 center = (node.Min + node.Max) * 0.5f;
        bool right = position.x >= center.x;
        bool top = position.y >= center.y;

        if (top)
            return right ? node.Child3 : node.Child2;

        return right ? node.Child1 : node.Child0;
    }

    private void QueryCircle(int nodeIndex, float2 center, float radiusSq, List<UnitQueryHit> results)
    {
        Node node = _nodes[nodeIndex];
        if (!IntersectsCircle(node.Min, node.Max, center, radiusSq))
            return;

        for (int i = 0; i < node.EntryIndices.Count; i++)
        {
            UnitQueryHit entry = _entries[node.EntryIndices[i]];
            if (math.lengthsq(entry.Position.xy - center) > radiusSq)
                continue;

            results.Add(entry);
        }

        if (!node.HasChildren)
            return;

        QueryCircle(node.Child0, center, radiusSq, results);
        QueryCircle(node.Child1, center, radiusSq, results);
        QueryCircle(node.Child2, center, radiusSq, results);
        QueryCircle(node.Child3, center, radiusSq, results);
    }

    private void QueryForwardRect(
        int nodeIndex,
        float2 origin,
        float2 normalizedForward,
        float2 right,
        float length,
        float halfWidth,
        float2 rectMin,
        float2 rectMax,
        List<UnitQueryHit> results)
    {
        Node node = _nodes[nodeIndex];
        if (!OverlapsAabb(node.Min, node.Max, rectMin, rectMax))
            return;

        for (int i = 0; i < node.EntryIndices.Count; i++)
        {
            UnitQueryHit entry = _entries[node.EntryIndices[i]];
            float2 diff = entry.Position.xy - origin;
            float forwardDistance = math.dot(diff, normalizedForward);
            if (forwardDistance < 0f || forwardDistance > length)
                continue;

            float lateralDistance = math.abs(math.dot(diff, right));
            if (lateralDistance > halfWidth)
                continue;

            results.Add(entry);
        }

        if (!node.HasChildren)
            return;

        QueryForwardRect(node.Child0, origin, normalizedForward, right, length, halfWidth, rectMin, rectMax, results);
        QueryForwardRect(node.Child1, origin, normalizedForward, right, length, halfWidth, rectMin, rectMax, results);
        QueryForwardRect(node.Child2, origin, normalizedForward, right, length, halfWidth, rectMin, rectMax, results);
        QueryForwardRect(node.Child3, origin, normalizedForward, right, length, halfWidth, rectMin, rectMax, results);
    }

    private void QueryCone(
        int nodeIndex,
        float2 origin,
        float2 normalizedForward,
        float radiusSq,
        float minDot,
        List<UnitQueryHit> results)
    {
        Node node = _nodes[nodeIndex];
        if (!IntersectsCircle(node.Min, node.Max, origin, radiusSq))
            return;

        for (int i = 0; i < node.EntryIndices.Count; i++)
        {
            UnitQueryHit entry = _entries[node.EntryIndices[i]];
            float2 diff = entry.Position.xy - origin;
            float distanceSq = math.lengthsq(diff);
            if (distanceSq > radiusSq)
                continue;

            if (distanceSq <= 0.0001f)
            {
                results.Add(entry);
                continue;
            }

            float2 normalizedDiff = diff * math.rsqrt(distanceSq);
            if (math.dot(normalizedForward, normalizedDiff) < minDot)
                continue;

            results.Add(entry);
        }

        if (!node.HasChildren)
            return;

        QueryCone(node.Child0, origin, normalizedForward, radiusSq, minDot, results);
        QueryCone(node.Child1, origin, normalizedForward, radiusSq, minDot, results);
        QueryCone(node.Child2, origin, normalizedForward, radiusSq, minDot, results);
        QueryCone(node.Child3, origin, normalizedForward, radiusSq, minDot, results);
    }

    private static bool IntersectsCircle(float2 min, float2 max, float2 center, float radiusSq)
    {
        float2 closest = math.clamp(center, min, max);
        return math.lengthsq(closest - center) <= radiusSq;
    }

    private static bool OverlapsAabb(float2 minA, float2 maxA, float2 minB, float2 maxB)
    {
        return minA.x <= maxB.x &&
               maxA.x >= minB.x &&
               minA.y <= maxB.y &&
               maxA.y >= minB.y;
    }

    private sealed class Node
    {
        public readonly List<int> EntryIndices = new();
        public float2 Min;
        public float2 Max;
        public int Child0 = -1;
        public int Child1 = -1;
        public int Child2 = -1;
        public int Child3 = -1;

        public bool HasChildren => Child0 >= 0;

        public void Reset(float2 min, float2 max)
        {
            Min = min;
            Max = max;
            Child0 = -1;
            Child1 = -1;
            Child2 = -1;
            Child3 = -1;
            EntryIndices.Clear();
        }
    }
}
