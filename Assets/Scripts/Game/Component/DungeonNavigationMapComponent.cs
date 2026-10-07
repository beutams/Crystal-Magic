using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Describes the immutable grid used by runtime navigation. Collision bits live in
    /// the <see cref="DungeonNavigationCollisionWord"/> buffer on the same entity.
    /// </summary>
    public struct DungeonNavigationMapComponent : IComponentData
    {
        public int Width;
        public int Height;
        public float CellSize;
        public float2 WorldOrigin;
        public int Version;

        public int CellCount => Width * Height;
    }

    /// <summary>
    /// Each word describes 64 cells. Navigation also blocks void; projectiles only
    /// collide with solid obstacles, so the two masks must stay separate.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DungeonNavigationCollisionWord : IBufferElementData
    {
        public ulong Value;
        public ulong ProjectileValue;
    }

    public static class DungeonNavigationMapUtility
    {
        public static int GetRequiredWordCount(int cellCount)
        {
            return (math.max(0, cellCount) + 63) >> 6;
        }

        public static bool IsInside(in DungeonNavigationMapComponent map, int2 cell)
        {
            return cell.x >= 0 && cell.x < map.Width && cell.y >= 0 && cell.y < map.Height;
        }

        public static int ToIndex(in DungeonNavigationMapComponent map, int2 cell)
        {
            return cell.y * map.Width + cell.x;
        }

        public static int2 ToCell(in DungeonNavigationMapComponent map, int index)
        {
            return new int2(index % map.Width, index / map.Width);
        }

        public static int2 WorldToCell(in DungeonNavigationMapComponent map, float2 worldPosition)
        {
            int2 cell = (int2)math.floor((worldPosition - map.WorldOrigin) / map.CellSize);
            return math.clamp(cell, int2.zero, new int2(map.Width - 1, map.Height - 1));
        }

        public static float2 CellToWorld(in DungeonNavigationMapComponent map, int2 cell)
        {
            return map.WorldOrigin + (new float2(cell.x, cell.y) + 0.5f) * map.CellSize;
        }

        public static bool IsBlocked(
            in DungeonNavigationMapComponent map,
            NativeArray<DungeonNavigationCollisionWord> collisionWords,
            int2 cell)
        {
            if (!IsInside(in map, cell))
                return true;

            int index = ToIndex(in map, cell);
            int wordIndex = index >> 6;
            if ((uint)wordIndex >= (uint)collisionWords.Length)
                return true;

            ulong mask = 1UL << (index & 63);
            return (collisionWords[wordIndex].Value & mask) != 0UL;
        }

        public static bool CanOccupy(
            in DungeonNavigationMapComponent map,
            NativeArray<DungeonNavigationCollisionWord> collisionWords,
            int2 cell,
            float clearanceRadius)
        {
            if (IsBlocked(in map, collisionWords, cell))
                return false;

            float radius = math.max(0f, clearanceRadius);
            if (radius <= 0f)
                return true;

            int radiusInCells = math.max(1, (int)math.ceil(radius / map.CellSize));
            float radiusSquared = radius * radius;
            for (int y = -radiusInCells; y <= radiusInCells; y++)
            {
                for (int x = -radiusInCells; x <= radiusInCells; x++)
                {
                    int2 checkCell = cell + new int2(x, y);
                    if (!IsBlocked(in map, collisionWords, checkCell))
                        continue;

                    float edgeX = math.max(math.abs(x) * map.CellSize - map.CellSize * 0.5f, 0f);
                    float edgeY = math.max(math.abs(y) * map.CellSize - map.CellSize * 0.5f, 0f);
                    if (edgeX * edgeX + edgeY * edgeY < radiusSquared)
                        return false;
                }
            }

            return true;
        }
    }
}
