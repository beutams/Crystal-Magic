using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo
{
    [System.Serializable]
    public sealed class TunnelingMapDemoGenerationSnapshot
    {
        public int MasterSeed;
        public int Seed;
        public float CellSize;
        public int SourceWidth;
        public int SourceHeight;
        public int DisplayWidth;
        public int DisplayHeight;
        public int AcceptedAttemptIndex;
        public int AttemptCount;
        public bool Qualified;
        public int QualityScore;
        public int PrunedDeadEndTiles;
        public DungeonMakerTunnelingStats Stats;
    }

    [DisallowMultipleComponent]
    public sealed class TunnelingMapDemo : MonoBehaviour
    {
        private const string GeneratedRootName = "__GeneratedTunnelingDemo";
        private const string PrunedOverlayName = "PrunedOverlay";
        private const string GenerationReportDirectoryName = "Logs";
        private const string GenerationReportFileName = "TunnelingMapDemoGenerationStats.txt";

        [Header("Generation")]
        [SerializeField] private int _seed = DungeonMakerTunnelingGenerator.DefaultSeed;
        [SerializeField] private bool _randomizeSeedOnGenerate = true;
        [SerializeField, Min(0.25f)] private float _cellSize = 1f;
        [SerializeField] private bool _autoGenerateOnPlay;
        [SerializeField] private DungeonMakerTunnelingConfig _config = DungeonMakerTunnelingConfig.CreateDefault();

        [Header("Preview")]
        [SerializeField] private Color _corridorColor = new(0.18f, 0.20f, 0.24f);
        [SerializeField] private Color _roomColor = new(0.28f, 0.24f, 0.16f);
        [SerializeField] private Color _anteRoomColor = new(0.17f, 0.26f, 0.30f);
        [SerializeField] private Color _wallColor = new(0.08f, 0.09f, 0.11f);
        [SerializeField] private Color _prunedDeadEndColor = new(1f, 0.22f, 0.22f, 0.72f);
        [SerializeField] private bool _generateWallCubes = true;

        [Header("Quality Filter")]
        [SerializeField] private bool _requireQualifiedMap = true;
        [SerializeField, Min(1)] private int _maxQualificationAttempts = 64;
        [SerializeField] private Vector2Int _largeRoomRange = new(5, 7);
        [SerializeField] private Vector2Int _mediumRoomRange = new(10, 15);
        [SerializeField] private Vector2Int _smallRoomRange = new(20, 25);
        [SerializeField] private Vector2Int _walkableTileRange = new(4000, 6000);

        [Header("Post Process")]
        [SerializeField] private bool _pruneDeadEnds;

        [Header("Last Result")]
        [SerializeField] private int _lastGeneratedSeed;
        [SerializeField] private TunnelingMapDemoGenerationSnapshot _lastGeneration;

        private DungeonMakerTunnelingResult _lastResult;
        private bool[] _lastPrunedDeadEndMask;
        private Transform _generatedRoot;
        private Texture2D _previewTexture;
        private Sprite _previewSprite;
        private Texture2D _prunedOverlayTexture;
        private Sprite _prunedOverlaySprite;

        public int Seed => _seed;
        public int LastGeneratedSeed => _lastGeneratedSeed;
        public DungeonMakerTunnelingConfig Config => _config;
        public static string GenerationReportFilePath => GetGenerationReportFilePath();

        [ContextMenu("Generate DEMO Map")]
        public void GenerateDemoMap()
        {
            if (_randomizeSeedOnGenerate)
                _seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            ValidateParameters();
            int masterSeed = _seed;
            _lastResult = GenerateQualifiedResult(masterSeed, out int acceptedAttemptIndex, out int attemptCount, out bool qualified, out int qualityScore, out int prunedDeadEndTiles, out bool[] prunedDeadEndMask);
            _lastPrunedDeadEndMask = prunedDeadEndMask;
            _lastGeneratedSeed = _lastResult.Seed;
            _lastGeneration = BuildGenerationSnapshot(masterSeed, _lastResult, acceptedAttemptIndex, attemptCount, qualified, qualityScore, prunedDeadEndTiles);
            RebuildVisuals();
            LogGenerationSummary();
            AppendGenerationReport();
        }

        [ContextMenu("Generate DEMO Map With New Seed")]
        public void GenerateDemoMapWithNewSeed()
        {
            bool originalRandomize = _randomizeSeedOnGenerate;

            _randomizeSeedOnGenerate = true;
            GenerateDemoMap();
            _randomizeSeedOnGenerate = originalRandomize;
        }

        [ContextMenu("Clear DEMO Map")]
        public void ClearDemoMap()
        {
            _lastResult = null;
            _lastPrunedDeadEndMask = null;
            _lastGeneratedSeed = 0;
            _lastGeneration = null;
            DestroyGeneratedRoot();
            DestroyPreviewAssets();
        }

        [ContextMenu("Clear Generation Report File")]
        public void ClearGenerationReportFile()
        {
            string filePath = GetGenerationReportFilePath();
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);

            File.WriteAllText(filePath, string.Empty, Encoding.UTF8);
            Debug.Log($"[TunnelingMapDemo] Cleared generation report file: {filePath}");
        }

        private void Start()
        {
            if (Application.isPlaying && _autoGenerateOnPlay)
                GenerateDemoMap();
        }

        private void OnDestroy()
        {
            DestroyPreviewAssets();
        }

        private void OnValidate()
        {
            ValidateParameters();
        }

        private void ValidateParameters()
        {
            _cellSize = Mathf.Max(0.25f, _cellSize);
            _maxQualificationAttempts = Mathf.Max(1, _maxQualificationAttempts);
            _largeRoomRange = NormalizeRange(_largeRoomRange);
            _mediumRoomRange = NormalizeRange(_mediumRoomRange);
            _smallRoomRange = NormalizeRange(_smallRoomRange);
            _walkableTileRange = NormalizeRange(_walkableTileRange);
            _config ??= DungeonMakerTunnelingConfig.CreateDefault();
            _config.DimX = Mathf.Max(3, _config.DimX);
            _config.DimY = Mathf.Max(3, _config.DimY);
            _config.MaxRoomSize = Mathf.Max(1, _config.MaxRoomSize);
            _config.MinSmallRoomSize = Mathf.Clamp(_config.MinSmallRoomSize, 1, _config.MaxRoomSize);
            _config.MinMediumRoomSize = Mathf.Clamp(_config.MinMediumRoomSize, _config.MinSmallRoomSize, _config.MaxRoomSize);
            _config.MinLargeRoomSize = Mathf.Clamp(_config.MinLargeRoomSize, _config.MinMediumRoomSize, _config.MaxRoomSize);
            _config.RoomAspectRatio = Mathf.Max(0.01f, (float)_config.RoomAspectRatio);
            EnsureCollections();
        }

        private void EnsureCollections()
        {
            _config.BabyDelayProbsTunneler ??= new();
            _config.BabyDelayProbsRoomie ??= new();
            _config.MaxAgesT ??= new();
            _config.RoomSizeProbS ??= new();
            _config.RoomSizeProbB ??= new();
            _config.JoinPref ??= new();
            _config.SizeUpProb ??= new();
            _config.SizeDownProb ??= new();
            _config.AnteRoomProb ??= new();
            _config.Tunnelers ??= System.Array.Empty<DungeonMakerTunnelerSeedData>();
        }

        private void RebuildVisuals()
        {
            DestroyGeneratedRoot();
            DestroyPreviewAssets();

            if (_lastResult == null)
                return;

            _generatedRoot = new GameObject(GeneratedRootName).transform;
            _generatedRoot.SetParent(transform, false);

            CreateSpritePreview();
            CreatePrunedOverlay();
            if (_generateWallCubes)
                CreateWallCubes();
        }

        private DungeonMakerTunnelingResult GenerateQualifiedResult(
            int masterSeed,
            out int acceptedAttemptIndex,
            out int attemptCount,
            out bool qualified,
            out int qualityScore,
            out int prunedDeadEndTiles,
            out bool[] prunedDeadEndMask)
        {
            int maxAttempts = _requireQualifiedMap ? _maxQualificationAttempts : 1;
            DungeonMakerTunnelingResult bestResult = null;
            int bestScore = int.MinValue;
            int bestAttemptIndex = 0;
            int bestPrunedDeadEnds = 0;
            bool[] bestPrunedDeadEndMask = null;

            for (int attemptIndex = 0; attemptIndex < maxAttempts; attemptIndex++)
            {
                int candidateSeed = DeriveCandidateSeed(masterSeed, attemptIndex);
                DungeonMakerTunnelingResult candidateResult = DungeonMakerTunnelingGenerator.Generate(candidateSeed, _config);
                candidateResult = PostProcessCandidateResult(candidateResult, out int candidatePrunedDeadEnds, out bool[] candidatePrunedDeadEndMask);
                DungeonMakerTunnelingStats stats = candidateResult.Stats;
                int candidateScore = ScoreMapQuality(stats);

                if (bestResult == null || candidateScore > bestScore)
                {
                    bestResult = candidateResult;
                    bestScore = candidateScore;
                    bestAttemptIndex = attemptIndex;
                    bestPrunedDeadEnds = candidatePrunedDeadEnds;
                    bestPrunedDeadEndMask = candidatePrunedDeadEndMask;
                }

                if (!_requireQualifiedMap || IsMapQualified(stats))
                {
                    acceptedAttemptIndex = attemptIndex;
                    attemptCount = attemptIndex + 1;
                    qualified = true;
                    qualityScore = candidateScore;
                    prunedDeadEndTiles = candidatePrunedDeadEnds;
                    prunedDeadEndMask = candidatePrunedDeadEndMask;
                    return candidateResult;
                }
            }

            acceptedAttemptIndex = bestAttemptIndex;
            attemptCount = maxAttempts;
            qualified = false;
            qualityScore = bestScore;
            prunedDeadEndTiles = bestPrunedDeadEnds;
            prunedDeadEndMask = bestPrunedDeadEndMask;
            return bestResult;
        }

        private TunnelingMapDemoGenerationSnapshot BuildGenerationSnapshot(
            int masterSeed,
            DungeonMakerTunnelingResult result,
            int acceptedAttemptIndex,
            int attemptCount,
            bool qualified,
            int qualityScore,
            int prunedDeadEndTiles)
        {
            return new TunnelingMapDemoGenerationSnapshot
            {
                MasterSeed = masterSeed,
                Seed = result.Seed,
                CellSize = _cellSize,
                SourceWidth = result.SourceWidth,
                SourceHeight = result.SourceHeight,
                DisplayWidth = result.DisplayWidth,
                DisplayHeight = result.DisplayHeight,
                AcceptedAttemptIndex = acceptedAttemptIndex,
                AttemptCount = attemptCount,
                Qualified = qualified,
                QualityScore = qualityScore,
                PrunedDeadEndTiles = prunedDeadEndTiles,
                Stats = result.Stats,
            };
        }

        private void LogGenerationSummary()
        {
            if (_lastGeneration?.Stats == null)
                return;

            DungeonMakerTunnelingStats stats = _lastGeneration.Stats;
            Debug.Log(
                $"[TunnelingMapDemo] MasterSeed={_lastGeneration.MasterSeed} SelectedSeed={_lastGeneration.Seed} " +
                $"Attempt={_lastGeneration.AcceptedAttemptIndex + 1}/{_lastGeneration.AttemptCount} Qualified={_lastGeneration.Qualified} Score={_lastGeneration.QualityScore} " +
                $"PrunedDeadEnds={_lastGeneration.PrunedDeadEndTiles} " +
                $"CellSize={_lastGeneration.CellSize:F2} " +
                $"Source={_lastGeneration.SourceWidth}x{_lastGeneration.SourceHeight} " +
                $"Display={_lastGeneration.DisplayWidth}x{_lastGeneration.DisplayHeight} " +
                $"TotalCells={stats.TotalCells} Walkable={stats.WalkableTiles} Blocked={stats.BlockedTiles} " +
                $"Rooms={stats.TotalRooms} (S={stats.SmallRooms}, M={stats.MediumRooms}, L={stats.LargeRooms}) " +
                $"AnteRooms={stats.AnteRooms} RoomTiles={stats.RoomTiles} TunnelTiles={stats.TunnelTiles} AnteRoomTiles={stats.AnteRoomTiles} " +
                $"Doors(H={stats.HorizontalDoorTiles}, V={stats.VerticalDoorTiles}) Columns={stats.ColumnTiles} " +
                $"Mobs={stats.MobTiles} Treasures={stats.TreasureTiles}");
        }

        private void AppendGenerationReport()
        {
            if (_lastGeneration?.Stats == null)
                return;

            string filePath = GetGenerationReportFilePath();
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);

            File.AppendAllText(filePath, BuildGenerationReportText(), Encoding.UTF8);
            Debug.Log($"[TunnelingMapDemo] Appended generation report: {filePath}");
        }

        private string BuildGenerationReportText()
        {
            DungeonMakerTunnelingStats stats = _lastGeneration.Stats;
            StringBuilder builder = new();
            builder.AppendLine("============================================================");
            builder.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"MasterSeed: {_lastGeneration.MasterSeed}");
            builder.AppendLine($"SelectedSeed: {_lastGeneration.Seed}");
            builder.AppendLine($"AcceptedAttemptIndex: {_lastGeneration.AcceptedAttemptIndex}");
            builder.AppendLine($"AttemptCount: {_lastGeneration.AttemptCount}");
            builder.AppendLine($"Qualified: {_lastGeneration.Qualified}");
            builder.AppendLine($"QualityScore: {_lastGeneration.QualityScore}");
            builder.AppendLine($"PrunedDeadEndTiles: {_lastGeneration.PrunedDeadEndTiles}");
            builder.AppendLine($"CellSize: {_lastGeneration.CellSize:F2}");
            builder.AppendLine($"SourceSize: {_lastGeneration.SourceWidth} x {_lastGeneration.SourceHeight}");
            builder.AppendLine($"DisplaySize: {_lastGeneration.DisplayWidth} x {_lastGeneration.DisplayHeight}");
            builder.AppendLine();
            builder.AppendLine("[Targets]");
            builder.AppendLine($"LargeRooms: {_largeRoomRange.x} - {_largeRoomRange.y}");
            builder.AppendLine($"MediumRooms: {_mediumRoomRange.x} - {_mediumRoomRange.y}");
            builder.AppendLine($"SmallRooms: {_smallRoomRange.x} - {_smallRoomRange.y}");
            builder.AppendLine($"WalkableTiles: {_walkableTileRange.x} - {_walkableTileRange.y}");
            builder.AppendLine();
            builder.AppendLine("[Counts]");
            builder.AppendLine($"TotalCells: {stats.TotalCells}");
            builder.AppendLine($"WalkableTiles: {stats.WalkableTiles}");
            builder.AppendLine($"BlockedTiles: {stats.BlockedTiles}");
            builder.AppendLine();
            builder.AppendLine("[Rooms]");
            builder.AppendLine($"TotalRooms: {stats.TotalRooms}");
            builder.AppendLine($"SmallRooms: {stats.SmallRooms}");
            builder.AppendLine($"MediumRooms: {stats.MediumRooms}");
            builder.AppendLine($"LargeRooms: {stats.LargeRooms}");
            builder.AppendLine($"AnteRooms: {stats.AnteRooms}");
            builder.AppendLine();
            builder.AppendLine("[Tile Types]");
            builder.AppendLine($"OpenTiles: {stats.OpenTiles}");
            builder.AppendLine($"ClosedTiles: {stats.ClosedTiles}");
            builder.AppendLine($"BoundaryOpenTiles: {stats.BoundaryOpenTiles}");
            builder.AppendLine($"BoundaryClosedTiles: {stats.BoundaryClosedTiles}");
            builder.AppendLine($"NonJoinOpenTiles: {stats.NonJoinOpenTiles}");
            builder.AppendLine($"NonJoinClosedTiles: {stats.NonJoinClosedTiles}");
            builder.AppendLine($"NonJoinBoundaryOpenTiles: {stats.NonJoinBoundaryOpenTiles}");
            builder.AppendLine($"NonJoinBoundaryClosedTiles: {stats.NonJoinBoundaryClosedTiles}");
            builder.AppendLine($"RoomTiles: {stats.RoomTiles}");
            builder.AppendLine($"TunnelTiles: {stats.TunnelTiles}");
            builder.AppendLine($"AnteRoomTiles: {stats.AnteRoomTiles}");
            builder.AppendLine($"HorizontalDoorTiles: {stats.HorizontalDoorTiles}");
            builder.AppendLine($"VerticalDoorTiles: {stats.VerticalDoorTiles}");
            builder.AppendLine($"ColumnTiles: {stats.ColumnTiles}");
            builder.AppendLine($"MobTiles: {stats.MobTiles}");
            builder.AppendLine($"TreasureTiles: {stats.TreasureTiles}");
            builder.AppendLine();
            return builder.ToString();
        }

        private DungeonMakerTunnelingResult PostProcessCandidateResult(DungeonMakerTunnelingResult result, out int prunedDeadEndTiles, out bool[] prunedDeadEndMask)
        {
            prunedDeadEndTiles = 0;
            prunedDeadEndMask = null;
            if (!_pruneDeadEnds || result.Regions.Count == 0)
                return result;

            DungeonMakerSquareData[] map = CopySourceMap(result);
            DungeonMakerSkeletonKind[] skeletonKinds = CopySourceSkeletonKinds(result);
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            int[] tileOwners = BuildRegionOwnershipMap(map.Length, regions);
            List<HashSet<int>> adjacency = BuildRegionAdjacency(regions, tileOwners, map, result.SourceWidth, result.SourceHeight);
            bool[] activeRegions = new bool[regions.Count];
            for (int i = 0; i < activeRegions.Length; i++)
                activeRegions[i] = true;
            bool[] removedMask = new bool[map.Length];

            Queue<int> queue = new();
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Kind == DungeonMakerRegionKind.Corridor && CountActiveRegionNeighbors(i, adjacency, activeRegions) <= 1)
                    queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int regionIndex = queue.Dequeue();
                if (!activeRegions[regionIndex] || regions[regionIndex].Kind != DungeonMakerRegionKind.Corridor)
                    continue;

                if (CountActiveRegionNeighbors(regionIndex, adjacency, activeRegions) > 1)
                    continue;

                activeRegions[regionIndex] = false;
                int[] tiles = regions[regionIndex].TileIndices;
                for (int i = 0; i < tiles.Length; i++)
                {
                    int tileIndex = tiles[i];
                    if (!IsPrunableDeadEndTile(map[tileIndex]))
                        continue;

                    removedMask[tileIndex] = true;
                    map[tileIndex] = GetDeadEndFilledTile(map[tileIndex]);
                    prunedDeadEndTiles++;
                }

                foreach (int neighbor in adjacency[regionIndex])
                {
                    if (activeRegions[neighbor] && regions[neighbor].Kind == DungeonMakerRegionKind.Corridor)
                        queue.Enqueue(neighbor);
                }
            }

            if (prunedDeadEndTiles == 0)
                return result;

            prunedDeadEndMask = removedMask;
            DungeonMakerTunnelingStats stats = BuildStatsFromMap(result, map);
            DungeonMakerRegion[] copiedRegions = new DungeonMakerRegion[regions.Count];
            for (int i = 0; i < regions.Count; i++)
                copiedRegions[i] = regions[i];
            return new DungeonMakerTunnelingResult(result.SourceWidth, result.SourceHeight, result.Seed, map, skeletonKinds, copiedRegions, stats);
        }

        private static int[] BuildRegionOwnershipMap(int cellCount, IReadOnlyList<DungeonMakerRegion> regions)
        {
            int[] tileOwners = new int[cellCount];
            Array.Fill(tileOwners, -1);

            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                int[] tiles = regions[regionIndex].TileIndices;
                for (int i = 0; i < tiles.Length; i++)
                {
                    int tileIndex = tiles[i];
                    if (tileIndex >= 0 && tileIndex < cellCount && tileOwners[tileIndex] < 0)
                        tileOwners[tileIndex] = regionIndex;
                }
            }

            return tileOwners;
        }

        private static List<HashSet<int>> BuildRegionAdjacency(IReadOnlyList<DungeonMakerRegion> regions, int[] tileOwners, DungeonMakerSquareData[] map, int width, int height)
        {
            List<HashSet<int>> adjacency = new(regions.Count);
            for (int i = 0; i < regions.Count; i++)
                adjacency.Add(new HashSet<int>());

            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                int[] tiles = regions[regionIndex].TileIndices;
                for (int i = 0; i < tiles.Length; i++)
                {
                    int tileIndex = tiles[i];
                    int x = tileIndex / height;
                    int y = tileIndex % height;
                    TryLinkRegionNeighbor(adjacency, tileOwners, regionIndex, width, height, x + 1, y);
                    TryLinkRegionNeighbor(adjacency, tileOwners, regionIndex, width, height, x - 1, y);
                    TryLinkRegionNeighbor(adjacency, tileOwners, regionIndex, width, height, x, y + 1);
                    TryLinkRegionNeighbor(adjacency, tileOwners, regionIndex, width, height, x, y - 1);
                }
            }

            bool[] visitedConnectors = new bool[map.Length];
            Queue<int> queue = new();
            for (int index = 0; index < map.Length; index++)
            {
                if (tileOwners[index] >= 0 || visitedConnectors[index] || !IsWalkableTile(map[index]))
                    continue;

                visitedConnectors[index] = true;
                queue.Enqueue(index);
                HashSet<int> touchedRegions = new();

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    int x = current / height;
                    int y = current % height;
                    CollectConnectorNeighbor(touchedRegions, tileOwners, map, visitedConnectors, queue, width, height, x + 1, y);
                    CollectConnectorNeighbor(touchedRegions, tileOwners, map, visitedConnectors, queue, width, height, x - 1, y);
                    CollectConnectorNeighbor(touchedRegions, tileOwners, map, visitedConnectors, queue, width, height, x, y + 1);
                    CollectConnectorNeighbor(touchedRegions, tileOwners, map, visitedConnectors, queue, width, height, x, y - 1);
                }

                if (touchedRegions.Count >= 2)
                {
                    foreach (int regionA in touchedRegions)
                    {
                        foreach (int regionB in touchedRegions)
                        {
                            if (regionA == regionB)
                                continue;

                            adjacency[regionA].Add(regionB);
                        }
                    }
                }
            }

            return adjacency;
        }

        private static void CollectConnectorNeighbor(
            HashSet<int> touchedRegions,
            int[] tileOwners,
            DungeonMakerSquareData[] map,
            bool[] visitedConnectors,
            Queue<int> queue,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = GetIndex(x, y, height);
            int regionIndex = tileOwners[index];
            if (regionIndex >= 0)
            {
                touchedRegions.Add(regionIndex);
                return;
            }

            if (visitedConnectors[index] || !IsWalkableTile(map[index]))
                return;

            visitedConnectors[index] = true;
            queue.Enqueue(index);
        }

        private static void TryLinkRegionNeighbor(List<HashSet<int>> adjacency, int[] tileOwners, int regionIndex, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int neighborRegionIndex = tileOwners[GetIndex(x, y, height)];
            if (neighborRegionIndex < 0 || neighborRegionIndex == regionIndex)
                return;

            adjacency[regionIndex].Add(neighborRegionIndex);
            adjacency[neighborRegionIndex].Add(regionIndex);
        }

        private static int CountActiveRegionNeighbors(int regionIndex, List<HashSet<int>> adjacency, bool[] activeRegions)
        {
            int count = 0;
            foreach (int neighbor in adjacency[regionIndex])
            {
                if (activeRegions[neighbor])
                    count++;
            }

            return count;
        }

        private DungeonMakerTunnelingStats BuildStatsFromMap(DungeonMakerTunnelingResult sourceResult, DungeonMakerSquareData[] map)
        {
            DungeonMakerTunnelingStats sourceStats = sourceResult.Stats;
            DungeonMakerTunnelingStats stats = new()
            {
                SourceWidth = sourceResult.SourceWidth,
                SourceHeight = sourceResult.SourceHeight,
                DisplayWidth = sourceResult.DisplayWidth,
                DisplayHeight = sourceResult.DisplayHeight,
                TotalCells = map.Length,
                SmallRooms = sourceStats.SmallRooms,
                MediumRooms = sourceStats.MediumRooms,
                LargeRooms = sourceStats.LargeRooms,
                TotalRooms = sourceStats.TotalRooms,
                AnteRooms = sourceStats.AnteRooms,
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

        private bool IsMapQualified(DungeonMakerTunnelingStats stats)
        {
            return IsWithinRange(stats.LargeRooms, _largeRoomRange)
                && IsWithinRange(stats.MediumRooms, _mediumRoomRange)
                && IsWithinRange(stats.SmallRooms, _smallRoomRange)
                && IsWithinRange(stats.WalkableTiles, _walkableTileRange);
        }

        private int ScoreMapQuality(DungeonMakerTunnelingStats stats)
        {
            return
                -DistanceToRange(stats.LargeRooms, _largeRoomRange) * 2000
                -DistanceToRange(stats.MediumRooms, _mediumRoomRange) * 1000
                -DistanceToRange(stats.SmallRooms, _smallRoomRange) * 500
                -DistanceToRange(stats.WalkableTiles, _walkableTileRange);
        }

        private static DungeonMakerSquareData[] CopySourceMap(DungeonMakerTunnelingResult result)
        {
            int width = result.SourceWidth;
            int height = result.SourceHeight;
            DungeonMakerSquareData[] map = new DungeonMakerSquareData[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    map[x * height + y] = result.GetSourceTile(x, y);
            }

            return map;
        }

        private static DungeonMakerSkeletonKind[] CopySourceSkeletonKinds(DungeonMakerTunnelingResult result)
        {
            int width = result.SourceWidth;
            int height = result.SourceHeight;
            DungeonMakerSkeletonKind[] skeletonKinds = new DungeonMakerSkeletonKind[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    skeletonKinds[x * height + y] = result.GetSourceSkeletonKind(x, y);
            }

            return skeletonKinds;
        }

        private static bool[] BuildPrunableMask(DungeonMakerSquareData[] map)
        {
            bool[] mask = new bool[map.Length];
            for (int i = 0; i < map.Length; i++)
                mask[i] = IsPrunableDeadEndTile(map[i]);

            return mask;
        }

        private static bool[] BuildOccupiedSkeletonMask(DungeonMakerSkeletonKind[] skeletonKinds)
        {
            bool[] mask = new bool[skeletonKinds.Length];
            for (int i = 0; i < skeletonKinds.Length; i++)
                mask[i] = skeletonKinds[i] != DungeonMakerSkeletonKind.None;

            return mask;
        }

        private static bool[] BuildPrunableSkeletonMask(DungeonMakerSkeletonKind[] skeletonKinds)
        {
            bool[] mask = new bool[skeletonKinds.Length];
            for (int i = 0; i < skeletonKinds.Length; i++)
                mask[i] = skeletonKinds[i] == DungeonMakerSkeletonKind.Corridor;

            return mask;
        }

        private static void PruneDeadEndSkeleton(
            bool[] prunableSkeletonMask,
            bool[] occupiedSkeletonMask,
            int width,
            int height,
            out bool[] keptCorridorSkeletonMask,
            out bool[] removedCorridorSkeletonMask)
        {
            bool[] workingCorridorSkeletonMask = new bool[prunableSkeletonMask.Length];
            Array.Copy(prunableSkeletonMask, workingCorridorSkeletonMask, prunableSkeletonMask.Length);
            bool[] workingOccupiedSkeletonMask = new bool[occupiedSkeletonMask.Length];
            Array.Copy(occupiedSkeletonMask, workingOccupiedSkeletonMask, occupiedSkeletonMask.Length);
            removedCorridorSkeletonMask = new bool[prunableSkeletonMask.Length];
            Queue<int> queue = new();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int index = GetIndex(x, y, height);
                    if (!workingCorridorSkeletonMask[index])
                        continue;

                    if (CountOccupiedSkeletonNeighbors(workingOccupiedSkeletonMask, width, height, x, y) <= 1)
                        queue.Enqueue(index);
                }
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (!workingCorridorSkeletonMask[index])
                    continue;

                int x = index / height;
                int y = index % height;
                if (CountOccupiedSkeletonNeighbors(workingOccupiedSkeletonMask, width, height, x, y) > 1)
                    continue;

                workingCorridorSkeletonMask[index] = false;
                workingOccupiedSkeletonMask[index] = false;
                removedCorridorSkeletonMask[index] = true;
                EnqueueCorridorSkeletonNeighbor(queue, workingCorridorSkeletonMask, width, height, x + 1, y);
                EnqueueCorridorSkeletonNeighbor(queue, workingCorridorSkeletonMask, width, height, x - 1, y);
                EnqueueCorridorSkeletonNeighbor(queue, workingCorridorSkeletonMask, width, height, x, y + 1);
                EnqueueCorridorSkeletonNeighbor(queue, workingCorridorSkeletonMask, width, height, x, y - 1);
            }

            keptCorridorSkeletonMask = workingCorridorSkeletonMask;
        }

        private static bool[] ComputeRemovedCorridorMask(bool[] prunableMask, bool[] keptCorridorSkeletonMask, bool[] removedCorridorSkeletonMask, int width, int height)
        {
            bool hasRemovedSkeleton = false;
            bool hasKeptSkeleton = false;
            for (int i = 0; i < removedCorridorSkeletonMask.Length; i++)
                hasRemovedSkeleton |= removedCorridorSkeletonMask[i];
            for (int i = 0; i < keptCorridorSkeletonMask.Length; i++)
                hasKeptSkeleton |= keptCorridorSkeletonMask[i];

            if (!hasRemovedSkeleton)
                return new bool[prunableMask.Length];

            int[] keptDistance = hasKeptSkeleton ? BuildDistanceField(prunableMask, keptCorridorSkeletonMask, width, height) : null;
            int[] removedDistance = BuildDistanceField(prunableMask, removedCorridorSkeletonMask, width, height);
            bool[] removedMask = new bool[prunableMask.Length];

            for (int i = 0; i < prunableMask.Length; i++)
            {
                if (!prunableMask[i] || removedDistance[i] == int.MaxValue)
                    continue;

                int kept = keptDistance == null ? int.MaxValue : keptDistance[i];
                if (removedDistance[i] < kept)
                    removedMask[i] = true;
            }

            return removedMask;
        }

        private static int[] BuildDistanceField(bool[] traversableMask, bool[] sourceMask, int width, int height)
        {
            int[] distance = new int[traversableMask.Length];
            Array.Fill(distance, int.MaxValue);
            Queue<int> queue = new();

            for (int i = 0; i < sourceMask.Length; i++)
            {
                if (!sourceMask[i])
                    continue;

                distance[i] = 0;
                queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int currentDistance = distance[current];
                int x = current / height;
                int y = current % height;

                TryRelaxDistance(queue, distance, traversableMask, width, height, x + 1, y, currentDistance + 1);
                TryRelaxDistance(queue, distance, traversableMask, width, height, x - 1, y, currentDistance + 1);
                TryRelaxDistance(queue, distance, traversableMask, width, height, x, y + 1, currentDistance + 1);
                TryRelaxDistance(queue, distance, traversableMask, width, height, x, y - 1, currentDistance + 1);
            }

            return distance;
        }

        private static void TryRelaxDistance(Queue<int> queue, int[] distance, bool[] traversableMask, int width, int height, int x, int y, int candidateDistance)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = GetIndex(x, y, height);
            if (!traversableMask[index] || candidateDistance >= distance[index])
                return;

            distance[index] = candidateDistance;
            queue.Enqueue(index);
        }

        private static void EnqueueCorridorSkeletonNeighbor(Queue<int> queue, bool[] corridorSkeletonMask, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = GetIndex(x, y, height);
            if (corridorSkeletonMask[index])
                queue.Enqueue(index);
        }

        private static int CountOccupiedSkeletonNeighbors(bool[] occupiedSkeletonMask, int width, int height, int x, int y)
        {
            int count = 0;
            count += HasSkeletonAt(occupiedSkeletonMask, width, height, x + 1, y) ? 1 : 0;
            count += HasSkeletonAt(occupiedSkeletonMask, width, height, x - 1, y) ? 1 : 0;
            count += HasSkeletonAt(occupiedSkeletonMask, width, height, x, y + 1) ? 1 : 0;
            count += HasSkeletonAt(occupiedSkeletonMask, width, height, x, y - 1) ? 1 : 0;
            return count;
        }

        private static bool HasSkeletonAt(bool[] skeletonMask, int width, int height, int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height && skeletonMask[GetIndex(x, y, height)];
        }

        private static int GetIndex(int x, int y, int height)
        {
            return x * height + y;
        }

        private static bool IsPrunableDeadEndTile(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.OPEN
                or DungeonMakerSquareData.G_OPEN
                or DungeonMakerSquareData.NJ_OPEN
                or DungeonMakerSquareData.NJ_G_OPEN
                or DungeonMakerSquareData.IT_OPEN;
        }

        private static bool IsWalkableTile(DungeonMakerSquareData tile)
        {
            return tile is not DungeonMakerSquareData.CLOSED
                and not DungeonMakerSquareData.G_CLOSED
                and not DungeonMakerSquareData.NJ_CLOSED
                and not DungeonMakerSquareData.NJ_G_CLOSED
                and not DungeonMakerSquareData.COLUMN;
        }

        private static bool HasAnyMarkedTile(bool[] mask)
        {
            if (mask == null)
                return false;

            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i])
                    return true;
            }

            return false;
        }

        private static DungeonMakerSquareData GetDeadEndFilledTile(DungeonMakerSquareData tile)
        {
            return tile switch
            {
                DungeonMakerSquareData.G_OPEN => DungeonMakerSquareData.G_CLOSED,
                DungeonMakerSquareData.NJ_OPEN => DungeonMakerSquareData.NJ_CLOSED,
                DungeonMakerSquareData.NJ_G_OPEN => DungeonMakerSquareData.NJ_G_CLOSED,
                _ => DungeonMakerSquareData.CLOSED,
            };
        }

        private static int DeriveCandidateSeed(int masterSeed, int attemptIndex)
        {
            if (attemptIndex == 0)
                return masterSeed;

            unchecked
            {
                uint x = (uint)masterSeed;
                x ^= 0x9E3779B9u;
                x += (uint)attemptIndex * 0x85EBCA6Bu;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (int)x;
            }
        }

        private static Vector2Int NormalizeRange(Vector2Int range)
        {
            if (range.x <= range.y)
                return range;

            return new Vector2Int(range.y, range.x);
        }

        private static bool IsWithinRange(int value, Vector2Int range)
        {
            return value >= range.x && value <= range.y;
        }

        private static int DistanceToRange(int value, Vector2Int range)
        {
            if (value < range.x)
                return range.x - value;

            if (value > range.y)
                return value - range.y;

            return 0;
        }

        private static string GetGenerationReportFilePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, GenerationReportDirectoryName, GenerationReportFileName);
        }

        private void CreateSpritePreview()
        {
            GameObject previewObject = new("MapPreview");
            previewObject.transform.SetParent(_generatedRoot, false);
            previewObject.transform.localPosition = new Vector3(0f, 0f, -0.5f);

            _previewTexture = BuildPreviewTexture();
            _previewSprite = Sprite.Create(
                _previewTexture,
                new Rect(0f, 0f, _previewTexture.width, _previewTexture.height),
                new Vector2(0.5f, 0.5f),
                1f / _cellSize,
                0,
                SpriteMeshType.FullRect);

            SpriteRenderer spriteRenderer = previewObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _previewSprite;
            spriteRenderer.sortingOrder = 0;
        }

        private void CreatePrunedOverlay()
        {
            Transform existingOverlay = _generatedRoot.Find(PrunedOverlayName);
            if (existingOverlay != null)
            {
                if (Application.isPlaying)
                    Destroy(existingOverlay.gameObject);
                else
                    DestroyImmediate(existingOverlay.gameObject);
            }

            DestroyPreviewObject(_prunedOverlaySprite);
            DestroyPreviewObject(_prunedOverlayTexture);
            _prunedOverlaySprite = null;
            _prunedOverlayTexture = null;

            if (_lastPrunedDeadEndMask == null || !HasAnyMarkedTile(_lastPrunedDeadEndMask))
                return;

            GameObject overlayObject = new(PrunedOverlayName);
            overlayObject.transform.SetParent(_generatedRoot, false);
            overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.45f);

            _prunedOverlayTexture = BuildPrunedOverlayTexture();
            _prunedOverlaySprite = Sprite.Create(
                _prunedOverlayTexture,
                new Rect(0f, 0f, _prunedOverlayTexture.width, _prunedOverlayTexture.height),
                new Vector2(0.5f, 0.5f),
                1f / _cellSize,
                0,
                SpriteMeshType.FullRect);

            SpriteRenderer spriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _prunedOverlaySprite;
            spriteRenderer.sortingOrder = 10;
        }

        private void CreateWallCubes()
        {
            GameObject wallsRoot = new("WallRectangles");
            wallsRoot.transform.SetParent(_generatedRoot, false);

            List<RectInt> wallRectangles = BuildWallRectangles(surfaceOnly: true);
            float halfWidth = _lastResult.DisplayWidth * 0.5f;
            float halfHeight = _lastResult.DisplayHeight * 0.5f;

            for (int i = 0; i < wallRectangles.Count; i++)
            {
                RectInt rectangle = wallRectangles[i];
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"WallRect_{i:D3}";
                cube.transform.SetParent(wallsRoot.transform, false);

                float centerX = (rectangle.x + rectangle.width * 0.5f - halfWidth) * _cellSize;
                float centerY = (rectangle.y + rectangle.height * 0.5f - halfHeight) * _cellSize;
                cube.transform.localPosition = new Vector3(centerX, centerY, 0f);
                cube.transform.localScale = new Vector3(
                    rectangle.width * _cellSize,
                    rectangle.height * _cellSize,
                    _cellSize);
            }
        }

        private List<RectInt> BuildWallRectangles(bool surfaceOnly)
        {
            int width = _lastResult.DisplayWidth;
            int height = _lastResult.DisplayHeight;
            bool[,] wallMask = new bool[width, height];
            bool[,] targetMask = new bool[width, height];
            bool[,] used = new bool[width, height];
            List<RectInt> rectangles = new();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    wallMask[x, y] = IsWallTile(_lastResult.GetDisplayTile(x, y));
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    targetMask[x, y] = surfaceOnly
                        ? IsSurfaceWall(wallMask, x, y, width, height)
                        : wallMask[x, y];
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!targetMask[x, y] || used[x, y])
                        continue;

                    int rectWidth = 0;
                    while (x + rectWidth < width && targetMask[x + rectWidth, y] && !used[x + rectWidth, y])
                        rectWidth++;

                    int rectHeight = 1;
                    bool canGrow = true;
                    while (y + rectHeight < height && canGrow)
                    {
                        for (int dx = 0; dx < rectWidth; dx++)
                        {
                            if (!targetMask[x + dx, y + rectHeight] || used[x + dx, y + rectHeight])
                            {
                                canGrow = false;
                                break;
                            }
                        }

                        if (canGrow)
                            rectHeight++;
                    }

                    for (int dy = 0; dy < rectHeight; dy++)
                    {
                        for (int dx = 0; dx < rectWidth; dx++)
                            used[x + dx, y + dy] = true;
                    }

                    rectangles.Add(new RectInt(x, y, rectWidth, rectHeight));
                }
            }

            return rectangles;
        }

        private Texture2D BuildPreviewTexture()
        {
            Texture2D texture = new(_lastResult.DisplayWidth, _lastResult.DisplayHeight, TextureFormat.RGBA32, false)
            {
                name = "TunnelingDemoPreview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[_lastResult.DisplayWidth * _lastResult.DisplayHeight];
            for (int y = 0; y < _lastResult.DisplayHeight; y++)
            {
                for (int x = 0; x < _lastResult.DisplayWidth; x++)
                {
                    int pixelIndex = y * _lastResult.DisplayWidth + x;
                    pixels[pixelIndex] = GetTileColor(_lastResult.GetDisplayTile(x, y));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D BuildPrunedOverlayTexture()
        {
            Texture2D texture = new(_lastResult.DisplayWidth, _lastResult.DisplayHeight, TextureFormat.RGBA32, false)
            {
                name = "TunnelingDemoPrunedOverlay",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[_lastResult.DisplayWidth * _lastResult.DisplayHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            for (int y = 0; y < _lastResult.DisplayHeight; y++)
            {
                for (int x = 0; x < _lastResult.DisplayWidth; x++)
                {
                    int sourceIndex = GetIndex(y, x, _lastResult.SourceHeight);
                    if (_lastPrunedDeadEndMask == null || !_lastPrunedDeadEndMask[sourceIndex])
                        continue;

                    int pixelIndex = y * _lastResult.DisplayWidth + x;
                    pixels[pixelIndex] = _prunedDeadEndColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Color GetTileColor(DungeonMakerSquareData tile)
        {
            return tile switch
            {
                DungeonMakerSquareData.IR_OPEN => _roomColor,
                DungeonMakerSquareData.H_DOOR => _roomColor,
                DungeonMakerSquareData.V_DOOR => _roomColor,
                DungeonMakerSquareData.IA_OPEN => _anteRoomColor,
                DungeonMakerSquareData.OPEN => _corridorColor,
                DungeonMakerSquareData.G_OPEN => _corridorColor,
                DungeonMakerSquareData.NJ_OPEN => _corridorColor,
                DungeonMakerSquareData.NJ_G_OPEN => _corridorColor,
                DungeonMakerSquareData.IT_OPEN => _corridorColor,
                _ => _wallColor,
            };
        }

        private static bool IsWallTile(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.CLOSED
                or DungeonMakerSquareData.G_CLOSED
                or DungeonMakerSquareData.NJ_CLOSED
                or DungeonMakerSquareData.NJ_G_CLOSED
                or DungeonMakerSquareData.COLUMN;
        }

        private static bool IsSurfaceWall(bool[,] wallMask, int x, int y, int width, int height)
        {
            if (!wallMask[x, y])
                return false;

            if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                return true;

            return !wallMask[x - 1, y]
                || !wallMask[x + 1, y]
                || !wallMask[x, y - 1]
                || !wallMask[x, y + 1];
        }

        private void DestroyGeneratedRoot()
        {
            Transform existingRoot = transform.Find(GeneratedRootName);
            if (existingRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(existingRoot.gameObject);
            else
                DestroyImmediate(existingRoot.gameObject);

            _generatedRoot = null;
        }

        private void DestroyPreviewAssets()
        {
            DestroyPreviewObject(_previewSprite);
            DestroyPreviewObject(_previewTexture);
            DestroyPreviewObject(_prunedOverlaySprite);
            DestroyPreviewObject(_prunedOverlayTexture);
            _previewSprite = null;
            _previewTexture = null;
            _prunedOverlaySprite = null;
            _prunedOverlayTexture = null;
        }

        private static void DestroyPreviewObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
