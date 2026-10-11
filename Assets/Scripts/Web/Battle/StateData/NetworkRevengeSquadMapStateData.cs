using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[Serializable]
public sealed class NetworkRevengeSquadMapStateData : NetworkStateData
{
    public Point[] points = Array.Empty<Point>();

    [Serializable]
    public struct Point
    {
        public int squadId;
        public float x, y, z;
    }

    // Include sleeping members: their client entities are intentionally suspended.
    public static NetworkRevengeSquadMapStateData Capture(EntityManager manager, EntityQuery query)
    {
        manager.CompleteAllTrackedJobs();
        using NativeArray<Entity> members = query.ToEntityArray(Allocator.Temp);
        NativeHashMap<int, float4> centers = new(math.max(1, members.Length), Allocator.Temp);
        foreach (Entity member in members)
        {
            int id = manager.GetComponentData<DungeonRevengeSquadMember>(member).SquadId;
            centers.TryGetValue(id, out float4 center);
            centers[id] = center + new float4(manager.GetComponentData<LocalTransform>(member).Position, 1f);
        }
        List<Point> result = new(centers.Count);
        foreach (var pair in centers)
        {
            float3 position = pair.Value.xyz / pair.Value.w;
            result.Add(new Point { squadId = pair.Key, x = position.x, y = position.y, z = position.z });
        }
        result.Sort((a, b) => a.squadId.CompareTo(b.squadId));
        centers.Dispose();
        return new NetworkRevengeSquadMapStateData { points = result.ToArray() };
    }

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.IsClient) return;
        Entity entity = context.GetOrCreateClientPresentationEntity();
        EntityManager manager = context.EntityManager;
        if (!manager.HasBuffer<DungeonRevengeSquadMapElement>(entity))
            manager.AddBuffer<DungeonRevengeSquadMapElement>(entity);
        DynamicBuffer<DungeonRevengeSquadMapElement> markers = manager.GetBuffer<DungeonRevengeSquadMapElement>(entity);
        markers.Clear();
        if (points == null) return;
        foreach (Point point in points)
            markers.Add(new DungeonRevengeSquadMapElement
                { SquadId = point.squadId, Position = new float3(point.x, point.y, point.z) });
    }
}
