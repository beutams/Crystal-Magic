using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.MapDemo
{
    public enum DungeonMakerSquareData : byte
    {
        OPEN = 0,
        CLOSED = 1,
        G_OPEN = 2,
        G_CLOSED = 3,
        NJ_OPEN = 4,
        NJ_CLOSED = 5,
        NJ_G_OPEN = 6,
        NJ_G_CLOSED = 7,
        IR_OPEN = 8,
        IT_OPEN = 9,
        IA_OPEN = 10,
        H_DOOR = 11,
        V_DOOR = 12,
        MOB1 = 13,
        MOB2 = 14,
        MOB3 = 15,
        TREAS1 = 16,
        TREAS2 = 17,
        TREAS3 = 18,
        COLUMN = 19,
    }

    [Serializable]
    public struct TunnelingMapMetrics
    {
        public int 总格子数;
        public int 可通行格子数;
        public float 可通行比例;
        public int 连通块数量;
        public int 死路数量;
        public int 岔路数量;
        public int 最长水平视线;
        public int 最长垂直视线;
        public int 最长视线;
        public int 最大开放矩形宽度;
        public int 最大开放矩形高度;
        public int 最大开放矩形面积;
        public bool 适合远程作战;
    }

    public sealed class DungeonMakerTunnelingResult
    {
        private readonly DungeonMakerSquareData[] _map;

        public DungeonMakerTunnelingResult(
            int sourceWidth,
            int sourceHeight,
            int seed,
            DungeonMakerSquareData[] map,
            TunnelingMapMetrics metrics)
        {
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            Seed = seed;
            _map = map;
            Metrics = metrics;
        }

        public int SourceWidth { get; }
        public int SourceHeight { get; }
        public int DisplayWidth => SourceHeight;
        public int DisplayHeight => SourceWidth;
        public int Seed { get; }
        public TunnelingMapMetrics Metrics { get; }

        public DungeonMakerSquareData GetSourceTile(int x, int y)
        {
            return _map[x * SourceHeight + y];
        }

        public DungeonMakerSquareData GetDisplayTile(int x, int y)
        {
            return GetSourceTile(y, x);
        }
    }

    internal static class DungeonMakerTunnelingGenerator
    {
        public const int DefaultSeed = 1015776839;

        public static DungeonMakerTunnelingResult Generate(int seed)
        {
            DungeonRuntime runtime = new(seed == 0 ? DefaultSeed : seed);
            runtime.Generate();
            return runtime.BuildResult();
        }

        private sealed class DungeonRuntime
        {
            private readonly MsRand _random;
            private readonly List<Builder> _builders = new();
            private readonly List<Room> _rooms = new();
            private readonly DungeonConfig _config = DungeonConfig.CreateOriginal();

            private DungeonMakerSquareData[] _map;
            private bool _changedThisIteration;
            private int _activeGeneration;
            private int _currentSmallRooms;
            private int _currentMediumRooms;
            private int _currentLargeRooms;

            public DungeonRuntime(int seed)
            {
                Seed = seed;
                _random = new MsRand(seed);
                InitFromConfig();
            }

            public int Seed { get; }

            public void Generate()
            {
                while (true)
                {
                    while (MakeIteration())
                    {
                    }

                    if (!AdvanceGeneration())
                        break;
                }

                if (_config.TunnelCrawlerGeneration < 0 || _activeGeneration < _config.TunnelCrawlerGeneration)
                {
                    while (true)
                    {
                        while (MakeIteration())
                        {
                        }

                        if (!AdvanceGeneration())
                            break;
                    }
                }
            }

            public DungeonMakerTunnelingResult BuildResult()
            {
                DungeonMakerSquareData[] copiedMap = new DungeonMakerSquareData[_map.Length];
                Array.Copy(_map, copiedMap, _map.Length);
                TunnelingMapMetrics metrics = AnalyzeMetrics(copiedMap, _config.DimX, _config.DimY);
                return new DungeonMakerTunnelingResult(_config.DimX, _config.DimY, Seed, copiedMap, metrics);
            }

            private void InitFromConfig()
            {
                _activeGeneration = 0;
                _currentSmallRooms = 0;
                _currentMediumRooms = 0;
                _currentLargeRooms = 0;

                _map = new DungeonMakerSquareData[_config.DimX * _config.DimY];
                for (int i = 0; i < _map.Length; i++)
                    _map[i] = _config.Background;

                SetRect(0, 0, _config.DimX - 1, 0, DungeonMakerSquareData.G_CLOSED);
                SetRect(0, 0, 0, _config.DimY - 1, DungeonMakerSquareData.G_CLOSED);
                SetRect(_config.DimX - 1, 0, _config.DimX - 1, _config.DimY - 1, DungeonMakerSquareData.G_CLOSED);
                SetRect(0, _config.DimY - 1, _config.DimX - 1, _config.DimY - 1, DungeonMakerSquareData.G_CLOSED);

                foreach (Direction opening in _config.Openings)
                {
                    switch (opening)
                    {
                        case Direction.NO:
                            SetRect(0, _config.DimY / 2 - 1, 2, _config.DimY / 2 + 1, DungeonMakerSquareData.G_OPEN);
                            break;
                        case Direction.WE:
                            SetRect(_config.DimX / 2 - 1, 0, _config.DimX / 2 + 1, 2, DungeonMakerSquareData.G_OPEN);
                            break;
                        case Direction.EA:
                            SetRect(_config.DimX / 2 - 1, _config.DimY - 3, _config.DimX / 2 + 1, _config.DimY - 1, DungeonMakerSquareData.G_OPEN);
                            break;
                        case Direction.SO:
                            SetRect(_config.DimX - 3, _config.DimY / 2 - 1, _config.DimX - 1, _config.DimY / 2 + 1, DungeonMakerSquareData.G_OPEN);
                            break;
                        case Direction.NW:
                            SetRect(0, 0, 2, 2, DungeonMakerSquareData.G_OPEN);
                            break;
                        case Direction.NE:
                            SetRect(0, _config.DimY - 3, 2, _config.DimY - 1, DungeonMakerSquareData.G_OPEN);
                            break;
                        case Direction.SW:
                            SetRect(_config.DimX - 3, 0, _config.DimX - 1, 2, DungeonMakerSquareData.G_OPEN);
                            break;
                        case Direction.SE:
                            SetRect(_config.DimX - 3, _config.DimY - 3, _config.DimX - 1, _config.DimY - 1, DungeonMakerSquareData.G_OPEN);
                            break;
                    }
                }

                foreach (TunnelerSeed seed in _config.Tunnelers)
                {
                    CreateTunneler(
                        seed.Location,
                        seed.Direction,
                        -seed.Age,
                        seed.MaxAge,
                        seed.Generation,
                        seed.IntendedDirection,
                        seed.StepLength,
                        seed.TunnelWidth,
                        seed.StraightDoubleSpawnProb,
                        seed.TurnDoubleSpawnProb,
                        seed.ChangeDirectionProb,
                        seed.MakeRoomsRightProb,
                        seed.MakeRoomsLeftProb,
                        seed.JoinPreference);
                }
            }

            private bool MakeIteration()
            {
                _changedThisIteration = false;

                for (int i = 0; i < _builders.Count; i++)
                {
                    Builder builder = _builders[i];
                    if (builder == null)
                        continue;

                    if (!builder.StepAhead())
                        _builders[i] = null;
                }

                return _changedThisIteration;
            }

            private bool AdvanceGeneration()
            {
                bool thereAreBuilders = false;
                int highestNegativeAge = 0;

                for (int i = 0; i < _builders.Count; i++)
                {
                    Builder builder = _builders[i];
                    if (builder == null)
                        continue;

                    thereAreBuilders = true;
                    if (builder.Generation != _activeGeneration)
                        continue;

                    int age = builder.Age;
                    if (age >= 0)
                        return true;

                    if (highestNegativeAge == 0 || age > highestNegativeAge)
                        highestNegativeAge = age;
                }

                if (highestNegativeAge == 0)
                {
                    _activeGeneration++;
                    return thereAreBuilders;
                }

                for (int i = 0; i < _builders.Count; i++)
                {
                    Builder builder = _builders[i];
                    if (builder != null && builder.Generation == _activeGeneration)
                        builder.Age += -highestNegativeAge;
                }

                return thereAreBuilders;
            }

            internal void CreateTunneler(
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                IntCoordinate intendedDirection,
                int stepLength,
                int tunnelWidth,
                int straightDoubleSpawnProb,
                int turnDoubleSpawnProb,
                int changeDirectionProb,
                int makeRoomsRightProb,
                int makeRoomsLeftProb,
                int joinPreference)
            {
                AddBuilder(new Tunneler(
                    this,
                    location,
                    forward,
                    age,
                    maxAge,
                    generation,
                    intendedDirection,
                    stepLength,
                    tunnelWidth,
                    straightDoubleSpawnProb,
                    turnDoubleSpawnProb,
                    changeDirectionProb,
                    makeRoomsRightProb,
                    makeRoomsLeftProb,
                    joinPreference));
            }

            internal void CreateRoomie(
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                int defaultWidth,
                RoomSize size,
                int category)
            {
                AddBuilder(new Roomie(this, location, forward, age, maxAge, generation, defaultWidth, size, category));
            }

            private void AddBuilder(Builder builder)
            {
                for (int i = 0; i < _builders.Count; i++)
                {
                    if (_builders[i] == null)
                    {
                        _builders[i] = builder;
                        return;
                    }
                }

                _builders.Add(builder);
            }

            public int ActiveGeneration => _activeGeneration;
            public int DimX => _config.DimX;
            public int DimY => _config.DimY;
            public int Mutator => _config.Mutator;
            public int TunnelJoinDist => _config.TunnelJoinDist;
            public int Patience => _config.Patience;
            public int SizeUpGenDelay => _config.SizeUpGenDelay;
            public bool ColumnsInTunnels => _config.ColumnsInTunnels;
            public double RoomAspectRatio => _config.RoomAspectRatio;
            public int GenSpeedUpOnAnteRoom => _config.GenSpeedUpOnAnteRoom;
            public int LastChanceGenDelay => _config.LastChanceGenerationalDelay;
            public TunnelerSeed LastChanceTunneler => _config.LastChanceTunneler;

            public int GetRoomSizeProbS(int tunnelWidth, RoomSize roomSize)
            {
                if (tunnelWidth >= _config.RoomSizeProbS.Count)
                    return roomSize == RoomSize.LARGE ? 100 : 0;

                return roomSize switch
                {
                    RoomSize.SMALL => _config.RoomSizeProbS[tunnelWidth].Small,
                    RoomSize.MEDIUM => _config.RoomSizeProbS[tunnelWidth].Medium,
                    _ => _config.RoomSizeProbS[tunnelWidth].Large,
                };
            }

            public int GetRoomSizeProbB(int tunnelWidth, RoomSize roomSize)
            {
                if (tunnelWidth >= _config.RoomSizeProbB.Count)
                    return roomSize == RoomSize.LARGE ? 100 : 0;

                return roomSize switch
                {
                    RoomSize.SMALL => _config.RoomSizeProbB[tunnelWidth].Small,
                    RoomSize.MEDIUM => _config.RoomSizeProbB[tunnelWidth].Medium,
                    _ => _config.RoomSizeProbB[tunnelWidth].Large,
                };
            }

            public int GetBabyDelayProbsForGenerationR(int generation)
            {
                return generation is >= 0 and <= 10 ? _config.BabyDelayProbsRoomie[generation] : 0;
            }

            public int GetBabyDelayProbsForGenerationT(int generation)
            {
                return generation is >= 0 and <= 10 ? _config.BabyDelayProbsTunneler[generation] : 0;
            }

            public int GetMaxAgeT(int generation)
            {
                return generation >= _config.MaxAgesT.Count
                    ? _config.MaxAgesT[_config.MaxAgesT.Count - 1]
                    : _config.MaxAgesT[generation];
            }

            public int GetAnteRoomProb(int tunnelWidth)
            {
                return tunnelWidth >= _config.AnteRoomProb.Count ? 100 : _config.AnteRoomProb[tunnelWidth];
            }

            public int GetSizeUpProb(int generation)
            {
                return generation >= _config.SizeUpProb.Count
                    ? _config.SizeUpProb[_config.SizeUpProb.Count - 1]
                    : _config.SizeUpProb[generation];
            }

            public int GetSizeDownProb(int generation)
            {
                return generation >= _config.SizeDownProb.Count
                    ? _config.SizeDownProb[_config.SizeDownProb.Count - 1]
                    : _config.SizeDownProb[generation];
            }

            public int GetMinRoomSize(RoomSize roomSize)
            {
                return roomSize switch
                {
                    RoomSize.SMALL => _config.MinSmallRoomSize,
                    RoomSize.MEDIUM => _config.MinMediumRoomSize,
                    _ => _config.MinLargeRoomSize,
                };
            }

            public int GetMaxRoomSize(RoomSize roomSize)
            {
                return roomSize switch
                {
                    RoomSize.SMALL => _config.MinMediumRoomSize - 1,
                    RoomSize.MEDIUM => _config.MinLargeRoomSize - 1,
                    _ => _config.MaxRoomSize - 1,
                };
            }

            public bool WantsMoreRoomsD(RoomSize roomSize)
            {
                return roomSize switch
                {
                    RoomSize.SMALL => _config.MaxSmallDungeonRooms > _currentSmallRooms,
                    RoomSize.MEDIUM => _config.MaxMediumDungeonRooms > _currentMediumRooms,
                    _ => _config.MaxLargeDungeonRooms > _currentLargeRooms,
                };
            }

            public void BuiltRoomD(RoomSize roomSize)
            {
                switch (roomSize)
                {
                    case RoomSize.SMALL:
                        _currentSmallRooms++;
                        break;
                    case RoomSize.MEDIUM:
                        _currentMediumRooms++;
                        break;
                    default:
                        _currentLargeRooms++;
                        break;
                }
            }

            public int Mutate(int input)
            {
                int output = input - _config.Mutator + _random.Next(2 * _config.Mutator + 1);
                return output < 0 ? 0 : output;
            }

            public int Next100()
            {
                return _random.Next(100);
            }

            public int Next101()
            {
                return _random.Next(101);
            }

            public bool CoinFlip()
            {
                return _random.Next(2) == 0;
            }

            public DungeonMakerSquareData GetMap(IntCoordinate position)
            {
                return _map[position.X * _config.DimY + position.Y];
            }

            public DungeonMakerSquareData GetMap(int x, int y)
            {
                return _map[x * _config.DimY + y];
            }

            public void SetMap(IntCoordinate position, DungeonMakerSquareData value)
            {
                _map[position.X * _config.DimY + position.Y] = value;
                _changedThisIteration = true;
            }

            public void SetMap(int x, int y, DungeonMakerSquareData value)
            {
                _map[x * _config.DimY + y] = value;
                _changedThisIteration = true;
            }

            public void SetRect(int startX, int startY, int endX, int endY, DungeonMakerSquareData value)
            {
                if (endX < startX || endY < startY)
                    return;

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                        SetMap(x, y, value);
                }
            }

            public void AddRoom(Room room)
            {
                _rooms.Add(room);
            }

            private static TunnelingMapMetrics AnalyzeMetrics(DungeonMakerSquareData[] map, int width, int height)
            {
                TunnelingMapMetrics metrics = new
                TunnelingMapMetrics
                {
                    总格子数 = width * height,
                };

                int[] heights = new int[width];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (IsWalkable(map[x * height + y]))
                        {
                            metrics.可通行格子数++;
                            heights[x] += 1;

                            int neighbors = CountCardinalWalkableNeighbors(map, width, height, x, y);
                            if (neighbors <= 1)
                                metrics.死路数量++;
                            else if (neighbors >= 3)
                                metrics.岔路数量++;
                        }
                        else
                        {
                            heights[x] = 0;
                        }
                    }

                    EvaluateLargestRectangleInHistogram(heights, ref metrics);
                }

                metrics.可通行比例 = metrics.总格子数 > 0
                    ? (float)metrics.可通行格子数 / metrics.总格子数
                    : 0f;
                metrics.连通块数量 = CountComponents(map, width, height);
                metrics.最长水平视线 = CalculateLongestHorizontalSightline(map, width, height);
                metrics.最长垂直视线 = CalculateLongestVerticalSightline(map, width, height);
                metrics.最长视线 = Math.Max(metrics.最长水平视线, metrics.最长垂直视线);
                metrics.适合远程作战 =
                    metrics.连通块数量 == 1 &&
                    metrics.最长视线 >= 16 &&
                    metrics.最大开放矩形宽度 >= 8 &&
                    metrics.最大开放矩形高度 >= 6;

                return metrics;
            }

            private static bool IsWalkable(DungeonMakerSquareData tile)
            {
                return tile is DungeonMakerSquareData.OPEN
                    or DungeonMakerSquareData.G_OPEN
                    or DungeonMakerSquareData.NJ_OPEN
                    or DungeonMakerSquareData.NJ_G_OPEN
                    or DungeonMakerSquareData.IR_OPEN
                    or DungeonMakerSquareData.IT_OPEN
                    or DungeonMakerSquareData.IA_OPEN
                    or DungeonMakerSquareData.H_DOOR
                    or DungeonMakerSquareData.V_DOOR;
            }

            private static int CountCardinalWalkableNeighbors(DungeonMakerSquareData[] map, int width, int height, int x, int y)
            {
                int count = 0;
                if (x > 0 && IsWalkable(map[(x - 1) * height + y])) count++;
                if (x < width - 1 && IsWalkable(map[(x + 1) * height + y])) count++;
                if (y > 0 && IsWalkable(map[x * height + (y - 1)])) count++;
                if (y < height - 1 && IsWalkable(map[x * height + (y + 1)])) count++;
                return count;
            }

            private static int CountComponents(DungeonMakerSquareData[] map, int width, int height)
            {
                bool[] visited = new bool[map.Length];
                Queue<IntCoordinate> queue = new();
                int componentCount = 0;

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index = x * height + y;
                        if (visited[index] || !IsWalkable(map[index]))
                            continue;

                        componentCount++;
                        visited[index] = true;
                        queue.Enqueue(new IntCoordinate(x, y));

                        while (queue.Count > 0)
                        {
                            IntCoordinate current = queue.Dequeue();
                            TryEnqueue(current.X - 1, current.Y);
                            TryEnqueue(current.X + 1, current.Y);
                            TryEnqueue(current.X, current.Y - 1);
                            TryEnqueue(current.X, current.Y + 1);
                        }
                    }
                }

                return componentCount;

                void TryEnqueue(int x, int y)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height)
                        return;

                    int index = x * height + y;
                    if (visited[index] || !IsWalkable(map[index]))
                        return;

                    visited[index] = true;
                    queue.Enqueue(new IntCoordinate(x, y));
                }
            }

            private static int CalculateLongestHorizontalSightline(DungeonMakerSquareData[] map, int width, int height)
            {
                int longest = 0;
                for (int y = 0; y < height; y++)
                {
                    int current = 0;
                    for (int x = 0; x < width; x++)
                    {
                        if (IsWalkable(map[x * height + y]))
                        {
                            current++;
                            if (current > longest)
                                longest = current;
                        }
                        else
                        {
                            current = 0;
                        }
                    }
                }

                return longest;
            }

            private static int CalculateLongestVerticalSightline(DungeonMakerSquareData[] map, int width, int height)
            {
                int longest = 0;
                for (int x = 0; x < width; x++)
                {
                    int current = 0;
                    for (int y = 0; y < height; y++)
                    {
                        if (IsWalkable(map[x * height + y]))
                        {
                            current++;
                            if (current > longest)
                                longest = current;
                        }
                        else
                        {
                            current = 0;
                        }
                    }
                }

                return longest;
            }

            private static void EvaluateLargestRectangleInHistogram(int[] heights, ref TunnelingMapMetrics metrics)
            {
                Stack<int> stack = new();
                for (int i = 0; i <= heights.Length; i++)
                {
                    int currentHeight = i == heights.Length ? 0 : heights[i];
                    while (stack.Count > 0 && currentHeight < heights[stack.Peek()])
                    {
                        int height = heights[stack.Pop()];
                        int width = stack.Count == 0 ? i : i - stack.Peek() - 1;
                        int area = height * width;
                        if (area > metrics.最大开放矩形面积)
                        {
                            metrics.最大开放矩形面积 = area;
                            metrics.最大开放矩形宽度 = width;
                            metrics.最大开放矩形高度 = height;
                        }
                    }

                    stack.Push(i);
                }
            }
        }

        private abstract class Builder
        {
            protected Builder(DungeonRuntime dungeon, IntCoordinate location, IntCoordinate forward, int age, int maxAge, int generation)
            {
                Dungeon = dungeon;
                Location = location;
                Forward = forward;
                Age = age;
                MaxAge = maxAge;
                Generation = generation;
            }

            protected DungeonRuntime Dungeon { get; }
            public IntCoordinate Location;
            public IntCoordinate Forward;
            public int Age;
            public int MaxAge;
            public int Generation;

            public abstract bool StepAhead();

            protected static IntCoordinate GetRight(IntCoordinate heading)
            {
                if (heading.X == 0)
                    return new IntCoordinate(heading.Y, 0);

                return new IntCoordinate(0, -heading.X);
            }

            protected int FrontFree(IntCoordinate position, IntCoordinate heading, ref int leftFree, ref int rightFree)
            {
                int frontFree = -1;
                IntCoordinate right = GetRight(heading);
                int checkDist = 0;

                while (frontFree == -1)
                {
                    checkDist++;
                    for (int i = -leftFree; i <= rightFree; i++)
                    {
                        IntCoordinate test = position + i * right + checkDist * heading;
                        if (test.X < 0 || test.Y < 0 || test.X >= Dungeon.DimX || test.Y >= Dungeon.DimY)
                        {
                            frontFree = checkDist - 1;
                            break;
                        }

                        DungeonMakerSquareData tile = Dungeon.GetMap(test);
                        if (tile != DungeonMakerSquareData.CLOSED && tile != DungeonMakerSquareData.NJ_CLOSED)
                        {
                            frontFree = checkDist - 1;
                            break;
                        }
                    }
                }

                if (frontFree > 0)
                {
                    checkDist = leftFree;
                    bool done = false;
                    while (!done)
                    {
                        checkDist++;
                        for (int i = 1; i <= frontFree; i++)
                        {
                            IntCoordinate test = position - checkDist * right + i * heading;
                            if (test.X < 0 || test.Y < 0 || test.X >= Dungeon.DimX || test.Y >= Dungeon.DimY)
                            {
                                leftFree = checkDist - 1;
                                done = true;
                                break;
                            }

                            DungeonMakerSquareData tile = Dungeon.GetMap(test);
                            if (tile != DungeonMakerSquareData.CLOSED && tile != DungeonMakerSquareData.NJ_CLOSED)
                            {
                                leftFree = checkDist - 1;
                                done = true;
                                break;
                            }
                        }
                    }

                    checkDist = rightFree;
                    done = false;
                    while (!done)
                    {
                        checkDist++;
                        for (int i = 1; i <= frontFree; i++)
                        {
                            IntCoordinate test = position + checkDist * right + i * heading;
                            if (test.X < 0 || test.Y < 0 || test.X >= Dungeon.DimX || test.Y >= Dungeon.DimY)
                            {
                                rightFree = checkDist - 1;
                                done = true;
                                break;
                            }

                            DungeonMakerSquareData tile = Dungeon.GetMap(test);
                            if (tile != DungeonMakerSquareData.CLOSED && tile != DungeonMakerSquareData.NJ_CLOSED)
                            {
                                rightFree = checkDist - 1;
                                done = true;
                                break;
                            }
                        }
                    }
                }

                return frontFree;
            }
        }

        private sealed class Tunneler : Builder
        {
            private IntCoordinate _intDirection;
            private int _stepLength;
            private int _tunnelWidth;
            private int _straightDoubleSpawnProb;
            private int _turnDoubleSpawnProb;
            private int _changeDirProb;
            private int _makeRoomsRightProb;
            private int _makeRoomsLeftProb;
            private int _joinPreference;

            public Tunneler(
                DungeonRuntime dungeon,
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                IntCoordinate intendedDirection,
                int stepLength,
                int tunnelWidth,
                int straightDoubleSpawnProb,
                int turnDoubleSpawnProb,
                int changeDirProb,
                int makeRoomsRightProb,
                int makeRoomsLeftProb,
                int joinPreference)
                : base(dungeon, location, forward, age, maxAge, generation)
            {
                _intDirection = intendedDirection;
                _stepLength = stepLength;
                _tunnelWidth = tunnelWidth;
                _straightDoubleSpawnProb = straightDoubleSpawnProb;
                _turnDoubleSpawnProb = turnDoubleSpawnProb;
                _changeDirProb = changeDirProb;
                _makeRoomsRightProb = makeRoomsRightProb;
                _makeRoomsLeftProb = makeRoomsLeftProb;
                _joinPreference = joinPreference;
            }

            public override bool StepAhead()
            {
                if (Generation != Dungeon.ActiveGeneration)
                    return true;

                Age++;
                if (Age >= MaxAge)
                    return false;
                if (Age < 0)
                    return true;

                int leftFree = _tunnelWidth + 1;
                int rightFree = _tunnelWidth + 1;
                int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                if (frontFree == 0)
                    return false;

                IntCoordinate right = GetRight(Forward);
                IntCoordinate left = -right;
                IntCoordinate test;

                int probMS = Dungeon.GetRoomSizeProbS(_tunnelWidth, RoomSize.MEDIUM);
                int probSS = Dungeon.GetRoomSizeProbS(_tunnelWidth, RoomSize.SMALL);
                int probMB = Dungeon.GetRoomSizeProbB(_tunnelWidth, RoomSize.MEDIUM);
                int probSB = Dungeon.GetRoomSizeProbB(_tunnelWidth, RoomSize.SMALL);

                int diceRoll = Dungeon.Next100();
                RoomSize sideRoomSize = diceRoll < probSS
                    ? RoomSize.SMALL
                    : diceRoll < probSS + probMS
                        ? RoomSize.MEDIUM
                        : RoomSize.LARGE;

                RoomSize branchingRoomSize = diceRoll < probSB
                    ? RoomSize.SMALL
                    : diceRoll < probSB + probMB
                        ? RoomSize.MEDIUM
                        : RoomSize.LARGE;

                diceRoll = Dungeon.Next101();
                int roomieGeneration = Generation;
                int summedProbs = 0;
                for (int ind = 0; ind <= 10; ind++)
                {
                    summedProbs += Dungeon.GetBabyDelayProbsForGenerationR(ind);
                    if (diceRoll < summedProbs)
                    {
                        roomieGeneration = Generation + ind;
                        break;
                    }
                }

                if (frontFree < 2 * _stepLength || Age == MaxAge - 1)
                {
                    bool guaranteedClosedAhead = false;
                    bool openAhead = false;
                    bool roomAhead = false;
                    int count = 0;

                    for (int i = -_tunnelWidth; i <= _tunnelWidth; i++)
                    {
                        test = Location + (frontFree + 1) * Forward + i * right;
                        DungeonMakerSquareData tile = Dungeon.GetMap(test);
                        if (tile is DungeonMakerSquareData.OPEN or DungeonMakerSquareData.G_OPEN or DungeonMakerSquareData.IT_OPEN or DungeonMakerSquareData.IA_OPEN)
                        {
                            openAhead = true;
                            count++;
                        }
                        else if (tile is DungeonMakerSquareData.G_CLOSED or DungeonMakerSquareData.NJ_G_CLOSED)
                        {
                            guaranteedClosedAhead = true;
                            count = 0;
                        }
                        else if (tile == DungeonMakerSquareData.IR_OPEN)
                        {
                            roomAhead = true;
                            count = 0;
                        }
                        else
                        {
                            count = 0;
                        }
                    }

                    if (((Dungeon.Next101() <= _joinPreference) && (Age < MaxAge - 1 || frontFree <= Dungeon.TunnelJoinDist)) || frontFree < 5)
                    {
                        if (2 * _tunnelWidth + 1 == count)
                        {
                            BuildTunnel(frontFree, _tunnelWidth);
                            return false;
                        }

                        if (openAhead)
                        {
                            test = Location + (frontFree + 1) * Forward;
                            DungeonMakerSquareData tile = Dungeon.GetMap(test);
                            if (tile is DungeonMakerSquareData.OPEN or DungeonMakerSquareData.G_OPEN or DungeonMakerSquareData.IT_OPEN or DungeonMakerSquareData.IA_OPEN)
                            {
                                BuildTunnel(frontFree, 0);
                                return false;
                            }

                            int offset = 0;
                            for (int i = 1; i <= _tunnelWidth; i++)
                            {
                                test = Location + (frontFree + 1) * Forward + i * right;
                                tile = Dungeon.GetMap(test);
                                if (tile is DungeonMakerSquareData.OPEN or DungeonMakerSquareData.G_OPEN or DungeonMakerSquareData.IT_OPEN or DungeonMakerSquareData.IA_OPEN)
                                {
                                    offset = i;
                                    break;
                                }

                                test = Location + (frontFree + 1) * Forward - i * right;
                                tile = Dungeon.GetMap(test);
                                if (tile is DungeonMakerSquareData.OPEN or DungeonMakerSquareData.G_OPEN or DungeonMakerSquareData.IT_OPEN or DungeonMakerSquareData.IA_OPEN)
                                {
                                    offset = -i;
                                    break;
                                }
                            }

                            if (offset != 0)
                            {
                                for (int i = 1; i <= frontFree; i++)
                                    Dungeon.SetMap(Location + i * Forward + offset * right, DungeonMakerSquareData.IT_OPEN);
                                return false;
                            }
                        }

                        if (roomAhead && _tunnelWidth == 0 && frontFree > 1)
                        {
                            BuildTunnel(frontFree - 1, 0);
                            if (Forward.X == 0)
                                Dungeon.SetMap(Location + frontFree * Forward, DungeonMakerSquareData.V_DOOR);
                            else
                                Dungeon.SetMap(Location + frontFree * Forward, DungeonMakerSquareData.H_DOOR);
                            return false;
                        }

                        if (guaranteedClosedAhead && _tunnelWidth == 0)
                        {
                            int jP = Dungeon.Next101() / 10 * 10;
                            if (leftFree >= rightFree)
                            {
                                if (CanSpawnLastChanceRedirect())
                                    Dungeon.CreateTunneler(Location, -right, 0, MaxAge, Generation + 1, -right, 3, 0, 0, 0, 30, 20, 20, jP);
                            }
                            else
                            {
                                if (CanSpawnLastChanceRedirect())
                                    Dungeon.CreateTunneler(Location, right, 0, MaxAge, Generation + 1, right, 3, 0, 0, 0, 30, 20, 20, jP);
                            }
                            return false;
                        }

                        if (!openAhead && !guaranteedClosedAhead)
                        {
                            bool specialCase = true;
                            for (int i = -_tunnelWidth; i <= _tunnelWidth; i++)
                            {
                                test = Location + (frontFree + 1) * Forward + i * right;
                                if (Dungeon.GetMap(test) != DungeonMakerSquareData.CLOSED)
                                {
                                    specialCase = false;
                                    break;
                                }
                            }

                            IntCoordinate testR = Location + (frontFree + 1) * Forward + (_tunnelWidth + 1) * right;
                            IntCoordinate testL = Location + (frontFree + 1) * Forward - (_tunnelWidth + 1) * right;
                            DungeonMakerSquareData datR = Dungeon.GetMap(testR);
                            DungeonMakerSquareData datL = Dungeon.GetMap(testL);
                            if (!(IsOpenLike(datR) || IsOpenLike(datL)))
                                specialCase = false;
                            if (datR == DungeonMakerSquareData.IR_OPEN || datL == DungeonMakerSquareData.IR_OPEN)
                                specialCase = false;

                            for (int i = -_tunnelWidth - 1; i <= _tunnelWidth + 1; i++)
                            {
                                test = Location + (frontFree + 2) * Forward + i * right;
                                if (Dungeon.GetMap(test) == DungeonMakerSquareData.IR_OPEN)
                                {
                                    specialCase = false;
                                    break;
                                }
                            }

                            if (specialCase)
                            {
                                BuildTunnel(frontFree, _tunnelWidth);
                                for (int i = -_tunnelWidth; i <= _tunnelWidth; i++)
                                    Dungeon.SetMap(Location + (frontFree + 1) * Forward + i * right, DungeonMakerSquareData.IT_OPEN);

                                int fwd = frontFree + 2;
                                bool contactInNextRow = true;
                                bool rowAfterIsOk = true;
                                while (contactInNextRow && rowAfterIsOk)
                                {
                                    for (int i = -_tunnelWidth; i <= _tunnelWidth; i++)
                                    {
                                        test = Location + fwd * Forward + i * right;
                                        if (Dungeon.GetMap(test) != DungeonMakerSquareData.CLOSED)
                                        {
                                            contactInNextRow = false;
                                            break;
                                        }
                                    }

                                    testR = Location + fwd * Forward + (_tunnelWidth + 1) * right;
                                    testL = Location + fwd * Forward - (_tunnelWidth + 1) * right;
                                    datR = Dungeon.GetMap(testR);
                                    datL = Dungeon.GetMap(testL);
                                    if (!(IsOpenLike(datR) || IsOpenLike(datL)))
                                    {
                                        contactInNextRow = false;
                                        break;
                                    }

                                    if (datR == DungeonMakerSquareData.IR_OPEN || datL == DungeonMakerSquareData.IR_OPEN)
                                    {
                                        contactInNextRow = false;
                                        break;
                                    }

                                    for (int i = -_tunnelWidth; i <= _tunnelWidth; i++)
                                    {
                                        test = Location + (fwd + 1) * Forward + i * right;
                                        if (Dungeon.GetMap(test) != DungeonMakerSquareData.CLOSED)
                                            rowAfterIsOk = false;
                                    }

                                    testR = Location + (fwd + 1) * Forward + (_tunnelWidth + 1) * right;
                                    testL = Location + (fwd + 1) * Forward - (_tunnelWidth + 1) * right;
                                    datR = Dungeon.GetMap(testR);
                                    datL = Dungeon.GetMap(testL);
                                    if (!((IsOpenLike(datR) || datR == DungeonMakerSquareData.CLOSED) &&
                                          (IsOpenLike(datL) || datL == DungeonMakerSquareData.CLOSED)))
                                        rowAfterIsOk = false;
                                    if (datR == DungeonMakerSquareData.IR_OPEN || datL == DungeonMakerSquareData.IR_OPEN)
                                        rowAfterIsOk = false;

                                    bool allOpen = true;
                                    for (int i = -_tunnelWidth - 1; i <= _tunnelWidth + 1; i++)
                                    {
                                        test = Location + (fwd + 1) * Forward + i * right;
                                        DungeonMakerSquareData tile = Dungeon.GetMap(test);
                                        if (tile != DungeonMakerSquareData.IT_OPEN && tile != DungeonMakerSquareData.IA_OPEN)
                                            allOpen = false;
                                    }
                                    if (allOpen)
                                        rowAfterIsOk = true;

                                    if (contactInNextRow && rowAfterIsOk)
                                    {
                                        for (int i = -_tunnelWidth; i <= _tunnelWidth; i++)
                                            Dungeon.SetMap(Location + fwd * Forward + i * right, DungeonMakerSquareData.IT_OPEN);
                                    }

                                    fwd++;
                                }

                                return false;
                            }

                            if (_tunnelWidth == 0 && Dungeon.GetMap(Location + (frontFree + 1) * Forward) == DungeonMakerSquareData.CLOSED)
                            {
                                if (Dungeon.GetMap(Location + (frontFree + 1) * Forward + right) == DungeonMakerSquareData.IR_OPEN)
                                {
                                    Forward = -right;
                                    if (Forward == -_intDirection)
                                        Forward = _intDirection;
                                    return true;
                                }

                                if (Dungeon.GetMap(Location + (frontFree + 1) * Forward - right) == DungeonMakerSquareData.IR_OPEN)
                                {
                                    Forward = right;
                                    if (Forward == -_intDirection)
                                        Forward = _intDirection;
                                    return true;
                                }
                            }
                        }
                    }

                    if (Dungeon.WantsMoreRoomsD(branchingRoomSize))
                    {
                        int dW = 2 * _tunnelWidth;
                        if (dW < 1)
                            dW = 1;
                        Dungeon.CreateRoomie(Location, Forward, 0, 2, Generation, dW, branchingRoomSize, 0);
                    }

                    int randomJoinPreference = (Dungeon.Next101() / 10) * 10;
                    if (CanSpawnLastChanceRedirect())
                    {
                        int freeRightLeft = _tunnelWidth + 1;
                        int freeRightRight = _tunnelWidth + 1;
                        int freeForwardRight = FrontFree(Location + _tunnelWidth * right, right, ref freeRightLeft, ref freeRightRight);

                        int freeLeftLeft = _tunnelWidth + 1;
                        int freeLeftRight = _tunnelWidth + 1;
                        int freeForwardLeft = FrontFree(Location - _tunnelWidth * right, left, ref freeLeftLeft, ref freeLeftRight);

                        int freeBackLeft = _tunnelWidth + 1;
                        int freeBackRight = _tunnelWidth + 1;
                        int freeBackward = FrontFree(Location, -Forward, ref freeBackLeft, ref freeBackRight);

                        if (_tunnelWidth == 0)
                        {
                            if (IsAlreadyLastChanceProfile())
                            {
                                if (frontFree >= freeForwardRight && frontFree >= freeForwardLeft && frontFree >= freeBackward)
                                {
                                    SpawnLastChanceTunneler(Location, Forward, Generation + 1, Forward, randomJoinPreference);
                                }
                                else if (freeBackward >= freeForwardRight && freeBackward >= freeForwardLeft)
                                {
                                    SpawnLastChanceTunneler(Location, -Forward, Generation + Dungeon.LastChanceGenDelay, -Forward, randomJoinPreference);
                                }
                                else if (freeForwardRight >= freeForwardLeft || (freeForwardRight == freeForwardLeft && Dungeon.CoinFlip()))
                                {
                                    SpawnLastChanceTunneler(Location, right, Generation + Dungeon.LastChanceGenDelay, right, randomJoinPreference);
                                }
                                else
                                {
                                    SpawnLastChanceTunneler(Location, left, Generation + Dungeon.LastChanceGenDelay, left, randomJoinPreference);
                                }
                            }
                            else
                            {
                                SpawnLastChanceTunneler(Location, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference);
                            }
                        }
                        else if (guaranteedClosedAhead)
                        {
                            SpawnLastChanceTunneler(Location + _tunnelWidth * right, right, Generation + Dungeon.LastChanceGenDelay, right, randomJoinPreference);
                            SpawnLastChanceTunneler(Location - _tunnelWidth * right, left, Generation + Dungeon.LastChanceGenDelay, left, randomJoinPreference);
                        }
                        else if (roomAhead)
                        {
                            if (freeForwardRight >= freeForwardLeft || (freeForwardRight == freeForwardLeft && Dungeon.CoinFlip()))
                            {
                                SpawnLastChanceTunneler(Location + _tunnelWidth * right, right, Generation + Dungeon.LastChanceGenDelay, right, randomJoinPreference);
                                SpawnLastChanceTunneler(Location - _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference);
                            }
                            else
                            {
                                SpawnLastChanceTunneler(Location + _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference);
                                SpawnLastChanceTunneler(Location - _tunnelWidth * right, left, Generation + Dungeon.LastChanceGenDelay, left, randomJoinPreference);
                            }
                        }
                        else
                        {
                            SpawnLastChanceTunneler(Location + _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference);
                            SpawnLastChanceTunneler(Location - _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference);
                        }
                    }

                    return false;
                }

                BuildTunnel(_stepLength, _tunnelWidth);

                if (Dungeon.Next100() < _makeRoomsRightProb)
                {
                    IntCoordinate spawnPoint = Location + (_stepLength / 2 + 1) * Forward + _tunnelWidth * right;
                    int defaultWidth = _stepLength / 2 - 1;
                    if (defaultWidth < 1)
                        defaultWidth = 1;
                    Dungeon.CreateRoomie(spawnPoint, right, -1, 2, roomieGeneration, defaultWidth, sideRoomSize, 0);
                }

                if (Dungeon.Next100() < _makeRoomsLeftProb)
                {
                    IntCoordinate spawnPoint = Location + (_stepLength / 2 + 1) * Forward + _tunnelWidth * left;
                    int defaultWidth = _stepLength / 2 - 1;
                    if (defaultWidth < 1)
                        defaultWidth = 1;
                    Dungeon.CreateRoomie(spawnPoint, left, -1, 2, roomieGeneration, defaultWidth, sideRoomSize, 0);
                }

                Location += _stepLength * Forward;

                bool smallAnteRoomPossible = false;
                bool largeAnteRoomPossible = false;

                leftFree = _tunnelWidth + 2;
                rightFree = _tunnelWidth + 2;
                Dungeon.SetMap(Location, DungeonMakerSquareData.CLOSED);
                for (int m = 1; m <= _tunnelWidth; m++)
                {
                    Dungeon.SetMap(Location + m * right, DungeonMakerSquareData.CLOSED);
                    Dungeon.SetMap(Location - m * right, DungeonMakerSquareData.CLOSED);
                }

                frontFree = FrontFree(Location - Forward, Forward, ref leftFree, ref rightFree);
                if (frontFree >= 2 * _tunnelWidth + 5)
                    smallAnteRoomPossible = true;

                Dungeon.SetMap(Location, DungeonMakerSquareData.IT_OPEN);
                for (int m = 1; m <= _tunnelWidth; m++)
                {
                    Dungeon.SetMap(Location + m * right, DungeonMakerSquareData.IT_OPEN);
                    Dungeon.SetMap(Location - m * right, DungeonMakerSquareData.IT_OPEN);
                }

                leftFree = _tunnelWidth + 3;
                rightFree = _tunnelWidth + 3;
                Dungeon.SetMap(Location, DungeonMakerSquareData.CLOSED);
                for (int m = 1; m <= _tunnelWidth; m++)
                {
                    Dungeon.SetMap(Location + m * right, DungeonMakerSquareData.CLOSED);
                    Dungeon.SetMap(Location - m * right, DungeonMakerSquareData.CLOSED);
                }

                frontFree = FrontFree(Location - Forward, Forward, ref leftFree, ref rightFree);
                if (frontFree >= 2 * _tunnelWidth + 7)
                    largeAnteRoomPossible = true;

                Dungeon.SetMap(Location, DungeonMakerSquareData.IT_OPEN);
                for (int m = 1; m <= _tunnelWidth; m++)
                {
                    Dungeon.SetMap(Location + m * right, DungeonMakerSquareData.IT_OPEN);
                    Dungeon.SetMap(Location - m * right, DungeonMakerSquareData.IT_OPEN);
                }

                bool sizeUpTunnel = false;
                bool sizeDownTunnel = false;
                diceRoll = Dungeon.Next101();
                int sizeUpProb = Dungeon.GetSizeUpProb(Generation);
                int sizeDownProb = sizeUpProb + Dungeon.GetSizeDownProb(Generation);
                if (diceRoll < sizeUpProb)
                    sizeUpTunnel = true;
                else if (diceRoll < sizeDownProb)
                    sizeDownTunnel = true;

                if (sizeUpTunnel && !largeAnteRoomPossible)
                    return true;

                bool changeDirection = Dungeon.Next100() < _changeDirProb;
                bool doSpawn = changeDirection
                    ? Dungeon.Next101() < _turnDoubleSpawnProb
                    : Dungeon.Next101() < _straightDoubleSpawnProb;

                if (!changeDirection && !doSpawn)
                    return true;

                bool doSpawnRoom = false;
                if (doSpawn && Dungeon.Next101() > Dungeon.Patience)
                    doSpawnRoom = true;

                diceRoll = Dungeon.Next101();
                int babyGeneration = Generation + 1;
                summedProbs = 0;
                if (doSpawn)
                {
                    if (!sizeUpTunnel)
                    {
                        for (int ind = 0; ind <= 10; ind++)
                        {
                            summedProbs += Dungeon.GetBabyDelayProbsForGenerationT(ind);
                            if (diceRoll < summedProbs)
                            {
                                babyGeneration = Generation + ind;
                                break;
                            }
                        }
                    }
                    else
                    {
                        babyGeneration = Generation + Dungeon.SizeUpGenDelay;
                    }
                }

                int mutatedStraightDoubleSpawnProb = Dungeon.Mutate(_straightDoubleSpawnProb);
                int mutatedTurnDoubleSpawnProb = Dungeon.Mutate(_turnDoubleSpawnProb);
                int mutatedChangeDirProb = Dungeon.Mutate(_changeDirProb);
                int mutatedMakeRoomsRightProb = Dungeon.Mutate(_makeRoomsRightProb);
                int mutatedMakeRoomsLeftProb = Dungeon.Mutate(_makeRoomsLeftProb);
                int mutatedJoinPreference = Dungeon.Mutate(_joinPreference);

                bool builtAnteRoom = false;
                bool usedRight = false;
                bool usedLeft = false;
                IntCoordinate spawnPointForward;
                IntCoordinate spawnPointRight;
                IntCoordinate spawnPointLeft;

                if (sizeUpTunnel)
                {
                    if (Dungeon.Next100() < Dungeon.GetAnteRoomProb(_tunnelWidth) || doSpawn)
                    {
                        BuildAnteRoom(2 * _tunnelWidth + 5, _tunnelWidth + 2);
                        spawnPointForward = Location + (2 * _tunnelWidth + 5) * Forward;
                        spawnPointRight = Location + (_tunnelWidth + 3) * Forward + (_tunnelWidth + 2) * right;
                        spawnPointLeft = Location + (_tunnelWidth + 3) * Forward + (_tunnelWidth + 2) * left;
                        builtAnteRoom = true;
                    }
                    else
                    {
                        spawnPointForward = Location;
                        spawnPointRight = Location - _tunnelWidth * Forward + _tunnelWidth * right;
                        spawnPointLeft = Location - _tunnelWidth * Forward + _tunnelWidth * left;
                        if (Dungeon.GetMap(spawnPointRight) != DungeonMakerSquareData.IT_OPEN || Dungeon.GetMap(spawnPointLeft) != DungeonMakerSquareData.IT_OPEN)
                            return true;
                    }
                }
                else
                {
                    if (Dungeon.Next100() < Dungeon.GetAnteRoomProb(_tunnelWidth) && smallAnteRoomPossible)
                    {
                        BuildAnteRoom(2 * _tunnelWidth + 3, _tunnelWidth + 1);
                        spawnPointForward = Location + (2 * _tunnelWidth + 3) * Forward;
                        spawnPointRight = Location + (_tunnelWidth + 2) * Forward + (_tunnelWidth + 1) * right;
                        spawnPointLeft = Location + (_tunnelWidth + 2) * Forward + (_tunnelWidth + 1) * left;
                        builtAnteRoom = true;
                    }
                    else
                    {
                        spawnPointForward = Location;
                        spawnPointRight = Location - _tunnelWidth * Forward + _tunnelWidth * right;
                        spawnPointLeft = Location - _tunnelWidth * Forward + _tunnelWidth * left;
                        if (Dungeon.GetMap(spawnPointRight) != DungeonMakerSquareData.IT_OPEN || Dungeon.GetMap(spawnPointLeft) != DungeonMakerSquareData.IT_OPEN)
                            return true;
                    }
                }

                IntCoordinate oldForward = Forward;
                bool goStraight = !changeDirection;
                if (changeDirection)
                {
                    int freeRightLeft = _tunnelWidth + 1;
                    int freeRightRight = _tunnelWidth + 1;
                    int freeForwardRight = FrontFree(spawnPointRight, right, ref freeRightLeft, ref freeRightRight);

                    int freeLeftLeft = _tunnelWidth + 1;
                    int freeLeftRight = _tunnelWidth + 1;
                    int freeForwardLeft = FrontFree(spawnPointLeft, left, ref freeLeftLeft, ref freeLeftRight);

                    if ((_intDirection.X == 0 && _intDirection.Y == 0) ||
                        (_intDirection.X == Forward.X && _intDirection.Y == Forward.Y))
                    {
                        if (!sizeUpTunnel || !doSpawn)
                        {
                            if (freeForwardRight > freeForwardLeft || (freeForwardRight == freeForwardLeft && Dungeon.CoinFlip()))
                            {
                                if (freeForwardRight > 0)
                                {
                                    Location = spawnPointRight;
                                    Forward = right;
                                    usedRight = true;
                                }
                            }
                            else if (freeForwardLeft > 0)
                            {
                                Location = spawnPointLeft;
                                Forward = left;
                                usedLeft = true;
                            }
                        }
                        else
                        {
                            if (freeForwardRight < freeForwardLeft || (freeForwardRight == freeForwardLeft && Dungeon.CoinFlip()))
                            {
                                if (freeForwardRight > 0)
                                {
                                    Location = spawnPointRight;
                                    Forward = right;
                                    usedRight = true;
                                }
                            }
                            else if (freeForwardLeft > 0)
                            {
                                Location = spawnPointLeft;
                                Forward = left;
                                usedLeft = true;
                            }
                        }
                    }
                    else if (_intDirection.X == 0 || _intDirection.Y == 0)
                    {
                        Forward = _intDirection;
                        if (Forward == right)
                        {
                            if (freeForwardRight > 0)
                            {
                                usedRight = true;
                                Location = spawnPointRight;
                            }
                        }
                        else if (freeForwardLeft > 0)
                        {
                            Location = spawnPointLeft;
                            usedLeft = true;
                        }
                    }
                    else
                    {
                        Forward = _intDirection - Forward;
                        if (Forward == right)
                        {
                            if (freeForwardRight > 0)
                            {
                                usedRight = true;
                                Location = spawnPointRight;
                            }
                        }
                        else if (freeForwardLeft > 0)
                        {
                            Location = spawnPointLeft;
                            usedLeft = true;
                        }
                    }

                    if (doSpawn)
                    {
                        IntCoordinate spawnPoint = default;
                        IntCoordinate spawnDirection = default;
                        if (usedLeft)
                        {
                            spawnPoint = spawnPointRight;
                            spawnDirection = right;
                        }
                        else if (usedRight)
                        {
                            spawnPoint = spawnPointLeft;
                            spawnDirection = left;
                        }
                        else
                        {
                            goStraight = true;
                        }

                        if (!goStraight)
                        {
                            diceRoll = Dungeon.Next100();
                            if (doSpawnRoom && diceRoll < 50)
                            {
                                int defaultWidth = Math.Max(1, 2 * _tunnelWidth);
                                int roomGeneration = roomieGeneration;
                                if (builtAnteRoom)
                                    roomGeneration = Generation + (roomieGeneration - Generation) / Dungeon.GenSpeedUpOnAnteRoom;
                                Dungeon.CreateRoomie(spawnPoint, spawnDirection, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0);
                            }
                            else
                            {
                                int tunnelWidth = _tunnelWidth;
                                int stepLength = _stepLength;
                                AdjustChildTunnelParameters(sizeUpTunnel, sizeDownTunnel, ref tunnelWidth, ref stepLength);
                                Dungeon.CreateTunneler(
                                    spawnPoint,
                                    spawnDirection,
                                    0,
                                    Dungeon.GetMaxAgeT(babyGeneration),
                                    babyGeneration,
                                    spawnDirection,
                                    stepLength,
                                    tunnelWidth,
                                    mutatedStraightDoubleSpawnProb,
                                    mutatedTurnDoubleSpawnProb,
                                    mutatedChangeDirProb,
                                    mutatedMakeRoomsRightProb,
                                    mutatedMakeRoomsLeftProb,
                                    mutatedJoinPreference);
                            }

                            if (doSpawnRoom && diceRoll >= 50)
                            {
                                int defaultWidth = Math.Max(1, 2 * _tunnelWidth);
                                int roomGeneration = roomieGeneration;
                                if (builtAnteRoom)
                                    roomGeneration = Generation + (roomieGeneration - Generation) / Dungeon.GenSpeedUpOnAnteRoom;
                                Dungeon.CreateRoomie(spawnPointForward, oldForward, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0);
                            }
                            else
                            {
                                int tunnelWidth = _tunnelWidth;
                                int stepLength = _stepLength;
                                AdjustChildTunnelParameters(sizeUpTunnel, sizeDownTunnel, ref tunnelWidth, ref stepLength);
                                Dungeon.CreateTunneler(
                                    spawnPointForward,
                                    oldForward,
                                    0,
                                    Dungeon.GetMaxAgeT(babyGeneration),
                                    babyGeneration,
                                    oldForward,
                                    stepLength,
                                    tunnelWidth,
                                    mutatedStraightDoubleSpawnProb,
                                    mutatedTurnDoubleSpawnProb,
                                    mutatedChangeDirProb,
                                    mutatedMakeRoomsRightProb,
                                    mutatedMakeRoomsLeftProb,
                                    mutatedJoinPreference);
                            }
                        }
                    }
                }

                if (goStraight)
                {
                    Location = spawnPointForward;

                    if (doSpawn)
                    {
                        diceRoll = Dungeon.Next100();
                        if (doSpawnRoom && diceRoll < 50)
                        {
                            int defaultWidth = Math.Max(1, 2 * _tunnelWidth);
                            int roomGeneration = roomieGeneration;
                            if (builtAnteRoom)
                                roomGeneration = Generation + (roomieGeneration - Generation) / Dungeon.GenSpeedUpOnAnteRoom;
                            Dungeon.CreateRoomie(spawnPointRight, right, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0);
                        }
                        else
                        {
                            int tunnelWidth = _tunnelWidth;
                            int stepLength = _stepLength;
                            AdjustChildTunnelParameters(sizeUpTunnel, sizeDownTunnel, ref tunnelWidth, ref stepLength);
                            Dungeon.CreateTunneler(
                                spawnPointRight,
                                right,
                                0,
                                Dungeon.GetMaxAgeT(babyGeneration),
                                babyGeneration,
                                right,
                                stepLength,
                                tunnelWidth,
                                mutatedStraightDoubleSpawnProb,
                                mutatedTurnDoubleSpawnProb,
                                mutatedChangeDirProb,
                                mutatedMakeRoomsRightProb,
                                mutatedMakeRoomsLeftProb,
                                mutatedJoinPreference);
                        }

                        if (doSpawnRoom && diceRoll >= 50)
                        {
                            int defaultWidth = Math.Max(1, 2 * _tunnelWidth);
                            int roomGeneration = roomieGeneration;
                            if (builtAnteRoom)
                                roomGeneration = Generation + (roomieGeneration - Generation) / Dungeon.GenSpeedUpOnAnteRoom;
                            Dungeon.CreateRoomie(spawnPointLeft, left, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0);
                        }
                        else
                        {
                            int tunnelWidth = _tunnelWidth;
                            int stepLength = _stepLength;
                            AdjustChildTunnelParameters(sizeUpTunnel, sizeDownTunnel, ref tunnelWidth, ref stepLength);
                            Dungeon.CreateTunneler(
                                spawnPointLeft,
                                left,
                                0,
                                Dungeon.GetMaxAgeT(babyGeneration),
                                babyGeneration,
                                left,
                                stepLength,
                                tunnelWidth,
                                mutatedStraightDoubleSpawnProb,
                                mutatedTurnDoubleSpawnProb,
                                mutatedChangeDirProb,
                                mutatedMakeRoomsRightProb,
                                mutatedMakeRoomsLeftProb,
                                mutatedJoinPreference);
                        }
                    }
                }

                return true;
            }

            private void AdjustChildTunnelParameters(bool sizeUpTunnel, bool sizeDownTunnel, ref int tunnelWidth, ref int stepLength)
            {
                if (sizeUpTunnel)
                {
                    tunnelWidth++;
                    stepLength += 2;
                }
                else if (sizeDownTunnel)
                {
                    tunnelWidth--;
                    if (tunnelWidth < 0)
                        tunnelWidth = 0;

                    stepLength -= 2;
                    if (stepLength < 3)
                        stepLength = 3;
                }
            }

            private bool IsAlreadyLastChanceProfile()
            {
                return _makeRoomsLeftProb == Dungeon.LastChanceTunneler.MakeRoomsLeftProb
                    && _makeRoomsRightProb == Dungeon.LastChanceTunneler.MakeRoomsRightProb
                    && _changeDirProb == Dungeon.LastChanceTunneler.ChangeDirectionProb
                    && _straightDoubleSpawnProb == Dungeon.LastChanceTunneler.StraightDoubleSpawnProb
                    && _turnDoubleSpawnProb == Dungeon.LastChanceTunneler.TurnDoubleSpawnProb;
            }

            private bool CanSpawnLastChanceRedirect()
            {
                return _joinPreference != 100
                    || _makeRoomsLeftProb != Dungeon.LastChanceTunneler.MakeRoomsLeftProb
                    || _makeRoomsRightProb != Dungeon.LastChanceTunneler.MakeRoomsRightProb
                    || _changeDirProb != Dungeon.LastChanceTunneler.ChangeDirectionProb
                    || _straightDoubleSpawnProb != Dungeon.LastChanceTunneler.StraightDoubleSpawnProb
                    || _turnDoubleSpawnProb != Dungeon.LastChanceTunneler.TurnDoubleSpawnProb
                    || _tunnelWidth != 0;
            }

            private void SpawnLastChanceTunneler(IntCoordinate location, IntCoordinate forward, int generation, IntCoordinate intendedDirection, int joinPreference)
            {
                TunnelerSeed seed = Dungeon.LastChanceTunneler;
                Dungeon.CreateTunneler(
                    location,
                    forward,
                    0,
                    MaxAge,
                    generation,
                    intendedDirection,
                    seed.StepLength,
                    seed.TunnelWidth,
                    seed.StraightDoubleSpawnProb,
                    seed.TurnDoubleSpawnProb,
                    seed.ChangeDirectionProb,
                    seed.MakeRoomsRightProb,
                    seed.MakeRoomsLeftProb,
                    joinPreference);
            }

            private bool BuildAnteRoom(int length, int width)
            {
                if (length < 3 || width < 1)
                    return false;

                int leftFree = width + 1;
                int rightFree = width + 1;
                int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                if (frontFree <= length)
                    return false;

                IntCoordinate right = GetRight(Forward);
                for (int fwd = 1; fwd <= length; fwd++)
                {
                    for (int side = -width; side <= width; side++)
                        Dungeon.SetMap(Location + fwd * Forward + side * right, DungeonMakerSquareData.IA_OPEN);
                }

                if (width >= 3 && length >= 7 && Dungeon.ColumnsInTunnels)
                {
                    Dungeon.SetMap(Location + 2 * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN);
                    Dungeon.SetMap(Location + 2 * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN);
                    Dungeon.SetMap(Location + (length - 1) * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN);
                    Dungeon.SetMap(Location + (length - 1) * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN);
                }

                return true;
            }

            private bool BuildTunnel(int length, int width)
            {
                if (length < 1 || width < 0)
                    return false;

                int leftFree = width + 1;
                int rightFree = width + 1;
                int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                if (frontFree < length)
                    return false;

                IntCoordinate right = GetRight(Forward);
                for (int fwd = 1; fwd <= length; fwd++)
                {
                    for (int side = -width; side <= width; side++)
                        Dungeon.SetMap(Location + fwd * Forward + side * right, DungeonMakerSquareData.IT_OPEN);
                }

                if (width >= 3 && length >= 7 && Dungeon.ColumnsInTunnels)
                {
                    int numColumns = (length - 1) / 6;
                    for (int i = 0; i < numColumns; i++)
                    {
                        int fwd = 2 + i * 3;
                        Dungeon.SetMap(Location + fwd * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN);
                        Dungeon.SetMap(Location + fwd * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN);

                        fwd = length - 1 - i * 3;
                        Dungeon.SetMap(Location + fwd * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN);
                        Dungeon.SetMap(Location + fwd * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN);
                    }
                }

                return true;
            }

            private static bool IsOpenLike(DungeonMakerSquareData tile)
            {
                return tile is DungeonMakerSquareData.OPEN
                    or DungeonMakerSquareData.G_OPEN
                    or DungeonMakerSquareData.IT_OPEN
                    or DungeonMakerSquareData.IA_OPEN;
            }
        }

        private sealed class Roomie : Builder
        {
            private readonly int _defaultWidth;
            private readonly RoomSize _size;
            private readonly int _category;

            public Roomie(
                DungeonRuntime dungeon,
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                int defaultWidth,
                RoomSize size,
                int category)
                : base(dungeon, location, forward, age, maxAge, generation)
            {
                _defaultWidth = defaultWidth;
                _size = size;
                _category = category;
            }

            public override bool StepAhead()
            {
                if (!Dungeon.WantsMoreRoomsD(_size))
                    return false;

                if (Generation != Dungeon.ActiveGeneration)
                    return true;

                Age++;
                if (Age >= MaxAge)
                    return false;
                if (Age < 0)
                    return true;

                IntCoordinate right = GetRight(Forward);
                int sweepWidth = _defaultWidth;
                double roomAspectRatio = Dungeon.RoomAspectRatio;
                int minSize = Dungeon.GetMinRoomSize(_size);
                int maxSize = Dungeon.GetMaxRoomSize(_size);

                do
                {
                    int leftFree = sweepWidth + 1;
                    int rightFree = sweepWidth + 1;
                    int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                    if (frontFree < 4)
                        break;

                    int length = frontFree - 2;
                    double lengthDouble = length;
                    int width = leftFree + rightFree - 1;
                    double widthDouble = width;

                    if (widthDouble / lengthDouble < roomAspectRatio)
                    {
                        length = (int)(widthDouble / roomAspectRatio);
                        lengthDouble = length;
                    }

                    if (lengthDouble / widthDouble < roomAspectRatio)
                    {
                        width = (int)(lengthDouble / roomAspectRatio);
                        widthDouble = width;
                    }

                    if (widthDouble / lengthDouble < roomAspectRatio)
                        return false;

                    while (length * width > maxSize)
                    {
                        if (length > width)
                            length--;
                        else if (width > length)
                            width--;
                        else if (Dungeon.CoinFlip())
                            length--;
                        else
                            width--;
                    }

                    if (length * width >= minSize)
                    {
                        Room room = new();
                        if (leftFree <= rightFree)
                        {
                            if (2 * leftFree - 1 > width)
                            {
                                for (int fwd = 1; fwd <= length; fwd++)
                                {
                                    for (int side = width / 2; side >= width / 2 - width + 1; side--)
                                    {
                                        IntCoordinate cell = Location + (fwd + 1) * Forward + side * right;
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN);
                                        room.AddSquare(cell);
                                    }
                                }
                            }
                            else
                            {
                                for (int fwd = 1; fwd <= length; fwd++)
                                {
                                    for (int side = -leftFree + 1; side <= -leftFree + width; side++)
                                    {
                                        IntCoordinate cell = Location + (fwd + 1) * Forward + side * right;
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN);
                                        room.AddSquare(cell);
                                    }
                                }
                            }

                            Dungeon.SetMap(Location + Forward, Forward.X == 0 ? DungeonMakerSquareData.V_DOOR : DungeonMakerSquareData.H_DOOR);
                        }
                        else
                        {
                            if (2 * rightFree - 1 > width)
                            {
                                for (int fwd = 1; fwd <= length; fwd++)
                                {
                                    for (int side = -width / 2; side <= -width / 2 + width - 1; side++)
                                    {
                                        IntCoordinate cell = Location + (fwd + 1) * Forward + side * right;
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN);
                                        room.AddSquare(cell);
                                    }
                                }
                            }
                            else
                            {
                                for (int fwd = 1; fwd <= length; fwd++)
                                {
                                    for (int side = rightFree - 1; side >= rightFree - width; side--)
                                    {
                                        IntCoordinate cell = Location + (fwd + 1) * Forward + side * right;
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN);
                                        room.AddSquare(cell);
                                    }
                                }
                            }

                            Dungeon.SetMap(Location + Forward, Forward.X == 0 ? DungeonMakerSquareData.V_DOOR : DungeonMakerSquareData.H_DOOR);
                        }

                        Dungeon.BuiltRoomD(_size);
                        room.SetInDungeon(true);
                        Dungeon.AddRoom(room);
                        return false;
                    }

                    sweepWidth++;
                }
                while ((double)(FrontFreeAtCurrentSweep(sweepWidth) - 2) >= (2 * sweepWidth + 1) * roomAspectRatio);

                return false;
            }

            private int FrontFreeAtCurrentSweep(int sweepWidth)
            {
                int leftFree = sweepWidth + 1;
                int rightFree = sweepWidth + 1;
                return FrontFree(Location, Forward, ref leftFree, ref rightFree);
            }
        }

        private sealed class Room
        {
            private readonly List<IntCoordinate> _inside = new();

            public void AddSquare(IntCoordinate square)
            {
                _inside.Add(square);
            }

            public void SetInDungeon(bool inDungeon)
            {
                InDungeon = inDungeon;
            }

            public bool InDungeon { get; private set; }
        }

        private readonly struct TripleInt
        {
            public TripleInt(int small, int medium, int large)
            {
                Small = small;
                Medium = medium;
                Large = large;
            }

            public int Small { get; }
            public int Medium { get; }
            public int Large { get; }
        }

        private readonly struct TunnelerSeed
        {
            public TunnelerSeed(
                IntCoordinate location,
                IntCoordinate direction,
                IntCoordinate intendedDirection,
                int age,
                int maxAge,
                int generation,
                int stepLength,
                int tunnelWidth,
                int straightDoubleSpawnProb,
                int turnDoubleSpawnProb,
                int changeDirectionProb,
                int makeRoomsRightProb,
                int makeRoomsLeftProb,
                int joinPreference)
            {
                Location = location;
                Direction = direction;
                IntendedDirection = intendedDirection;
                Age = age;
                MaxAge = maxAge;
                Generation = generation;
                StepLength = stepLength;
                TunnelWidth = tunnelWidth;
                StraightDoubleSpawnProb = straightDoubleSpawnProb;
                TurnDoubleSpawnProb = turnDoubleSpawnProb;
                ChangeDirectionProb = changeDirectionProb;
                MakeRoomsRightProb = makeRoomsRightProb;
                MakeRoomsLeftProb = makeRoomsLeftProb;
                JoinPreference = joinPreference;
            }

            public IntCoordinate Location { get; }
            public IntCoordinate Direction { get; }
            public IntCoordinate IntendedDirection { get; }
            public int Age { get; }
            public int MaxAge { get; }
            public int Generation { get; }
            public int StepLength { get; }
            public int TunnelWidth { get; }
            public int StraightDoubleSpawnProb { get; }
            public int TurnDoubleSpawnProb { get; }
            public int ChangeDirectionProb { get; }
            public int MakeRoomsRightProb { get; }
            public int MakeRoomsLeftProb { get; }
            public int JoinPreference { get; }
        }

        private sealed class DungeonConfig
        {
            public int DimX;
            public int DimY;
            public DungeonMakerSquareData Background;
            public Direction[] Openings;
            public List<int> BabyDelayProbsTunneler;
            public List<int> BabyDelayProbsRoomie;
            public List<int> MaxAgesT;
            public List<TripleInt> RoomSizeProbS;
            public List<TripleInt> RoomSizeProbB;
            public List<int> JoinPref;
            public List<int> SizeUpProb;
            public List<int> SizeDownProb;
            public List<int> AnteRoomProb;
            public int Mutator;
            public int TunnelJoinDist;
            public int Patience;
            public int SizeUpGenDelay;
            public bool ColumnsInTunnels;
            public double RoomAspectRatio;
            public int GenSpeedUpOnAnteRoom;
            public int MinSmallRoomSize;
            public int MinMediumRoomSize;
            public int MinLargeRoomSize;
            public int MaxRoomSize;
            public int MaxSmallDungeonRooms;
            public int MaxMediumDungeonRooms;
            public int MaxLargeDungeonRooms;
            public int TunnelCrawlerGeneration;
            public TunnelerSeed LastChanceTunneler;
            public int LastChanceGenerationalDelay;
            public TunnelerSeed[] Tunnelers;

            public static DungeonConfig CreateOriginal()
            {
                return new DungeonConfig
                {
                    DimX = 80,
                    DimY = 200,
                    Background = DungeonMakerSquareData.CLOSED,
                    Openings = new[] { Direction.WE },
                    BabyDelayProbsTunneler = new List<int> { 0, 20, 30, 50, 0, 0, 0, 0, 0, 0, 0 },
                    BabyDelayProbsRoomie = new List<int> { 0, 0, 0, 50, 50, 0, 0, 0, 0, 0, 0 },
                    MaxAgesT = new List<int>
                    {
                        5, 12, 12, 15, 15, 15, 15, 15, 15, 20, 30,
                        10, 15, 10, 10, 20, 10, 10, 15, 10, 10,
                        20, 20, 20, 20, 10, 20, 10, 20, 5,
                    },
                    RoomSizeProbS = new List<TripleInt>
                    {
                        new(100, 0, 0),
                        new(100, 0, 0),
                        new(70, 30, 0),
                        new(50, 50, 0),
                        new(0, 50, 50),
                        new(0, 0, 100),
                    },
                    RoomSizeProbB = new List<TripleInt>
                    {
                        new(100, 0, 0),
                        new(50, 50, 0),
                        new(0, 30, 70),
                        new(0, 0, 100),
                    },
                    JoinPref = new List<int> { 0, 0, 10, 100, 100 },
                    SizeUpProb = new List<int> { 0, 10, 10, 10, 20, 30, 40 },
                    SizeDownProb = new List<int> { 0, 0, 20, 50, 60, 50, 60 },
                    AnteRoomProb = new List<int> { 20, 20, 50, 0, 0, 100 },
                    Mutator = 20,
                    TunnelJoinDist = 18,
                    Patience = 90,
                    SizeUpGenDelay = 1,
                    ColumnsInTunnels = false,
                    RoomAspectRatio = 0.6,
                    GenSpeedUpOnAnteRoom = 2,
                    MinSmallRoomSize = 20,
                    MinMediumRoomSize = 50,
                    MinLargeRoomSize = 100,
                    MaxRoomSize = 300,
                    MaxSmallDungeonRooms = 100,
                    MaxMediumDungeonRooms = 20,
                    MaxLargeDungeonRooms = 2,
                    TunnelCrawlerGeneration = -1,
                    LastChanceTunneler = new TunnelerSeed(
                        new IntCoordinate(0, 0),
                        new IntCoordinate(0, 0),
                        new IntCoordinate(0, 0),
                        0,
                        0,
                        0,
                        3,
                        0,
                        0,
                        30,
                        30,
                        80,
                        80,
                        100),
                    LastChanceGenerationalDelay = 4,
                    Tunnelers = new[]
                    {
                        new TunnelerSeed(
                            new IntCoordinate(40, 2),
                            TransformDirection(Direction.EA),
                            TransformDirection(Direction.EA),
                            0,
                            16,
                            0,
                            5,
                            1,
                            25,
                            50,
                            30,
                            100,
                            100,
                            100),
                    },
                };
            }
        }

        private sealed class MsRand
        {
            private uint _state;

            public MsRand(int seed)
            {
                _state = unchecked((uint)seed);
            }

            public int Next(int exclusiveMax)
            {
                if (exclusiveMax <= 1)
                    return 0;

                _state = unchecked(_state * 214013u + 2531011u);
                return (int)((_state >> 16) & 0x7fff) % exclusiveMax;
            }
        }

        private readonly struct IntCoordinate : IEquatable<IntCoordinate>
        {
            public IntCoordinate(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public bool Equals(IntCoordinate other)
            {
                return X == other.X && Y == other.Y;
            }

            public override bool Equals(object obj)
            {
                return obj is IntCoordinate other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X, Y);
            }

            public static IntCoordinate operator +(IntCoordinate lhs, IntCoordinate rhs)
            {
                return new IntCoordinate(lhs.X + rhs.X, lhs.Y + rhs.Y);
            }

            public static IntCoordinate operator -(IntCoordinate lhs, IntCoordinate rhs)
            {
                return new IntCoordinate(lhs.X - rhs.X, lhs.Y - rhs.Y);
            }

            public static IntCoordinate operator -(IntCoordinate value)
            {
                return new IntCoordinate(-value.X, -value.Y);
            }

            public static IntCoordinate operator *(int lhs, IntCoordinate rhs)
            {
                return new IntCoordinate(lhs * rhs.X, lhs * rhs.Y);
            }

            public static bool operator ==(IntCoordinate lhs, IntCoordinate rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(IntCoordinate lhs, IntCoordinate rhs)
            {
                return !lhs.Equals(rhs);
            }
        }

        private enum Direction
        {
            NO = 0,
            EA = 1,
            SO = 2,
            WE = 3,
            NE = 4,
            SE = 5,
            SW = 6,
            NW = 7,
            XX = 8,
        }

        private enum RoomSize
        {
            SMALL,
            MEDIUM,
            LARGE,
        }

        private static IntCoordinate TransformDirection(Direction direction)
        {
            return direction switch
            {
                Direction.NO => new IntCoordinate(-1, 0),
                Direction.EA => new IntCoordinate(0, 1),
                Direction.SO => new IntCoordinate(1, 0),
                Direction.WE => new IntCoordinate(0, -1),
                Direction.NE => new IntCoordinate(-1, 1),
                Direction.SE => new IntCoordinate(1, 1),
                Direction.SW => new IntCoordinate(1, -1),
                Direction.NW => new IntCoordinate(-1, -1),
                _ => new IntCoordinate(0, 0),
            };
        }
    }
}
