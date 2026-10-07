using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrystalMagic.Editor.Map
{
    // Reconstruct the ground-plan enclosure, independently of all roof art.
    public static class TileHouseFootprintBuilder
    {
        private static readonly Vector3Int[] Directions =
            { Vector3Int.left, Vector3Int.right, Vector3Int.down, Vector3Int.up };

        public static List<Vector3Int> Build(IEnumerable<Vector3Int> frameCells)
        {
            var walls = new HashSet<Vector3Int>(frameCells);
            if (walls.Count < 4 || walls.Any(p => p.z != 0))
                throw new InvalidOperationException("房屋缺少可闭合的底层房框，请手工指定碰撞占地。");
            int x0 = walls.Min(p => p.x), x1 = walls.Max(p => p.x);
            int y0 = walls.Min(p => p.y), y1 = walls.Max(p => p.y);
            if ((long)x1 - x0 > 500 || (long)y1 - y0 > 500 ||
                ((long)x1 - x0 + 3) * ((long)y1 - y0 + 3) > 250000 ||
                x0 == int.MinValue || y0 == int.MinValue || x1 == int.MaxValue || y1 == int.MaxValue)
                throw new InvalidOperationException("房框范围过大，请分开处理。");
            bool Inside(Vector3Int p) => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1;

            // Continue the tangent of dangling wall ends. A repair is either a
            // straight gap or the intersection of two perpendicular end rays.
            // Shortest repairs go first: missing U-wing corners close locally
            // before a longer candidate could bridge across the courtyard.
            while (true)
            {
                var ends = new List<(Vector3Int position, Vector3Int direction)>();
                foreach (Vector3Int p in walls.OrderBy(p => p.y).ThenBy(p => p.x))
                {
                    var neighbours = Directions.Where(d => walls.Contains(p + d)).ToArray();
                    if (neighbours.Length == 1) ends.Add((p, -neighbours[0]));
                }
                if (ends.Count > 256) throw new InvalidOperationException("房框断点过多，请手工复核后导出。");
                var rays = new List<List<Vector3Int>>();
                List<Vector3Int> best = null;
                void Consider(List<Vector3Int> path)
                {
                    if (path.Count > 0 && (best == null || path.Count < best.Count)) best = path;
                }
                foreach (var end in ends)
                {
                    var ray = new List<Vector3Int>();
                    for (Vector3Int p = end.position + end.direction; Inside(p); p += end.direction)
                    {
                        if (walls.Contains(p)) { Consider(new List<Vector3Int>(ray)); break; }
                        ray.Add(p);
                    }
                    rays.Add(ray);
                }
                for (int i = 0; i < ends.Count; i++)
                    for (int j = i + 1; j < ends.Count; j++)
                    {
                        if (ends[i].direction.x * ends[j].direction.x + ends[i].direction.y * ends[j].direction.y != 0) continue;
                        int a = rays[i].FindIndex(p => rays[j].Contains(p));
                        if (a < 0) continue;
                        int b = rays[j].IndexOf(rays[i][a]);
                        Consider(rays[i].Take(a + 1).Concat(rays[j].Take(b + 1)).Distinct().ToList());
                    }
                if (best == null) break;
                walls.UnionWith(best);
            }

            // Flood only the exterior. This fills every enclosed room while
            // preserving concave notches, courtyards and the space between wings.
            var outside = new HashSet<Vector3Int>();
            var pending = new Queue<Vector3Int>();
            var start = new Vector3Int(x0 - 1, y0 - 1, 0);
            outside.Add(start); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                Vector3Int p = pending.Dequeue();
                foreach (Vector3Int d in Directions)
                {
                    Vector3Int next = p + d;
                    if (next.x < x0 - 1 || next.x > x1 + 1 || next.y < y0 - 1 || next.y > y1 + 1 || walls.Contains(next)) continue;
                    if (outside.Add(next)) pending.Enqueue(next);
                }
            }
            var interior = new HashSet<Vector3Int>();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    if (!outside.Contains(p) && !walls.Contains(p)) interior.Add(p);
                }
            // Do not silently export a leaked/open frame as a finished house.
            foreach (Vector3Int p in walls)
            {
                bool bordersRoom = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        bordersRoom |= interior.Contains(p + new Vector3Int(dx, dy, 0));
                if (!bordersRoom)
                    throw new InvalidOperationException($"房框在 {p} 附近无法可靠闭合，请用画碰撞格手工补齐后导出。");
            }
            walls.UnionWith(interior);
            return walls.OrderBy(p => p.y).ThenBy(p => p.x).ToList();
        }
    }
}
