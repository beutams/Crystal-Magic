using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo
{
    public enum DungeonMakerSquareData : byte
    {
        // 普通开放地块。
        OPEN = 0,
        // 普通封闭地块。
        CLOSED = 1,
        // 边界上的开放地块，通常用于入口/出口开口。
        G_OPEN = 2,
        // 边界上的封闭地块。
        G_CLOSED = 3,
        // 非连接节点用的开放地块。
        NJ_OPEN = 4,
        // 非连接节点用的封闭地块。
        NJ_CLOSED = 5,
        // 边界上的非连接节点开放地块。
        NJ_G_OPEN = 6,
        // 边界上的非连接节点封闭地块。
        NJ_G_CLOSED = 7,
        // Roomie 生成的房间内部地块。
        IR_OPEN = 8,
        // Tunneler 挖出的隧道/走廊地块。
        IT_OPEN = 9,
        // 前厅（anteroom）地块。
        IA_OPEN = 10,
        // 水平门。
        H_DOOR = 11,
        // 垂直门。
        V_DOOR = 12,
        // 怪物标记 1。
        MOB1 = 13,
        // 怪物标记 2。
        MOB2 = 14,
        // 怪物标记 3。
        MOB3 = 15,
        // 宝物标记 1。
        TREAS1 = 16,
        // 宝物标记 2。
        TREAS2 = 17,
        // 宝物标记 3。
        TREAS3 = 18,
        // 柱子/障碍点。
        COLUMN = 19,
    }

    public enum DungeonMakerSkeletonKind : byte
    {
        None = 0,
        Corridor = 1,
        Anchor = 2,
    }

    public enum DungeonMakerRegionKind : byte
    {
        Corridor = 0,
        Room = 1,
        AnteRoom = 2,
    }

    public enum DungeonMakerRoomSizeClass : byte
    {
        None = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
    }

    public enum DungeonMakerSpecialRoomRole : byte
    {
        None = 0,
        Start = 1,
        NextLevel = 2,
    }

    public enum DungeonMakerTileOrigin : byte
    {
        None = 0,
        BoundaryClosed = 1,
        TunnelCarve = 2,
        TunnelOffsetJoin = 3,
        TunnelParallelJoinLead = 4,
        TunnelParallelJoinExtend = 5,
        TunnelProbeRestore = 6,
        AnteRoomCarve = 7,
        RoomCarve = 8,
        DoorPlacement = 9,
        ColumnPlacement = 10,
        MonsterPlacement = 11,
        TreasurePlacement = 12,
    }

    [Serializable]
    public sealed class DungeonMakerRegion
    {
        public DungeonMakerRegion(
            int id,
            DungeonMakerRegionKind kind,
            int[] tileIndices,
            DungeonMakerRoomSizeClass roomSizeClass = DungeonMakerRoomSizeClass.None,
            DungeonMakerSpecialRoomRole specialRoomRole = DungeonMakerSpecialRoomRole.None,
            int visualStyleId = -1,
            int corridorWidth = 0)
        {
            Id = id;
            Kind = kind;
            TileIndices = tileIndices;
            RoomSizeClass = roomSizeClass;
            SpecialRoomRole = specialRoomRole;
            VisualStyleId = visualStyleId;
            CorridorWidth = corridorWidth;
        }

        public int Id { get; }
        public DungeonMakerRegionKind Kind { get; }
        public int[] TileIndices { get; }
        public DungeonMakerRoomSizeClass RoomSizeClass { get; }
        public DungeonMakerSpecialRoomRole SpecialRoomRole { get; }
        public int VisualStyleId { get; }
        public int CorridorWidth { get; }
    }

    [Serializable]
    public sealed class DungeonMakerTunnelingStats
    {
        public int SourceWidth;
        public int SourceHeight;
        public int DisplayWidth;
        public int DisplayHeight;
        public int TotalCells;
        public int TotalRooms;
        public int SmallRooms;
        public int MediumRooms;
        public int LargeRooms;
        public int AnteRooms;
        public int OpenTiles;
        public int ClosedTiles;
        public int BoundaryOpenTiles;
        public int BoundaryClosedTiles;
        public int NonJoinOpenTiles;
        public int NonJoinClosedTiles;
        public int NonJoinBoundaryOpenTiles;
        public int NonJoinBoundaryClosedTiles;
        public int RoomTiles;
        public int TunnelTiles;
        public int AnteRoomTiles;
        public int HorizontalDoorTiles;
        public int VerticalDoorTiles;
        public int MobTiles;
        public int TreasureTiles;
        public int ColumnTiles;
        public int WalkableTiles;
        public int BlockedTiles;
    }

    [Serializable]
    public sealed class DungeonMakerSkeletonSegment
    {
        public DungeonMakerSkeletonSegment(int id, int builderId, Vector2Int start, Vector2Int end, int[] ownedTileIndices)
        {
            Id = id;
            BuilderId = builderId;
            Start = start;
            End = end;
            OwnedTileIndices = ownedTileIndices;
        }

        public int Id { get; }
        public int BuilderId { get; }
        public Vector2Int Start { get; }
        public Vector2Int End { get; }
        public int[] OwnedTileIndices { get; }
    }

    [Serializable]
    public sealed class DungeonMakerSkeletonLink
    {
        public DungeonMakerSkeletonLink(int fromSegmentId, int toSegmentId, Vector2Int from, Vector2Int to)
        {
            FromSegmentId = fromSegmentId;
            ToSegmentId = toSegmentId;
            From = from;
            To = to;
        }

        public int FromSegmentId { get; }
        public int ToSegmentId { get; }
        public Vector2Int From { get; }
        public Vector2Int To { get; }
    }

    [Serializable]
    public sealed class DungeonMakerSkeletonAttachment
    {
        public DungeonMakerSkeletonAttachment(int segmentId)
        {
            SegmentId = segmentId;
        }

        public int SegmentId { get; }
    }

    // 生成完成后的只读结果对象。
    // 只保留地图数组和显示坐标转换，不承担任何运行时生成职责。
    public sealed class DungeonMakerTunnelingResult
    {
        private readonly DungeonMakerSquareData[] _map;
        private readonly DungeonMakerTileOrigin[] _originMap;
        private readonly DungeonMakerSkeletonKind[] _skeletonMap;
        private readonly DungeonMakerRegion[] _regions;
        private readonly DungeonMakerSkeletonSegment[] _skeletonSegments;
        private readonly DungeonMakerSkeletonLink[] _skeletonLinks;
        private readonly DungeonMakerSkeletonAttachment[] _skeletonAttachments;

        public DungeonMakerTunnelingResult(
            int sourceWidth,
            int sourceHeight,
            int seed,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] originMap,
            DungeonMakerSkeletonKind[] skeletonMap,
            DungeonMakerRegion[] regions,
            DungeonMakerSkeletonSegment[] skeletonSegments,
            DungeonMakerSkeletonLink[] skeletonLinks,
            DungeonMakerSkeletonAttachment[] skeletonAttachments,
            DungeonMakerTunnelingStats stats)
        {
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            Seed = seed;
            _map = map;
            _originMap = originMap;
            _skeletonMap = skeletonMap;
            _regions = regions;
            _skeletonSegments = skeletonSegments;
            _skeletonLinks = skeletonLinks;
            _skeletonAttachments = skeletonAttachments;
            Stats = stats;
        }

        public int SourceWidth { get; }
        public int SourceHeight { get; }
        public int DisplayWidth => SourceHeight;
        public int DisplayHeight => SourceWidth;
        public int Seed { get; }
        public DungeonMakerTunnelingStats Stats { get; }
        public IReadOnlyList<DungeonMakerRegion> Regions => _regions;
        public IReadOnlyList<DungeonMakerSkeletonSegment> SkeletonSegments => _skeletonSegments;
        public IReadOnlyList<DungeonMakerSkeletonLink> SkeletonLinks => _skeletonLinks;
        public IReadOnlyList<DungeonMakerSkeletonAttachment> SkeletonAttachments => _skeletonAttachments;

        public DungeonMakerSquareData GetSourceTile(int x, int y)
        {
            return _map[x * SourceHeight + y];
        }

        public DungeonMakerSquareData GetDisplayTile(int x, int y)
        {
            return GetSourceTile(y, x);
        }

        public DungeonMakerTileOrigin GetSourceOrigin(int x, int y)
        {
            return _originMap[x * SourceHeight + y];
        }

        public DungeonMakerTileOrigin GetDisplayOrigin(int x, int y)
        {
            return GetSourceOrigin(y, x);
        }

        public DungeonMakerSkeletonKind GetSourceSkeletonKind(int x, int y)
        {
            return _skeletonMap[x * SourceHeight + y];
        }
    }

    // 纯生成入口。外部只要给一个种子，就能得到一份完整地图结果。
    internal static class DungeonMakerTunnelingGenerator
    {
        public const int DefaultSeed = 1015776839;
        public const int DefaultTimeoutMs = 3000;

        internal readonly struct StepResult
        {
            public StepResult(bool changedMap, bool advancedGeneration, bool hasMoreBuilders, int activeGeneration, int liveBuilderCount)
            {
                ChangedMap = changedMap;
                AdvancedGeneration = advancedGeneration;
                HasMoreBuilders = hasMoreBuilders;
                ActiveGeneration = activeGeneration;
                LiveBuilderCount = liveBuilderCount;
            }

            public bool ChangedMap { get; }
            public bool AdvancedGeneration { get; }
            public bool HasMoreBuilders { get; }
            public int ActiveGeneration { get; }
            public int LiveBuilderCount { get; }
        }

        internal sealed class Stepper
        {
            private readonly DungeonRuntime _runtime;

            public Stepper(int seed, DungeonMakerTunnelingConfig config = null, int timeoutMs = DefaultTimeoutMs)
            {
                _runtime = new DungeonRuntime(seed == 0 ? DefaultSeed : seed, config ?? DungeonMakerTunnelingConfig.CreateDefault(), timeoutMs);
            }

            public int Seed => _runtime.Seed;
            public bool HasMoreBuilders => _runtime.HasAnyBuilders();
            public int ActiveGeneration => _runtime.ActiveGeneration;
            public int LiveBuilderCount => _runtime.LiveBuilderCount;

            public StepResult StepOnce()
            {
                return _runtime.StepOnce();
            }

            public DungeonMakerTunnelingResult BuildResult()
            {
                return _runtime.BuildResult();
            }
        }

        // 生成流程入口：
        // 1. 创建运行时上下文
        // 2. 执行代际 Builder 系统
        // 3. 导出最终地图
        public static DungeonMakerTunnelingResult Generate(int seed, DungeonMakerTunnelingConfig config = null, int timeoutMs = DefaultTimeoutMs)
        {
            DungeonRuntime runtime = new(seed == 0 ? DefaultSeed : seed, config ?? DungeonMakerTunnelingConfig.CreateDefault(), timeoutMs);
            runtime.Generate();
            return runtime.BuildResult();
        }

        // 真正的运行时生成上下文。
        // 地图、Builder、随机数、配置和代际推进全部集中在这里。
        private sealed class DungeonRuntime
        {
            internal enum TunnelerSpawnKind
            {
                Normal,
                Redirect,
                LastChance,
            }

            private readonly MsRand _random;
            private readonly List<Builder> _builders = new();
            private readonly List<Room> _rooms = new();
            private readonly List<DungeonMakerRegion> _regions = new();
            private readonly List<DungeonMakerSkeletonSegment> _skeletonSegments = new();
            private readonly List<DungeonMakerSkeletonLink> _skeletonLinks = new();
            private readonly HashSet<int> _valuableSkeletonSegmentIds = new();
            private readonly Dictionary<int, int> _builderCurrentSkeletonSegmentIds = new();
            private readonly Dictionary<int, int> _builderVisualStyleIds = new();
            private readonly DungeonConfig _config;
            private readonly Stopwatch _timeoutWatch = new();
            private readonly int _timeoutMs;

            // 一维地图数组，索引规则是 x * DimY + y。
            private DungeonMakerSquareData[] _map;
            private DungeonMakerTileOrigin[] _originMap;
            private DungeonMakerSkeletonKind[] _skeletonMap;
            // 当前 iteration 是否真的改动过地图。
            private bool _changedThisIteration;
            // 当前正在工作的代数。
            private int _activeGeneration;
            // 已经生成出来的小/中/大房间数量。
            private int _currentSmallRooms;
            private int _currentMediumRooms;
            private int _currentLargeRooms;
            private int _totalNormalTunnelersCreated;
            private int _totalRedirectTunnelersCreated;
            private int _totalLastChanceTunnelersCreated;
            private int _totalRoomiesCreated;
            private int _totalAnteRoomsBuilt;
            private int _peakLiveBuilders;
            private int _nextBuilderId = 1;
            private int _nextRegionId = 1;
            private int _nextSkeletonSegmentId = 1;

            public DungeonRuntime(int seed, DungeonMakerTunnelingConfig config, int timeoutMs)
            {
                Seed = seed;
                _random = new MsRand(seed);
                _config = DungeonConfig.From(config);
                _timeoutMs = timeoutMs;
                InitFromConfig();
            }

            public int Seed { get; }
            public int LiveBuilderCount => CountLiveBuilders();

            // 主生成循环：
            // - 当前代不停迭代，直到这一轮已经没人再改图
            // - 然后推进代际
            // - 所有 Builder 都耗尽时结束
            public void Generate()
            {
                _timeoutWatch.Restart();
                while (true)
                {
                    GuardTimeout();
                    while (MakeIteration())
                    {
                        GuardTimeout();
                    }

                    if (!AdvanceGeneration())
                        break;
                }

                if (_config.TunnelCrawlerGeneration < 0 || _activeGeneration < _config.TunnelCrawlerGeneration)
                {
                    while (true)
                    {
                        GuardTimeout();
                        while (MakeIteration())
                        {
                            GuardTimeout();
                        }

                        if (!AdvanceGeneration())
                            break;
                    }
                }

                _timeoutWatch.Stop();
            }

            public bool HasAnyBuilders()
            {
                return CountLiveBuilders() > 0;
            }

            public StepResult StepOnce()
            {
                _timeoutWatch.Restart();
                try
                {
                    GuardTimeout();
                    bool changed = MakeIteration();
                    bool advancedGeneration = false;
                    if (!changed)
                        advancedGeneration = AdvanceGeneration();

                    bool hasMoreBuilders = HasAnyBuilders();
                    return new StepResult(changed, advancedGeneration, hasMoreBuilders, _activeGeneration, CountLiveBuilders());
                }
                finally
                {
                    _timeoutWatch.Stop();
                }
            }

            // 复制地图数据并导出结果，避免外部直接持有内部运行时数组。
            public DungeonMakerTunnelingResult BuildResult()
            {
                DungeonMakerSquareData[] copiedMap = new DungeonMakerSquareData[_map.Length];
                DungeonMakerTileOrigin[] copiedOriginMap = new DungeonMakerTileOrigin[_originMap.Length];
                DungeonMakerSkeletonKind[] copiedSkeletonMap = new DungeonMakerSkeletonKind[_skeletonMap.Length];
                Array.Copy(_map, copiedMap, _map.Length);
                Array.Copy(_originMap, copiedOriginMap, _originMap.Length);
                Array.Copy(_skeletonMap, copiedSkeletonMap, _skeletonMap.Length);
                DungeonMakerRegion[] copiedRegions = new DungeonMakerRegion[_regions.Count];
                for (int i = 0; i < _regions.Count; i++)
                {
                    DungeonMakerRegion region = _regions[i];
                    int[] tileIndices = new int[region.TileIndices.Length];
                    Array.Copy(region.TileIndices, tileIndices, tileIndices.Length);
                    copiedRegions[i] = new DungeonMakerRegion(
                        region.Id,
                        region.Kind,
                        tileIndices,
                        region.RoomSizeClass,
                        region.SpecialRoomRole,
                        region.VisualStyleId,
                        region.CorridorWidth);
                }

                DungeonMakerSkeletonSegment[] copiedSkeletonSegments = new DungeonMakerSkeletonSegment[_skeletonSegments.Count];
                for (int i = 0; i < _skeletonSegments.Count; i++)
                {
                    DungeonMakerSkeletonSegment segment = _skeletonSegments[i];
                    int[] ownedTileIndices = new int[segment.OwnedTileIndices.Length];
                    Array.Copy(segment.OwnedTileIndices, ownedTileIndices, ownedTileIndices.Length);
                    copiedSkeletonSegments[i] = new DungeonMakerSkeletonSegment(segment.Id, segment.BuilderId, segment.Start, segment.End, ownedTileIndices);
                }

                DungeonMakerSkeletonLink[] copiedSkeletonLinks = new DungeonMakerSkeletonLink[_skeletonLinks.Count];
                for (int i = 0; i < _skeletonLinks.Count; i++)
                {
                    DungeonMakerSkeletonLink link = _skeletonLinks[i];
                    copiedSkeletonLinks[i] = new DungeonMakerSkeletonLink(link.FromSegmentId, link.ToSegmentId, link.From, link.To);
                }
                DungeonMakerSkeletonAttachment[] copiedSkeletonAttachments = new DungeonMakerSkeletonAttachment[_valuableSkeletonSegmentIds.Count];
                int attachmentIndex = 0;
                foreach (int segmentId in _valuableSkeletonSegmentIds)
                    copiedSkeletonAttachments[attachmentIndex++] = new DungeonMakerSkeletonAttachment(segmentId);

                return new DungeonMakerTunnelingResult(_config.DimX, _config.DimY, Seed, copiedMap, copiedOriginMap, copiedSkeletonMap, copiedRegions, copiedSkeletonSegments, copiedSkeletonLinks, copiedSkeletonAttachments, BuildStats(copiedMap));
            }

            private DungeonMakerTunnelingStats BuildStats(DungeonMakerSquareData[] map)
            {
                DungeonMakerTunnelingStats stats = new()
                {
                    SourceWidth = _config.DimX,
                    SourceHeight = _config.DimY,
                    DisplayWidth = _config.DimY,
                    DisplayHeight = _config.DimX,
                    TotalCells = map.Length,
                    SmallRooms = _currentSmallRooms,
                    MediumRooms = _currentMediumRooms,
                    LargeRooms = _currentLargeRooms,
                    TotalRooms = _currentSmallRooms + _currentMediumRooms + _currentLargeRooms,
                    AnteRooms = _totalAnteRoomsBuilt,
                };

                for (int i = 0; i < map.Length; i++)
                {
                    switch (map[i])
                    {
                        case DungeonMakerSquareData.OPEN:
                            stats.OpenTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.CLOSED:
                            stats.ClosedTiles++;
                            stats.BlockedTiles++;
                            break;
                        case DungeonMakerSquareData.G_OPEN:
                            stats.BoundaryOpenTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.G_CLOSED:
                            stats.BoundaryClosedTiles++;
                            stats.BlockedTiles++;
                            break;
                        case DungeonMakerSquareData.NJ_OPEN:
                            stats.NonJoinOpenTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.NJ_CLOSED:
                            stats.NonJoinClosedTiles++;
                            stats.BlockedTiles++;
                            break;
                        case DungeonMakerSquareData.NJ_G_OPEN:
                            stats.NonJoinBoundaryOpenTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.NJ_G_CLOSED:
                            stats.NonJoinBoundaryClosedTiles++;
                            stats.BlockedTiles++;
                            break;
                        case DungeonMakerSquareData.IR_OPEN:
                            stats.RoomTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.IT_OPEN:
                            stats.TunnelTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.IA_OPEN:
                            stats.AnteRoomTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.H_DOOR:
                            stats.HorizontalDoorTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.V_DOOR:
                            stats.VerticalDoorTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.MOB1:
                        case DungeonMakerSquareData.MOB2:
                        case DungeonMakerSquareData.MOB3:
                            stats.MobTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.TREAS1:
                        case DungeonMakerSquareData.TREAS2:
                        case DungeonMakerSquareData.TREAS3:
                            stats.TreasureTiles++;
                            stats.WalkableTiles++;
                            break;
                        case DungeonMakerSquareData.COLUMN:
                            stats.ColumnTiles++;
                            stats.BlockedTiles++;
                            break;
                    }
                }

                return stats;
            }

            // 按配置初始化地图：
            // - 背景填充
            // - 边界封口
            // - 初始 Tunneler 投放
            private void InitFromConfig()
            {
                _activeGeneration = 0;
                _currentSmallRooms = 0;
                _currentMediumRooms = 0;
                _currentLargeRooms = 0;
                _totalNormalTunnelersCreated = 0;
                _totalRedirectTunnelersCreated = 0;
                _totalLastChanceTunnelersCreated = 0;
                _totalRoomiesCreated = 0;
                _totalAnteRoomsBuilt = 0;
                _peakLiveBuilders = 0;

                _map = new DungeonMakerSquareData[_config.DimX * _config.DimY];
                _originMap = new DungeonMakerTileOrigin[_config.DimX * _config.DimY];
                _skeletonMap = new DungeonMakerSkeletonKind[_config.DimX * _config.DimY];
                for (int i = 0; i < _map.Length; i++)
                    _map[i] = _config.Background;

                SetRect(0, 0, _config.DimX - 1, 0, DungeonMakerSquareData.G_CLOSED, DungeonMakerTileOrigin.BoundaryClosed);
                SetRect(0, 0, 0, _config.DimY - 1, DungeonMakerSquareData.G_CLOSED, DungeonMakerTileOrigin.BoundaryClosed);
                SetRect(_config.DimX - 1, 0, _config.DimX - 1, _config.DimY - 1, DungeonMakerSquareData.G_CLOSED, DungeonMakerTileOrigin.BoundaryClosed);
                SetRect(0, _config.DimY - 1, _config.DimX - 1, _config.DimY - 1, DungeonMakerSquareData.G_CLOSED, DungeonMakerTileOrigin.BoundaryClosed);

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
                        seed.JoinPreference,
                        spawnKind: TunnelerSpawnKind.Normal);
                }
            }

            // 一次 iteration = 所有 Builder 各执行一次 StepAhead。
            // 只要这轮里有人改了地图，就返回 true。
            private bool MakeIteration()
            {
                GuardTimeout();
                _changedThisIteration = false;

                for (int i = 0; i < _builders.Count; i++)
                {
                    GuardTimeout();
                    Builder builder = _builders[i];
                    if (builder == null)
                        continue;

                    if (!builder.StepAhead())
                        _builders[i] = null;
                }

                return _changedThisIteration;
            }

            // 代际推进规则：
            // - 当前代还有激活 Builder：不能切代
            // - 当前代只剩休眠 Builder：快进到最近的激活时刻
            // - 当前代彻底没人：activeGeneration++
            private bool AdvanceGeneration()
            {
                GuardTimeout();
                bool thereAreBuilders = false;
                int highestNegativeAge = 0;

                for (int i = 0; i < _builders.Count; i++)
                {
                    GuardTimeout();
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
                    if (_activeGeneration > 0 && _activeGeneration % 5000 == 0)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[DungeonMakerTunnelingGenerator] generation={_activeGeneration}, liveBuilders={CountLiveBuilders()}, peakBuilders={_peakLiveBuilders}, normalTunnelers={_totalNormalTunnelersCreated}, redirectTunnelers={_totalRedirectTunnelersCreated}, lastChanceTunnelers={_totalLastChanceTunnelersCreated}, roomies={_totalRoomiesCreated}");
                    }
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

            internal void GuardTimeout()
            {
                if (_timeoutMs <= 0)
                    return;

                if (_timeoutWatch.ElapsedMilliseconds <= _timeoutMs)
                    return;

                throw new TimeoutException(
                    $"DungeonMaker tunneling generation timed out after {_timeoutMs} ms (seed={Seed}, generation={_activeGeneration}, liveBuilders={CountLiveBuilders()}, peakBuilders={_peakLiveBuilders}, normalTunnelers={_totalNormalTunnelersCreated}, redirectTunnelers={_totalRedirectTunnelersCreated}, lastChanceTunnelers={_totalLastChanceTunnelersCreated}, roomies={_totalRoomiesCreated}).");
            }

            private int CountLiveBuilders()
            {
                int count = 0;
                for (int i = 0; i < _builders.Count; i++)
                {
                    if (_builders[i] != null)
                        count++;
                }

                return count;
            }

            // 创建一个新的 Tunneler，并加入 Builder 池。
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
                int joinPreference,
                int parentBuilderId = -1,
                bool hasParentAttachPoint = false,
                IntCoordinate parentAttachPoint = default,
                TunnelerSpawnKind spawnKind = TunnelerSpawnKind.Normal)
            {
                switch (spawnKind)
                {
                    case TunnelerSpawnKind.Redirect:
                        _totalRedirectTunnelersCreated++;
                        break;
                    case TunnelerSpawnKind.LastChance:
                        _totalLastChanceTunnelersCreated++;
                        break;
                    default:
                        _totalNormalTunnelersCreated++;
                        break;
                }

                if (hasParentAttachPoint)
                {
                    CommitSkeletonConnection(parentAttachPoint, location);
                    MarkCorridorSkeleton(location);
                    RegisterSkeletonLink(-1, -1, parentAttachPoint, location);
                }

                tunnelWidth = ClampTunnelHalfWidth(tunnelWidth);

                int builderId = AllocateBuilderId();
                int visualStyleId = ResolveChildVisualStyle(parentBuilderId, builderId);
                _builderVisualStyleIds[builderId] = visualStyleId;

                AddBuilder(new Tunneler(
                    this,
                    location,
                    forward,
                    age,
                    maxAge,
                    generation,
                    builderId,
                    parentBuilderId,
                    hasParentAttachPoint,
                    parentAttachPoint,
                    intendedDirection,
                    stepLength,
                    tunnelWidth,
                    straightDoubleSpawnProb,
                    turnDoubleSpawnProb,
                    changeDirectionProb,
                    makeRoomsRightProb,
                    makeRoomsLeftProb,
                    joinPreference,
                    visualStyleId));
            }

            // 创建一个新的 Roomie，并加入 Builder 池。
            internal void CreateRoomie(
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                int defaultWidth,
                RoomSize size,
                int category,
                int parentBuilderId = -1,
                bool hasParentAttachPoint = false,
                IntCoordinate parentAttachPoint = default)
            {
                _totalRoomiesCreated++;

                if (hasParentAttachPoint)
                {
                    CommitSkeletonConnection(parentAttachPoint, location);
                    MarkAnchorSkeleton(location);
                }

                int builderId = AllocateBuilderId();
                int visualStyleId = GetInheritedVisualStyle(parentBuilderId);
                _builderVisualStyleIds[builderId] = visualStyleId;

                AddBuilder(new Roomie(
                    this,
                    location,
                    forward,
                    age,
                    maxAge,
                    generation,
                    builderId,
                    parentBuilderId,
                    hasParentAttachPoint,
                    parentAttachPoint,
                    defaultWidth,
                    size,
                    category,
                    visualStyleId));
            }

            // 优先复用空槽位，避免频繁收缩/扩容 Builder 列表。
            private void AddBuilder(Builder builder)
            {
                for (int i = 0; i < _builders.Count; i++)
                {
                    if (_builders[i] == null)
                    {
                        _builders[i] = builder;
                        if (CountLiveBuilders() > _peakLiveBuilders)
                            _peakLiveBuilders = CountLiveBuilders();
                        return;
                    }
                }

                _builders.Add(builder);
                if (_builders.Count > _peakLiveBuilders)
                    _peakLiveBuilders = _builders.Count;
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
            public int MinRoomLength => Math.Max(1, _config.MinRoomLength);
            public int MaxRoomLength => Math.Max(MinRoomLength, _config.MaxRoomLength);
            public int MinRoomWidth => Math.Max(1, _config.MinRoomWidth);
            public int MaxRoomWidth => Math.Max(MinRoomWidth, _config.MaxRoomWidth);
            public int LastChanceGenDelay => _config.LastChanceGenerationalDelay;
            public TunnelerSeed LastChanceTunneler => _config.LastChanceTunneler;
            public void RegisterAnteRoomBuilt() => _totalAnteRoomsBuilt++;

            private int AllocateBuilderId()
            {
                return _nextBuilderId++;
            }

            // 读取“侧向房”的大小概率。
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

            // 读取“分叉房”的大小概率。
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

            // 读取 Roomie 子代延迟表。
            public int GetBabyDelayProbsForGenerationR(int generation)
            {
                return generation is >= 0 and <= 10 ? _config.BabyDelayProbsRoomie[generation] : 0;
            }

            // 读取 Tunneler 子代延迟表。
            public int GetBabyDelayProbsForGenerationT(int generation)
            {
                return generation is >= 0 and <= 10 ? _config.BabyDelayProbsTunneler[generation] : 0;
            }

            // 读取某一代 Tunneler 的寿命上限。
            public int GetMaxAgeT(int generation)
            {
                return generation >= _config.MaxAgesT.Count
                    ? _config.MaxAgesT[_config.MaxAgesT.Count - 1]
                    : _config.MaxAgesT[generation];
            }

            // 读取当前隧道宽度下生成前厅的概率。
            public int GetAnteRoomProb(int tunnelWidth)
            {
                return tunnelWidth >= _config.AnteRoomProb.Count ? 100 : _config.AnteRoomProb[tunnelWidth];
            }

            public int ClampTunnelHalfWidth(int tunnelHalfWidth)
            {
                int minWidth = NormalizeOddSize(_config.MinCorridorWidth, 1);
                int maxWidth = Math.Max(minWidth, NormalizeOddSize(_config.MaxCorridorWidth, 1));
                return Math.Clamp(tunnelHalfWidth, (minWidth - 1) / 2, (maxWidth - 1) / 2);
            }

            public int GetAnteRoomSideLength(int tunnelHalfWidth)
            {
                int minSide = NormalizeOddSize(_config.MinAnteRoomSide, 3);
                int maxSide = Math.Max(minSide, NormalizeOddSize(_config.MaxAnteRoomSide, 3));
                return Math.Clamp(2 * tunnelHalfWidth + 5, minSide, maxSide);
            }

            private static int NormalizeOddSize(int value, int minimum)
            {
                int normalized = Math.Max(minimum, value);
                return normalized % 2 == 0 ? normalized + 1 : normalized;
            }

            // 读取当前代的变宽概率。
            public int GetSizeUpProb(int generation)
            {
                return generation >= _config.SizeUpProb.Count
                    ? _config.SizeUpProb[_config.SizeUpProb.Count - 1]
                    : _config.SizeUpProb[generation];
            }

            // 读取当前代的变窄概率。
            public int GetSizeDownProb(int generation)
            {
                return generation >= _config.SizeDownProb.Count
                    ? _config.SizeDownProb[_config.SizeDownProb.Count - 1]
                    : _config.SizeDownProb[generation];
            }

            // 读取房型的最小面积。
            public int GetMinRoomSize(RoomSize roomSize)
            {
                return roomSize switch
                {
                    RoomSize.SMALL => _config.MinSmallRoomSize,
                    RoomSize.MEDIUM => _config.MinMediumRoomSize,
                    _ => _config.MinLargeRoomSize,
                };
            }

            // 读取房型的最大面积上界。
            public int GetMaxRoomSize(RoomSize roomSize)
            {
                return roomSize switch
                {
                    RoomSize.SMALL => _config.MinMediumRoomSize - 1,
                    RoomSize.MEDIUM => _config.MinLargeRoomSize - 1,
                    _ => _config.MaxRoomSize - 1,
                };
            }

            // 检查当前地图还需不需要这种大小的房间。
            public bool WantsMoreRoomsD(RoomSize roomSize)
            {
                return roomSize switch
                {
                    RoomSize.SMALL => _config.MaxSmallDungeonRooms > _currentSmallRooms,
                    RoomSize.MEDIUM => _config.MaxMediumDungeonRooms > _currentMediumRooms,
                    _ => _config.MaxLargeDungeonRooms > _currentLargeRooms,
                };
            }

            // 记录某种房型已经成功生成了一间。
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

            // 对子代参数做轻微随机扰动。
            public int Mutate(int input)
            {
                int output = input - _config.Mutator + _random.Next(2 * _config.Mutator + 1);
                return output < 0 ? 0 : output;
            }

            // 返回 0~99。
            public int Next100()
            {
                return _random.Next(100);
            }

            // 返回 0~100。
            public int Next101()
            {
                return _random.Next(101);
            }

            // 50% 布尔随机。
            public bool CoinFlip()
            {
                return _random.Next(2) == 0;
            }

            // 读取地图格。
            public DungeonMakerSquareData GetMap(IntCoordinate position)
            {
                return _map[position.X * _config.DimY + position.Y];
            }

            // 读取地图格。
            public DungeonMakerSquareData GetMap(int x, int y)
            {
                return _map[x * _config.DimY + y];
            }

            // 写地图格，并标记本轮 iteration 已有改动。
            public void SetMap(IntCoordinate position, DungeonMakerSquareData value, DungeonMakerTileOrigin origin = DungeonMakerTileOrigin.None)
            {
                int index = position.X * _config.DimY + position.Y;
                _map[index] = value;
                if (origin != DungeonMakerTileOrigin.None)
                    _originMap[index] = origin;
                _changedThisIteration = true;
            }

            // 写地图格，并标记本轮 iteration 已有改动。
            public void SetMap(int x, int y, DungeonMakerSquareData value, DungeonMakerTileOrigin origin = DungeonMakerTileOrigin.None)
            {
                int index = x * _config.DimY + y;
                _map[index] = value;
                if (origin != DungeonMakerTileOrigin.None)
                    _originMap[index] = origin;
                _changedThisIteration = true;
            }

            public void MarkCorridorSkeleton(IntCoordinate position)
            {
                int index = position.X * _config.DimY + position.Y;
                if (_skeletonMap[index] < DungeonMakerSkeletonKind.Corridor)
                    _skeletonMap[index] = DungeonMakerSkeletonKind.Corridor;
            }

            public void MarkAnchorSkeleton(IntCoordinate position)
            {
                _skeletonMap[position.X * _config.DimY + position.Y] = DungeonMakerSkeletonKind.Anchor;
            }

            public int GetMapIndex(IntCoordinate position)
            {
                return position.X * _config.DimY + position.Y;
            }

            public void RegisterRegion(
                DungeonMakerRegionKind kind,
                List<int> tileIndices,
                DungeonMakerRoomSizeClass roomSizeClass = DungeonMakerRoomSizeClass.None,
                DungeonMakerSpecialRoomRole specialRoomRole = DungeonMakerSpecialRoomRole.None,
                int visualStyleId = -1,
                int corridorWidth = 0)
            {
                if (tileIndices == null || tileIndices.Count == 0)
                    return;

                int[] copiedTileIndices = tileIndices.ToArray();
                _regions.Add(new DungeonMakerRegion(
                    _nextRegionId++,
                    kind,
                    copiedTileIndices,
                    roomSizeClass,
                    specialRoomRole,
                    visualStyleId,
                    corridorWidth));
            }

            private int GetInheritedVisualStyle(int parentBuilderId)
            {
                return parentBuilderId >= 0 && _builderVisualStyleIds.TryGetValue(parentBuilderId, out int styleId)
                    ? styleId
                    : _config.RootVisualStyleId;
            }

            private int ResolveChildVisualStyle(int parentBuilderId, int childBuilderId)
            {
                int inheritedStyleId = GetInheritedVisualStyle(parentBuilderId);
                if (parentBuilderId < 0)
                    return inheritedStyleId;

                for (int i = 0; i < _config.VisualStyleRules.Count; i++)
                {
                    DungeonConfig.VisualStyleRule rule = _config.VisualStyleRules[i];
                    if (rule.StyleId != inheritedStyleId || rule.ChildStyleWeights.Count == 0)
                        continue;

                    int totalWeight = 0;
                    for (int weightIndex = 0; weightIndex < rule.ChildStyleWeights.Count; weightIndex++)
                        totalWeight += Math.Max(1, rule.ChildStyleWeights[weightIndex].Weight);

                    int roll = GetVisualStyleRoll(parentBuilderId, childBuilderId, totalWeight);
                    for (int weightIndex = 0; weightIndex < rule.ChildStyleWeights.Count; weightIndex++)
                    {
                        DungeonConfig.VisualStyleWeight weight = rule.ChildStyleWeights[weightIndex];
                        roll -= Math.Max(1, weight.Weight);
                        if (roll < 0)
                            return weight.StyleId;
                    }
                }

                return inheritedStyleId;
            }

            private int GetVisualStyleRoll(int parentBuilderId, int childBuilderId, int totalWeight)
            {
                unchecked
                {
                    uint value = (uint)Seed;
                    value ^= (uint)(parentBuilderId + 1) * 2246822519u;
                    value ^= (uint)(childBuilderId + 1) * 3266489917u;
                    value ^= value >> 16;
                    value *= 2246822519u;
                    value ^= value >> 13;
                    return (int)(value % (uint)Math.Max(1, totalWeight));
                }
            }

            public void CommitSkeletonConnection(IntCoordinate start, IntCoordinate end)
            {
                IntCoordinate current = start;
                MarkCorridorSkeleton(current);

                while (current.X != end.X)
                {
                    current = new IntCoordinate(current.X + Math.Sign(end.X - current.X), current.Y);
                    MarkCorridorSkeleton(current);
                }

                while (current.Y != end.Y)
                {
                    current = new IntCoordinate(current.X, current.Y + Math.Sign(end.Y - current.Y));
                    MarkCorridorSkeleton(current);
                }
            }

            public int GetCurrentSkeletonSegmentId(int builderId)
            {
                return _builderCurrentSkeletonSegmentIds.TryGetValue(builderId, out int segmentId)
                    ? segmentId
                    : -1;
            }

            public int RegisterSkeletonSegment(int builderId, IntCoordinate start, IntCoordinate end, List<int> ownedTileIndices)
            {
                int segmentId = _nextSkeletonSegmentId++;
                int[] copiedOwnedTileIndices = ownedTileIndices != null ? ownedTileIndices.ToArray() : Array.Empty<int>();
                _skeletonSegments.Add(new DungeonMakerSkeletonSegment(
                    segmentId,
                    builderId,
                    new Vector2Int(start.X, start.Y),
                    new Vector2Int(end.X, end.Y),
                    copiedOwnedTileIndices));
                _builderCurrentSkeletonSegmentIds[builderId] = segmentId;
                return segmentId;
            }

            public void RegisterSkeletonLink(int fromSegmentId, int toSegmentId, IntCoordinate from, IntCoordinate to)
            {
                _skeletonLinks.Add(new DungeonMakerSkeletonLink(
                    fromSegmentId,
                    toSegmentId,
                    new Vector2Int(from.X, from.Y),
                    new Vector2Int(to.X, to.Y)));
            }

            public void MarkValuableSkeletonSegment(int segmentId)
            {
                if (segmentId >= 0)
                    _valuableSkeletonSegmentIds.Add(segmentId);
            }

            // 批量写一个矩形区域。
            public void SetRect(int startX, int startY, int endX, int endY, DungeonMakerSquareData value, DungeonMakerTileOrigin origin = DungeonMakerTileOrigin.None)
            {
                if (endX < startX || endY < startY)
                    return;

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                        SetMap(x, y, value, origin);
                }
            }

            // 记录一个已经落进地图的房间。
            public void AddRoom(Room room)
            {
                _rooms.Add(room);
            }
        }

        // 所有施工者的共同基类。
        // Generation/Age/Forward 等概念都在这里统一维护。
        private abstract class Builder
        {
            private readonly bool _hasParentAttachPoint;
            private readonly IntCoordinate _parentAttachPoint;
            private readonly int _parentSkeletonSegmentId;
            private bool _skeletonConnectionCommitted;

            protected Builder(
                DungeonRuntime dungeon,
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                int builderId,
                int parentBuilderId,
                bool hasParentAttachPoint,
                IntCoordinate parentAttachPoint)
            {
                Dungeon = dungeon;
                Location = location;
                Forward = forward;
                Age = age;
                MaxAge = maxAge;
                Generation = generation;
                BuilderId = builderId;
                ParentBuilderId = parentBuilderId;
                _hasParentAttachPoint = hasParentAttachPoint;
                _parentAttachPoint = parentAttachPoint;
                _parentSkeletonSegmentId = parentBuilderId >= 0
                    ? dungeon.GetCurrentSkeletonSegmentId(parentBuilderId)
                    : -1;
            }

            protected DungeonRuntime Dungeon { get; }
            public int BuilderId { get; }
            public int ParentBuilderId { get; }
            public IntCoordinate Location;
            public IntCoordinate Forward;
            public int Age;
            public int MaxAge;
            public int Generation;

            // 返回 true 表示自己继续存活，false 表示本轮后应从 Builder 池移除。
            public abstract bool StepAhead();

            // 根据当前朝向推导出右手方向。
            protected static IntCoordinate GetRight(IntCoordinate heading)
            {
                if (heading.X == 0)
                    return new IntCoordinate(heading.Y, 0);

                return new IntCoordinate(0, -heading.X);
            }

            protected void CommitParentSkeletonConnection(IntCoordinate childEntryPoint)
            {
                if (!_hasParentAttachPoint || _skeletonConnectionCommitted)
                    return;

                Dungeon.CommitSkeletonConnection(_parentAttachPoint, childEntryPoint);
                _skeletonConnectionCommitted = true;
            }

            protected int CurrentSkeletonSegmentId { get; private set; } = -1;
            protected IntCoordinate CurrentSkeletonSegmentEnd { get; private set; }
            protected int ParentSkeletonSegmentId => _parentSkeletonSegmentId;

            protected void RegisterLinearSkeletonSegment(IntCoordinate start, IntCoordinate end, bool anchorSegment, List<int> ownedTileIndices)
            {
                int previousSegmentId = CurrentSkeletonSegmentId;
                int segmentId = Dungeon.RegisterSkeletonSegment(BuilderId, start, end, ownedTileIndices);

                if (previousSegmentId >= 0)
                {
                    Dungeon.RegisterSkeletonLink(previousSegmentId, segmentId, CurrentSkeletonSegmentEnd, start);
                }
                else if (_parentSkeletonSegmentId >= 0 && _hasParentAttachPoint)
                {
                    Dungeon.RegisterSkeletonLink(_parentSkeletonSegmentId, segmentId, _parentAttachPoint, start);
                }

                CurrentSkeletonSegmentId = segmentId;
                CurrentSkeletonSegmentEnd = end;

                IntCoordinate current = start;
                if (anchorSegment)
                    Dungeon.MarkAnchorSkeleton(current);
                else
                    Dungeon.MarkCorridorSkeleton(current);

                while (current.X != end.X)
                {
                    current = new IntCoordinate(current.X + Math.Sign(end.X - current.X), current.Y);
                    if (anchorSegment)
                        Dungeon.MarkAnchorSkeleton(current);
                    else
                        Dungeon.MarkCorridorSkeleton(current);
                }

                while (current.Y != end.Y)
                {
                    current = new IntCoordinate(current.X, current.Y + Math.Sign(end.Y - current.Y));
                    if (anchorSegment)
                        Dungeon.MarkAnchorSkeleton(current);
                    else
                        Dungeon.MarkCorridorSkeleton(current);
                }
            }

            // 向前探测可用空间：
            // - frontFree：正前方还能走多远
            // - leftFree / rightFree：这段前方空间里左右还能扩多少宽度
            protected int FrontFree(IntCoordinate position, IntCoordinate heading, ref int leftFree, ref int rightFree)
            {
                Dungeon.GuardTimeout();
                int frontFree = -1;
                IntCoordinate right = GetRight(heading);
                int checkDist = 0;

                while (frontFree == -1)
                {
                    Dungeon.GuardTimeout();
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
                        Dungeon.GuardTimeout();
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
                        Dungeon.GuardTimeout();
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

        // 隧道工：地图骨架的主力生成器。
        // 它负责挖隧道、决定是否转向/分叉/插前厅，并派生 Roomie 或子 Tunneler。
        private sealed class Tunneler : Builder
        {
            // 期望朝向，用于影响下次转向时更偏哪边。
            private IntCoordinate _intDirection;
            // 单次向前挖多少格。
            private int _stepLength;
            // 隧道半宽；实际宽度通常是 2 * width + 1。
            private int _tunnelWidth;
            // 直行时双生分叉概率。
            private int _straightDoubleSpawnProb;
            // 转向时双生分叉概率。
            private int _turnDoubleSpawnProb;
            // 转向概率。
            private int _changeDirProb;
            // 右侧造房概率。
            private int _makeRoomsRightProb;
            // 左侧造房概率。
            private int _makeRoomsLeftProb;
            // 接入已有区域的偏好。
            private int _joinPreference;

            public Tunneler(
                DungeonRuntime dungeon,
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                int builderId,
                int parentBuilderId,
                bool hasParentAttachPoint,
                IntCoordinate parentAttachPoint,
                IntCoordinate intendedDirection,
                int stepLength,
                int tunnelWidth,
                int straightDoubleSpawnProb,
                int turnDoubleSpawnProb,
                int changeDirProb,
                int makeRoomsRightProb,
                int makeRoomsLeftProb,
                int joinPreference,
                int visualStyleId)
                : base(
                    dungeon,
                    location,
                    forward,
                    age,
                    maxAge,
                    generation,
                    builderId,
                    parentBuilderId,
                    hasParentAttachPoint,
                    parentAttachPoint)
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
                VisualStyleId = visualStyleId;
            }

            private int VisualStyleId { get; }

            // Tunneler 的一次完整动作。
            // 读这段时可以按下面几个阶段理解：
            // 1. 代际/寿命检查
            // 2. 前方空间探测
            // 3. 计算房型和子代延迟
            // 4. 收尾逻辑（接入、补救、终止）
            // 5. 正常挖一段隧道
            // 6. 决定是否转向、变宽、变窄、分叉、插前厅
            // 7. 派生子 Tunneler / Roomie
            public override bool StepAhead()
            {
                Dungeon.GuardTimeout();
                // 只在轮到自己这一代时工作。
                if (Generation != Dungeon.ActiveGeneration)
                    return true;

                // 先增长年龄，再判断是否寿命耗尽。
                Age++;
                if (Age >= MaxAge)
                    return false;
                if (Age < 0)
                    return true;

                // 探测当前朝向下，前/左/右还有多少可用空间。
                int leftFree = _tunnelWidth + 1;
                int rightFree = _tunnelWidth + 1;
                int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                if (frontFree == 0)
                    return false;

                IntCoordinate right = GetRight(Forward);
                IntCoordinate left = -right;
                IntCoordinate test;

                // 根据当前隧道宽度，决定这次侧房/分叉房更倾向生成什么尺寸。
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

                // 计算 Roomie 子代应该延迟到哪一代激活。
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

                // 前方空间不足，或者自己快老死时，进入收尾逻辑。
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

                    // 优先尝试接入已有开放区域，而不是直接终止。
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
                                return false;
                            }
                        }

                        if (roomAhead && _tunnelWidth == 0 && frontFree > 1)
                        {
                            BuildTunnel(frontFree, 0);
                            return false;
                        }

                        // 极细隧道撞到封闭尽头时，允许派一个 last-chance 子代做补救。
                        if (guaranteedClosedAhead && _tunnelWidth == 0)
                        {
                            int jP = Dungeon.Next101() / 10 * 10;
                            if (leftFree >= rightFree)
                            {
                                if (CanSpawnGuaranteedClosedRedirect())
                                    Dungeon.CreateTunneler(Location, -right, 0, MaxAge, Generation + 1, -right, 3, 0, 0, 0, 30, 20, 20, jP, BuilderId, true, Location, DungeonRuntime.TunnelerSpawnKind.Redirect);
                            }
                            else
                            {
                                if (CanSpawnGuaranteedClosedRedirect())
                                    Dungeon.CreateTunneler(Location, right, 0, MaxAge, Generation + 1, right, 3, 0, 0, 0, 30, 20, 20, jP, BuilderId, true, Location, DungeonRuntime.TunnelerSpawnKind.Redirect);
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

                            // 特殊情况：前方与侧边开放区平行接触时，继续向前打穿几格形成更自然的接入。
                            if (specialCase)
                            {
                                BuildTunnel(frontFree, _tunnelWidth);
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
                        Dungeon.CreateRoomie(Location, Forward, 0, 2, Generation, dW, branchingRoomSize, 0, BuilderId, true, Location);
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
                                    SpawnLastChanceTunneler(Location, Forward, Generation + 1, Forward, randomJoinPreference, Location);
                                }
                                else if (freeBackward >= freeForwardRight && freeBackward >= freeForwardLeft)
                                {
                                    SpawnLastChanceTunneler(Location, -Forward, Generation + Dungeon.LastChanceGenDelay, -Forward, randomJoinPreference, Location);
                                }
                                else if (freeForwardRight >= freeForwardLeft || (freeForwardRight == freeForwardLeft && Dungeon.CoinFlip()))
                                {
                                    SpawnLastChanceTunneler(Location, right, Generation + Dungeon.LastChanceGenDelay, right, randomJoinPreference, Location);
                                }
                                else
                                {
                                    SpawnLastChanceTunneler(Location, left, Generation + Dungeon.LastChanceGenDelay, left, randomJoinPreference, Location);
                                }
                            }
                            else
                            {
                                SpawnLastChanceTunneler(Location, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference, Location);
                            }
                        }
                        else if (guaranteedClosedAhead)
                        {
                            SpawnLastChanceTunneler(Location + _tunnelWidth * right, right, Generation + Dungeon.LastChanceGenDelay, right, randomJoinPreference, Location);
                            SpawnLastChanceTunneler(Location - _tunnelWidth * right, left, Generation + Dungeon.LastChanceGenDelay, left, randomJoinPreference, Location);
                        }
                        else if (roomAhead)
                        {
                            if (freeForwardRight >= freeForwardLeft || (freeForwardRight == freeForwardLeft && Dungeon.CoinFlip()))
                            {
                                SpawnLastChanceTunneler(Location + _tunnelWidth * right, right, Generation + Dungeon.LastChanceGenDelay, right, randomJoinPreference, Location);
                                SpawnLastChanceTunneler(Location - _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference, Location);
                            }
                            else
                            {
                                SpawnLastChanceTunneler(Location + _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference, Location);
                                SpawnLastChanceTunneler(Location - _tunnelWidth * right, left, Generation + Dungeon.LastChanceGenDelay, left, randomJoinPreference, Location);
                            }
                        }
                        else
                        {
                            SpawnLastChanceTunneler(Location + _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference, Location);
                            SpawnLastChanceTunneler(Location - _tunnelWidth * right, Forward, Generation + Dungeon.LastChanceGenDelay, Forward, randomJoinPreference, Location);
                        }
                    }

                    return false;
                }

                // 常规情况：向前挖一整段隧道。
                BuildTunnel(_stepLength, _tunnelWidth);

                // 隧道中段两侧可能额外长出侧房。
                if (Dungeon.Next100() < _makeRoomsRightProb)
                {
                    IntCoordinate spawnPoint = Location + (_stepLength / 2 + 1) * Forward + _tunnelWidth * right;
                    IntCoordinate attachPoint = Location + (_stepLength / 2 + 1) * Forward;
                    int defaultWidth = _stepLength / 2 - 1;
                    if (defaultWidth < 1)
                        defaultWidth = 1;
                    Dungeon.CreateRoomie(spawnPoint, right, -1, 2, roomieGeneration, defaultWidth, sideRoomSize, 0, BuilderId, true, attachPoint);
                }

                if (Dungeon.Next100() < _makeRoomsLeftProb)
                {
                    IntCoordinate spawnPoint = Location + (_stepLength / 2 + 1) * Forward + _tunnelWidth * left;
                    IntCoordinate attachPoint = Location + (_stepLength / 2 + 1) * Forward;
                    int defaultWidth = _stepLength / 2 - 1;
                    if (defaultWidth < 1)
                        defaultWidth = 1;
                    Dungeon.CreateRoomie(spawnPoint, left, -1, 2, roomieGeneration, defaultWidth, sideRoomSize, 0, BuilderId, true, attachPoint);
                }

                // 父 Tunneler 自己推进到这段隧道的尽头。
                Location += _stepLength * Forward;

                int nextTunnelWidth = _tunnelWidth;
                diceRoll = Dungeon.Next101();
                int sizeUpProb = Dungeon.GetSizeUpProb(Generation);
                int sizeDownProb = sizeUpProb + Dungeon.GetSizeDownProb(Generation);
                if (diceRoll < sizeUpProb)
                    nextTunnelWidth++;
                else if (diceRoll < sizeDownProb)
                    nextTunnelWidth--;

                nextTunnelWidth = Dungeon.ClampTunnelHalfWidth(nextTunnelWidth);
                bool sizeUpTunnel = nextTunnelWidth > _tunnelWidth;
                bool sizeDownTunnel = nextTunnelWidth < _tunnelWidth;
                int anteRoomSideLength = Dungeon.GetAnteRoomSideLength(nextTunnelWidth);
                int anteRoomHalfWidth = (anteRoomSideLength - 1) / 2;
                bool anteRoomPossible = CanBuildAnteRoom(anteRoomSideLength, anteRoomHalfWidth);

                if (sizeUpTunnel && !anteRoomPossible)
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
                IntCoordinate attachPointForward;
                IntCoordinate attachPointRight;
                IntCoordinate attachPointLeft;

                bool wantsAnteRoom = Dungeon.Next100() < Dungeon.GetAnteRoomProb(_tunnelWidth);
                if (sizeUpTunnel)
                    wantsAnteRoom |= doSpawn;

                if (wantsAnteRoom && anteRoomPossible && BuildAnteRoom(anteRoomSideLength, anteRoomHalfWidth))
                {
                    spawnPointForward = Location + anteRoomSideLength * Forward;
                    spawnPointRight = Location + (anteRoomHalfWidth + 1) * Forward + anteRoomHalfWidth * right;
                    spawnPointLeft = Location + (anteRoomHalfWidth + 1) * Forward + anteRoomHalfWidth * left;
                    attachPointForward = spawnPointForward;
                    attachPointRight = Location + (anteRoomHalfWidth + 1) * Forward;
                    attachPointLeft = Location + (anteRoomHalfWidth + 1) * Forward;
                    builtAnteRoom = true;
                }
                else
                {
                    spawnPointForward = Location;
                    spawnPointRight = Location - _tunnelWidth * Forward + _tunnelWidth * right;
                    spawnPointLeft = Location - _tunnelWidth * Forward + _tunnelWidth * left;
                    attachPointForward = spawnPointForward;
                    attachPointRight = Location - _tunnelWidth * Forward;
                    attachPointLeft = Location - _tunnelWidth * Forward;
                    if (Dungeon.GetMap(spawnPointRight) != DungeonMakerSquareData.IT_OPEN || Dungeon.GetMap(spawnPointLeft) != DungeonMakerSquareData.IT_OPEN)
                        return true;
                }

                // 这里开始决定下一轮/子代的风格：转向、分叉、直行。
                IntCoordinate oldForward = Forward;
                bool goStraight = !changeDirection;
                // 先处理“转向”这一支；否则默认直行。
                if (changeDirection)
                {
                    bool hasTurnConnection = false;
                    IntCoordinate turnAttachPoint = default;
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
                                    hasTurnConnection = true;
                                    turnAttachPoint = attachPointRight;
                                }
                            }
                            else if (freeForwardLeft > 0)
                            {
                                Location = spawnPointLeft;
                                Forward = left;
                                usedLeft = true;
                                hasTurnConnection = true;
                                turnAttachPoint = attachPointLeft;
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
                                    hasTurnConnection = true;
                                    turnAttachPoint = attachPointRight;
                                }
                            }
                            else if (freeForwardLeft > 0)
                            {
                                Location = spawnPointLeft;
                                Forward = left;
                                usedLeft = true;
                                hasTurnConnection = true;
                                turnAttachPoint = attachPointLeft;
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
                                hasTurnConnection = true;
                                turnAttachPoint = attachPointRight;
                            }
                        }
                        else if (freeForwardLeft > 0)
                        {
                            Location = spawnPointLeft;
                            usedLeft = true;
                            hasTurnConnection = true;
                            turnAttachPoint = attachPointLeft;
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
                                hasTurnConnection = true;
                                turnAttachPoint = attachPointRight;
                            }
                        }
                        else if (freeForwardLeft > 0)
                        {
                            Location = spawnPointLeft;
                            usedLeft = true;
                            hasTurnConnection = true;
                            turnAttachPoint = attachPointLeft;
                        }
                    }

                    if (hasTurnConnection)
                        Dungeon.CommitSkeletonConnection(turnAttachPoint, Location + Forward);

                    if (doSpawn)
                    {
                        IntCoordinate spawnPoint = default;
                        IntCoordinate spawnDirection = default;
                        IntCoordinate attachPoint = default;
                        if (usedLeft)
                        {
                            spawnPoint = spawnPointRight;
                            spawnDirection = right;
                            attachPoint = attachPointRight;
                        }
                        else if (usedRight)
                        {
                            spawnPoint = spawnPointLeft;
                            spawnDirection = left;
                            attachPoint = attachPointLeft;
                        }
                        else
                        {
                            goStraight = true;
                        }

                        // 转向成功时，还可能同时派生一个分支子代。
                        if (!goStraight)
                        {
                            diceRoll = Dungeon.Next100();
                            if (doSpawnRoom && diceRoll < 50)
                            {
                                int defaultWidth = Math.Max(1, 2 * _tunnelWidth);
                                int roomGeneration = roomieGeneration;
                                if (builtAnteRoom)
                                    roomGeneration = Generation + (roomieGeneration - Generation) / Dungeon.GenSpeedUpOnAnteRoom;
                                Dungeon.CreateRoomie(spawnPoint, spawnDirection, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0, BuilderId, true, attachPoint);
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
                                    mutatedJoinPreference,
                                    BuilderId,
                                    true,
                                    attachPoint);
                            }

                            if (doSpawnRoom && diceRoll >= 50)
                            {
                                int defaultWidth = Math.Max(1, 2 * _tunnelWidth);
                                int roomGeneration = roomieGeneration;
                                if (builtAnteRoom)
                                    roomGeneration = Generation + (roomieGeneration - Generation) / Dungeon.GenSpeedUpOnAnteRoom;
                                Dungeon.CreateRoomie(spawnPointForward, oldForward, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0, BuilderId, true, attachPointForward);
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
                                    mutatedJoinPreference,
                                    BuilderId,
                                    true,
                                    attachPointForward);
                            }
                        }
                    }
                }

                // 直行分支：自己走主路，左右可能分别派生子代或分叉房。
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
                            Dungeon.CreateRoomie(spawnPointRight, right, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0, BuilderId, true, attachPointRight);
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
                                mutatedJoinPreference,
                                BuilderId,
                                true,
                                attachPointRight);
                        }

                        if (doSpawnRoom && diceRoll >= 50)
                        {
                            int defaultWidth = Math.Max(1, 2 * _tunnelWidth);
                            int roomGeneration = roomieGeneration;
                            if (builtAnteRoom)
                                roomGeneration = Generation + (roomieGeneration - Generation) / Dungeon.GenSpeedUpOnAnteRoom;
                            Dungeon.CreateRoomie(spawnPointLeft, left, 0, 2, roomGeneration, defaultWidth, branchingRoomSize, 0, BuilderId, true, attachPointLeft);
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
                                mutatedJoinPreference,
                                BuilderId,
                                true,
                                attachPointLeft);
                        }
                    }
                }

                return true;
            }

            // 按“变宽 / 变窄”规则修正子代隧道宽度和步长。
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

                tunnelWidth = Dungeon.ClampTunnelHalfWidth(tunnelWidth);
            }

            // 判断当前自身是不是已经处于“last chance”参数模板，避免无限套娃。
            private bool IsAlreadyLastChanceProfile()
            {
                return _makeRoomsLeftProb == Dungeon.LastChanceTunneler.MakeRoomsLeftProb
                    && _makeRoomsRightProb == Dungeon.LastChanceTunneler.MakeRoomsRightProb
                    && _changeDirProb == Dungeon.LastChanceTunneler.ChangeDirectionProb
                    && _straightDoubleSpawnProb == Dungeon.LastChanceTunneler.StraightDoubleSpawnProb
                    && _turnDoubleSpawnProb == Dungeon.LastChanceTunneler.TurnDoubleSpawnProb;
            }

            // guaranteedClosedAhead + tunnelWidth == 0 时的“转向补救 tunneler”防套娃判断。
            // 这里必须对齐原版硬编码参数，避免 redirect baby 继续无穷地产生同类 redirect baby。
            private bool CanSpawnGuaranteedClosedRedirect()
            {
                return _joinPreference != 100
                    || _makeRoomsLeftProb != 20
                    || _makeRoomsRightProb != 20
                    || _changeDirProb != 30
                    || _straightDoubleSpawnProb != 0
                    || _turnDoubleSpawnProb != 0
                    || _tunnelWidth != 0;
            }

            // 判断当前状态是否还有必要派生一个补救用的 last-chance tunneler。
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

            // 用配置里的 last-chance 模板派生一个补救子代。
            private void SpawnLastChanceTunneler(IntCoordinate location, IntCoordinate forward, int generation, IntCoordinate intendedDirection, int joinPreference, IntCoordinate attachPoint)
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
                    joinPreference,
                    BuilderId,
                    true,
                    attachPoint,
                    DungeonRuntime.TunnelerSpawnKind.LastChance);
            }

            // 在当前位置前方 carve 一个前厅。
            private bool BuildAnteRoom(int length, int width)
            {
                if (length < 3 || width < 1)
                    return false;

                int leftFree = width + 1;
                int rightFree = width + 1;
                int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                if (frontFree <= length)
                    return false;

                CommitParentSkeletonConnection(Location + Forward);
                List<int> regionTiles = new();
                IntCoordinate right = GetRight(Forward);
                for (int fwd = 1; fwd <= length; fwd++)
                {
                    for (int side = -width; side <= width; side++)
                    {
                        IntCoordinate cell = Location + fwd * Forward + side * right;
                        Dungeon.SetMap(cell, DungeonMakerSquareData.IA_OPEN, DungeonMakerTileOrigin.AnteRoomCarve);
                        regionTiles.Add(Dungeon.GetMapIndex(cell));
                    }

                    Dungeon.MarkAnchorSkeleton(Location + fwd * Forward);
                }

                RegisterLinearSkeletonSegment(Location, Location + length * Forward, false, regionTiles);

                if (width >= 3 && length >= 7 && Dungeon.ColumnsInTunnels)
                {
                    Dungeon.SetMap(Location + 2 * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);
                    Dungeon.SetMap(Location + 2 * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);
                    Dungeon.SetMap(Location + (length - 1) * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);
                    Dungeon.SetMap(Location + (length - 1) * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);
                }

                Dungeon.RegisterRegion(DungeonMakerRegionKind.AnteRoom, regionTiles, visualStyleId: VisualStyleId);
                Dungeon.RegisterAnteRoomBuilt();
                return true;
            }

            private bool CanBuildAnteRoom(int length, int halfWidth)
            {
                int leftFree = halfWidth + 1;
                int rightFree = halfWidth + 1;
                int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                return frontFree > length;
            }

            // 在当前位置前方 carve 一段隧道。
            private bool BuildTunnel(int length, int width)
            {
                if (length < 1 || width < 0)
                    return false;

                int leftFree = width + 1;
                int rightFree = width + 1;
                int frontFree = FrontFree(Location, Forward, ref leftFree, ref rightFree);
                if (frontFree < length)
                    return false;

                CommitParentSkeletonConnection(Location + Forward);
                List<int> regionTiles = new();
                IntCoordinate right = GetRight(Forward);
                for (int fwd = 1; fwd <= length; fwd++)
                {
                    for (int side = -width; side <= width; side++)
                    {
                        IntCoordinate cell = Location + fwd * Forward + side * right;
                        Dungeon.SetMap(cell, DungeonMakerSquareData.IT_OPEN, DungeonMakerTileOrigin.TunnelCarve);
                        regionTiles.Add(Dungeon.GetMapIndex(cell));
                    }

                    Dungeon.MarkCorridorSkeleton(Location + fwd * Forward);
                }

                RegisterLinearSkeletonSegment(Location, Location + length * Forward, false, regionTiles);

                if (width >= 3 && length >= 7 && Dungeon.ColumnsInTunnels)
                {
                    int numColumns = (length - 1) / 6;
                    for (int i = 0; i < numColumns; i++)
                    {
                        int fwd = 2 + i * 3;
                        Dungeon.SetMap(Location + fwd * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);
                        Dungeon.SetMap(Location + fwd * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);

                        fwd = length - 1 - i * 3;
                        Dungeon.SetMap(Location + fwd * Forward + (-width + 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);
                        Dungeon.SetMap(Location + fwd * Forward + (width - 1) * right, DungeonMakerSquareData.COLUMN, DungeonMakerTileOrigin.ColumnPlacement);
                    }
                }

                Dungeon.RegisterRegion(
                    DungeonMakerRegionKind.Corridor,
                    regionTiles,
                    visualStyleId: VisualStyleId,
                    corridorWidth: 2 * width + 1);
                return true;
            }

            // 用于 join 判定：哪些地块可视为“已经开放的区域”。
            private static bool IsOpenLike(DungeonMakerSquareData tile)
            {
                return tile is DungeonMakerSquareData.OPEN
                    or DungeonMakerSquareData.G_OPEN
                    or DungeonMakerSquareData.IT_OPEN
                    or DungeonMakerSquareData.IA_OPEN;
            }
        }

        // 房间工：只负责尝试一次房间放置，成功后立即退休。
        private sealed class Roomie : Builder
        {
            // 默认从多宽的走廊上长房间。
            private readonly int _defaultWidth;
            // 目标房型大小。
            private readonly RoomSize _size;
            // 预留分类字段，当前 DEMO 里基本未使用。
            private readonly int _category;

            public Roomie(
                DungeonRuntime dungeon,
                IntCoordinate location,
                IntCoordinate forward,
                int age,
                int maxAge,
                int generation,
                int builderId,
                int parentBuilderId,
                bool hasParentAttachPoint,
                IntCoordinate parentAttachPoint,
                int defaultWidth,
                RoomSize size,
                int category,
                int visualStyleId)
                : base(
                    dungeon,
                    location,
                    forward,
                    age,
                    maxAge,
                    generation,
                    builderId,
                    parentBuilderId,
                    hasParentAttachPoint,
                    parentAttachPoint)
            {
                _defaultWidth = defaultWidth;
                _size = size;
                _category = category;
                VisualStyleId = visualStyleId;
            }

            private int VisualStyleId { get; }

            // Roomie 的工作流：
            // 1. 检查该房型是否还需要
            // 2. 逐步探测前方能容纳多大矩形
            // 3. 按面积与长宽比约束修正尺寸
            // 4. carve 房间并补门
            public override bool StepAhead()
            {
                Dungeon.GuardTimeout();
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

                    if (length < Dungeon.MinRoomLength || width < Dungeon.MinRoomWidth)
                        return false;

                    length = Math.Min(length, Dungeon.MaxRoomLength);
                    width = Math.Min(width, Dungeon.MaxRoomWidth);
                    lengthDouble = length;
                    widthDouble = width;

                    if (widthDouble / lengthDouble < roomAspectRatio)
                        return false;

                    while (length * width > maxSize)
                    {
                        Dungeon.GuardTimeout();
                        bool canReduceLength = length > Dungeon.MinRoomLength;
                        bool canReduceWidth = width > Dungeon.MinRoomWidth;
                        if (!canReduceLength && !canReduceWidth)
                            return false;

                        if (length > width && canReduceLength)
                            length--;
                        else if (width > length && canReduceWidth)
                            width--;
                        else if (canReduceLength && canReduceWidth && Dungeon.CoinFlip())
                            length--;
                        else if (canReduceWidth)
                            width--;
                        else
                            length--;
                    }

                    if (length * width >= minSize)
                    {
                        Room room = new();
                        List<int> regionTiles = new();
                        IntCoordinate doorCell = Location + Forward;
                        if (leftFree <= rightFree)
                        {
                            if (2 * leftFree - 1 > width)
                            {
                                for (int fwd = 1; fwd <= length; fwd++)
                                {
                                    for (int side = width / 2; side >= width / 2 - width + 1; side--)
                                    {
                                        IntCoordinate cell = Location + (fwd + 1) * Forward + side * right;
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN, DungeonMakerTileOrigin.RoomCarve);
                                        room.AddSquare(cell);
                                        regionTiles.Add(Dungeon.GetMapIndex(cell));
                                        if (side == 0)
                                            Dungeon.MarkAnchorSkeleton(cell);
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
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN, DungeonMakerTileOrigin.RoomCarve);
                                        room.AddSquare(cell);
                                        regionTiles.Add(Dungeon.GetMapIndex(cell));
                                        if (side == 0)
                                            Dungeon.MarkAnchorSkeleton(cell);
                                    }
                                }
                            }

                            Dungeon.SetMap(doorCell, DungeonMakerSquareData.IR_OPEN, DungeonMakerTileOrigin.RoomCarve);
                            room.AddSquare(doorCell);
                            regionTiles.Add(Dungeon.GetMapIndex(doorCell));
                            Dungeon.MarkAnchorSkeleton(doorCell);
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
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN, DungeonMakerTileOrigin.RoomCarve);
                                        room.AddSquare(cell);
                                        regionTiles.Add(Dungeon.GetMapIndex(cell));
                                        if (side == 0)
                                            Dungeon.MarkAnchorSkeleton(cell);
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
                                        Dungeon.SetMap(cell, DungeonMakerSquareData.IR_OPEN, DungeonMakerTileOrigin.RoomCarve);
                                        room.AddSquare(cell);
                                        regionTiles.Add(Dungeon.GetMapIndex(cell));
                                        if (side == 0)
                                            Dungeon.MarkAnchorSkeleton(cell);
                                    }
                                }
                            }

                            Dungeon.SetMap(doorCell, DungeonMakerSquareData.IR_OPEN, DungeonMakerTileOrigin.RoomCarve);
                            room.AddSquare(doorCell);
                            regionTiles.Add(Dungeon.GetMapIndex(doorCell));
                            Dungeon.MarkAnchorSkeleton(doorCell);
                        }

                        CommitParentSkeletonConnection(Location + Forward);
                        Dungeon.MarkValuableSkeletonSegment(ParentSkeletonSegmentId);
                        Dungeon.RegisterRegion(
                            DungeonMakerRegionKind.Room,
                            regionTiles,
                            ToRoomSizeClass(_size),
                            visualStyleId: VisualStyleId);
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

            // sweepWidth 递增时，辅助重跑一次 FrontFree 探测。
            private int FrontFreeAtCurrentSweep(int sweepWidth)
            {
                int leftFree = sweepWidth + 1;
                int rightFree = sweepWidth + 1;
                return FrontFree(Location, Forward, ref leftFree, ref rightFree);
            }
        }

        // 简单房间记录，只保存内部格子和是否已正式落进地图。
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

        // 三元权重，小/中/大三种结果共用的配置结构。
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

        // 一份完整的 Tunneler 出生模板。
        // 既可以作为初始施工队配置，也可以作为 last-chance 模板。
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
            // 地图宽度（源坐标 X）。
            public int DimX;
            // 地图高度（源坐标 Y）。
            public int DimY;
            // 地图默认背景格类型，当前通常是 CLOSED。
            public DungeonMakerSquareData Background;

            // Tunneler 子代的代际延迟权重表。
            public List<int> BabyDelayProbsTunneler;
            // Roomie 子代的代际延迟权重表。
            public List<int> BabyDelayProbsRoomie;
            // 不同代 Tunneler 的最大寿命表。
            public List<int> MaxAgesT;

            // 侧向房间的大中小概率表，索引通常对应 tunnel width。
            public List<TripleInt> RoomSizeProbS;
            // 分叉房间的大中小概率表，索引通常对应 tunnel width。
            public List<TripleInt> RoomSizeProbB;

            // 接入已有开放区域的偏好权重表。
            public List<int> JoinPref;
            // 不同代数时隧道变宽的概率表。
            public List<int> SizeUpProb;
            // 不同代数时隧道变窄的概率表。
            public List<int> SizeDownProb;
            // 不同 tunnel width 下插入前厅的概率表。
            public List<int> AnteRoomProb;

            // 子代参数扰动幅度。
            public int Mutator;
            // 尝试接入已有通道时的探测距离。
            public int TunnelJoinDist;
            // 受阻时继续尝试的耐心值。
            public int Patience;
            // 变宽后子代默认额外延迟的代数。
            public int SizeUpGenDelay;
            // 宽隧道里是否允许摆柱子。
            public bool ColumnsInTunnels;
            // 房间允许的最小长宽比。
            public double RoomAspectRatio;
            // 生成前厅后对子代代数做的加速修正。
            public int GenSpeedUpOnAnteRoom;
            public int MinCorridorWidth;
            public int MaxCorridorWidth;
            public int MinAnteRoomSide;
            public int MaxAnteRoomSide;
            public int MinRoomLength;
            public int MaxRoomLength;
            public int MinRoomWidth;
            public int MaxRoomWidth;
            public int RootVisualStyleId;
            public List<VisualStyleRule> VisualStyleRules;

            // 小房的最小面积。
            public int MinSmallRoomSize;
            // 中房的最小面积。
            public int MinMediumRoomSize;
            // 大房的最小面积。
            public int MinLargeRoomSize;
            // 任意房间允许的最大面积上限。
            public int MaxRoomSize;

            // 地图中小房数量上限。
            public int MaxSmallDungeonRooms;
            // 地图中中房数量上限。
            public int MaxMediumDungeonRooms;
            // 地图中大房数量上限。
            public int MaxLargeDungeonRooms;

            // 额外 TunnelCrawler 后处理从哪一代开始；-1 表示关闭。
            public int TunnelCrawlerGeneration;
            // 末路补救时生成的特殊 Tunneler 配置。
            public TunnelerSeed LastChanceTunneler;
            // 末路补救子代默认延迟到第几代再激活。
            public int LastChanceGenerationalDelay;
            // 初始投放到地图中的 Tunneler 种子列表。
            public TunnelerSeed[] Tunnelers;

            public static DungeonConfig CreateOriginal()
            {
                return From(DungeonMakerTunnelingConfig.CreateDefault());
            }

            // 把 Inspector 可编辑配置转换成运行时使用的紧凑配置。
            public static DungeonConfig From(DungeonMakerTunnelingConfig source)
            {
                return new DungeonConfig
                {
                    DimX = source.DimX,
                    DimY = source.DimY,
                    Background = source.Background,
                    BabyDelayProbsTunneler = new List<int>(source.BabyDelayProbsTunneler),
                    BabyDelayProbsRoomie = new List<int>(source.BabyDelayProbsRoomie),
                    MaxAgesT = new List<int>(source.MaxAgesT),
                    RoomSizeProbS = ConvertTriples(source.RoomSizeProbS),
                    RoomSizeProbB = ConvertTriples(source.RoomSizeProbB),
                    JoinPref = new List<int>(source.JoinPref),
                    SizeUpProb = new List<int>(source.SizeUpProb),
                    SizeDownProb = new List<int>(source.SizeDownProb),
                    AnteRoomProb = new List<int>(source.AnteRoomProb),
                    Mutator = source.Mutator,
                    TunnelJoinDist = source.TunnelJoinDist,
                    Patience = source.Patience,
                    SizeUpGenDelay = source.SizeUpGenDelay,
                    ColumnsInTunnels = source.ColumnsInTunnels,
                    RoomAspectRatio = source.RoomAspectRatio,
                    GenSpeedUpOnAnteRoom = source.GenSpeedUpOnAnteRoom,
                    MinCorridorWidth = source.MinCorridorWidth,
                    MaxCorridorWidth = source.MaxCorridorWidth,
                    MinAnteRoomSide = source.MinAnteRoomSide,
                    MaxAnteRoomSide = source.MaxAnteRoomSide,
                    MinRoomLength = source.MinRoomLength,
                    MaxRoomLength = source.MaxRoomLength,
                    MinRoomWidth = source.MinRoomWidth,
                    MaxRoomWidth = source.MaxRoomWidth,
                    RootVisualStyleId = source.RootVisualStyleId,
                    VisualStyleRules = ConvertVisualStyleRules(source.VisualStyleRules),
                    MinSmallRoomSize = source.MinSmallRoomSize,
                    MinMediumRoomSize = source.MinMediumRoomSize,
                    MinLargeRoomSize = source.MinLargeRoomSize,
                    MaxRoomSize = source.MaxRoomSize,
                    MaxSmallDungeonRooms = source.MaxSmallDungeonRooms,
                    MaxMediumDungeonRooms = source.MaxMediumDungeonRooms,
                    MaxLargeDungeonRooms = source.MaxLargeDungeonRooms,
                    TunnelCrawlerGeneration = source.TunnelCrawlerGeneration,
                    LastChanceTunneler = ConvertSeed(source.LastChanceTunneler),
                    LastChanceGenerationalDelay = source.LastChanceGenerationalDelay,
                    Tunnelers = ConvertSeeds(source.Tunnelers),
                };
            }

            private static List<TripleInt> ConvertTriples(List<DungeonMakerTripleInt> source)
            {
                List<TripleInt> result = new(source.Count);
                foreach (DungeonMakerTripleInt triple in source)
                    result.Add(new TripleInt(triple.Small, triple.Medium, triple.Large));
                return result;
            }

            private static TunnelerSeed[] ConvertSeeds(DungeonMakerTunnelerSeedData[] source)
            {
                TunnelerSeed[] result = new TunnelerSeed[source.Length];
                for (int i = 0; i < source.Length; i++)
                    result[i] = ConvertSeed(source[i]);
                return result;
            }

            private static TunnelerSeed ConvertSeed(DungeonMakerTunnelerSeedData source)
            {
                return new TunnelerSeed(
                    new IntCoordinate(source.Location.x, source.Location.y),
                    TransformDirection(ConvertDirection(source.Direction)),
                    TransformDirection(ConvertDirection(source.IntendedDirection)),
                    source.Age,
                    source.MaxAge,
                    source.Generation,
                    source.StepLength,
                    source.TunnelWidth,
                    source.StraightDoubleSpawnProb,
                    source.TurnDoubleSpawnProb,
                    source.ChangeDirectionProb,
                    source.MakeRoomsRightProb,
                    source.MakeRoomsLeftProb,
                    source.JoinPreference);
            }

            private static List<VisualStyleRule> ConvertVisualStyleRules(List<DungeonMakerVisualStyleRuleData> source)
            {
                List<VisualStyleRule> result = new();
                if (source == null)
                    return result;

                for (int i = 0; i < source.Count; i++)
                {
                    DungeonMakerVisualStyleRuleData rule = source[i];
                    if (rule == null)
                        continue;

                    VisualStyleRule copiedRule = new() { StyleId = rule.StyleId };
                    if (rule.ChildStyleWeights != null)
                    {
                        for (int weightIndex = 0; weightIndex < rule.ChildStyleWeights.Count; weightIndex++)
                        {
                            DungeonMakerVisualStyleWeightData weight = rule.ChildStyleWeights[weightIndex];
                            if (weight == null)
                                continue;

                            copiedRule.ChildStyleWeights.Add(new VisualStyleWeight
                            {
                                StyleId = weight.StyleId,
                                Weight = Math.Max(1, weight.Weight),
                            });
                        }
                    }

                    result.Add(copiedRule);
                }

                return result;
            }

            public sealed class VisualStyleRule
            {
                public int StyleId;
                public List<VisualStyleWeight> ChildStyleWeights = new();
            }

            public sealed class VisualStyleWeight
            {
                public int StyleId;
                public int Weight;
            }

            private static int NormalizeOddSize(int value, int minimum)
            {
                int normalized = Math.Max(minimum, value);
                return normalized % 2 == 0 ? normalized + 1 : normalized;
            }

            private static Direction ConvertDirection(DungeonMakerDirection source)
            {
                return (Direction)(int)source;
            }
        }

        // 复刻原版 DungeonMaker 使用的 MSVC 风格随机数。
        private sealed class MsRand
        {
            private uint _state;

            public MsRand(int seed)
            {
                _state = unchecked((uint)seed);
            }

            // 返回 [0, exclusiveMax)。
            public int Next(int exclusiveMax)
            {
                if (exclusiveMax <= 1)
                    return 0;

                _state = unchecked(_state * 214013u + 2531011u);
                return (int)((_state >> 16) & 0x7fff) % exclusiveMax;
            }
        }

        // 整个生成器都在用的整数坐标结构。
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

        public enum Direction
        {
            // 向上。
            NO = 0,
            // 向右。
            EA = 1,
            // 向下。
            SO = 2,
            // 向左。
            WE = 3,
            // 右上对角。
            NE = 4,
            // 右下对角。
            SE = 5,
            // 左下对角。
            SW = 6,
            // 左上对角。
            NW = 7,
            // 无方向 / 空方向。
            XX = 8,
        }

        private enum RoomSize
        {
            // 小房间。
            SMALL,
            // 中房间。
            MEDIUM,
            // 大房间。
            LARGE,
        }

        private static DungeonMakerRoomSizeClass ToRoomSizeClass(RoomSize size)
        {
            return size switch
            {
                RoomSize.SMALL => DungeonMakerRoomSizeClass.Small,
                RoomSize.MEDIUM => DungeonMakerRoomSizeClass.Medium,
                _ => DungeonMakerRoomSizeClass.Large,
            };
        }

        // 把设计文件风格的方向枚举翻译成整数向量。
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

