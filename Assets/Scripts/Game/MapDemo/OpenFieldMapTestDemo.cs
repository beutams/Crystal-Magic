using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrystalMagic.Game.MapDemo
{
    [DisallowMultipleComponent]
    public sealed class OpenFieldMapTestDemo : MonoBehaviour
    {
        private const int MinimumMapSide = 48;
        private const int AnchorPlacementAttempts = 512;

        private enum TerrainGenerationMethod
        {
            FbmPerlin = 0,
            Voronoi = 1,
        }

        private enum MapAnchorType
        {
            Spawn = 0,
            Exit = 1,
            LargeInterest = 2,
            MediumInterest = 3,
            SmallInterest = 4,
        }

        private enum ElevationBand : byte
        {
            Low = 0,
            Middle = 1,
            High = 2,
        }

        private enum MapContentType
        {
            Chest = 0,
            InterestMonster = 1,
            WildMonster = 2,
        }

        private enum MonsterLevel : byte
        {
            None = 0,
            Level1 = 1,
            Level2 = 2,
            Level3 = 3,
        }

        private readonly struct MapAnchor
        {
            public readonly MapAnchorType Type;
            public readonly Vector2Int Cell;
            public readonly int Radius;

            public MapAnchor(MapAnchorType type, Vector2Int cell, int radius)
            {
                Type = type;
                Cell = cell;
                Radius = radius;
            }
        }

        private readonly struct MapContentSpawn
        {
            public readonly MapContentType Type;
            public readonly MonsterLevel Level;
            public readonly Vector2Int Cell;

            public MapContentSpawn(MapContentType type, MonsterLevel level, Vector2Int cell)
            {
                Type = type;
                Level = level;
                Cell = cell;
            }
        }

        private sealed class OpenFieldMapData
        {
            public readonly int Width;
            public readonly int Height;
            public readonly float[] GroundY;
            public readonly ElevationBand[] ElevationBands;
            public readonly bool[] WalkableMask;
            public readonly bool[] LineOfSightBlockerMask;
            public readonly bool[] ReachableFromSpawnMask;
            public readonly List<MapAnchor> Anchors = new();
            public readonly List<MapContentSpawn> ContentSpawns = new();
            public int ChestCount;
            public int InterestMonsterLevel1Count;
            public int InterestMonsterLevel2Count;
            public int InterestMonsterLevel3Count;
            public int WildMonsterCount;

            public OpenFieldMapData(int width, int height)
            {
                Width = width;
                Height = height;
                GroundY = new float[width * height];
                ElevationBands = new ElevationBand[width * height];
                WalkableMask = new bool[width * height];
                LineOfSightBlockerMask = new bool[width * height];
                ReachableFromSpawnMask = new bool[width * height];
            }

            public int GetIndex(int x, int y)
            {
                return y * Width + x;
            }

            public bool IsWalkableCell(int x, int y)
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    return false;

                return WalkableMask[GetIndex(x, y)];
            }
        }

        private interface ITerrainGenerationStep
        {
            void Generate(OpenFieldMapData mapData, int seed);

            bool IsWalkable(ElevationBand band);
        }

        [Serializable]
        private sealed class FbmPerlinTerrainSettings
        {
            [Range(0.01f, 0.99f)] public float LowToMiddleHeight = 0.42f;
            [Range(0.01f, 0.99f)] public float MiddleToHighHeight = 0.58f;
            [Min(2f)] public float HighFrequencyMultiplier = 6f;
            [Range(0f, 1.5f)] public float HighFrequencyAmplitude = 0.65f;

            public void Validate()
            {
                LowToMiddleHeight = Mathf.Clamp(LowToMiddleHeight, 0.01f, 0.98f);
                MiddleToHighHeight = Mathf.Clamp(MiddleToHighHeight, LowToMiddleHeight + 0.01f, 0.99f);
                HighFrequencyMultiplier = Mathf.Max(2f, HighFrequencyMultiplier);
                HighFrequencyAmplitude = Mathf.Clamp(HighFrequencyAmplitude, 0f, 1.5f);
            }
        }

        [Serializable]
        private sealed class VoronoiTerrainSettings
        {
            [Min(3)] public int RegionCount = 12;
            [Range(0f, 12f)] public float EdgeJitter = 1.5f;

            public void Validate()
            {
                RegionCount = Mathf.Max(3, RegionCount);
                EdgeJitter = Mathf.Clamp(EdgeJitter, 0f, 12f);
            }
        }

        [Serializable]
        private sealed class GameplayContentSettings
        {
            public Vector3 ChestCounts = new(1f, 2f, 3f);
            [FormerlySerializedAs("NormalMonsterCounts")]
            public Vector3 MonsterLevel1Counts = new(4f, 7f, 12f);
            [FormerlySerializedAs("EliteMonsterCounts")]
            public Vector3 MonsterLevel2Counts = new(0f, 1f, 3f);
            public Vector3 MonsterLevel3Counts = new(0f, 0f, 1f);
            [Min(0)] public int WildMonsterCount = 22;

            public void Validate()
            {
                ChestCounts = ClampCounts(ChestCounts);
                MonsterLevel1Counts = ClampCounts(MonsterLevel1Counts);
                MonsterLevel2Counts = ClampCounts(MonsterLevel2Counts);
                MonsterLevel3Counts = ClampCounts(MonsterLevel3Counts);
                WildMonsterCount = Mathf.Max(0, WildMonsterCount);
            }

            public int GetInterestCount(Vector3 counts, MapAnchorType type)
            {
                float count = type switch
                {
                    MapAnchorType.SmallInterest => counts.x,
                    MapAnchorType.MediumInterest => counts.y,
                    MapAnchorType.LargeInterest => counts.z,
                    _ => 0f,
                };
                return Mathf.RoundToInt(count);
            }

            private static Vector3 ClampCounts(Vector3 counts)
            {
                return new Vector3(
                    Mathf.Max(0f, counts.x),
                    Mathf.Max(0f, counts.y),
                    Mathf.Max(0f, counts.z));
            }
        }

        private sealed class FbmPerlinTerrainStep : ITerrainGenerationStep
        {
            private readonly FbmPerlinTerrainSettings _settings;

            public FbmPerlinTerrainStep(FbmPerlinTerrainSettings settings)
            {
                _settings = settings;
            }

            public bool IsWalkable(ElevationBand band)
            {
                return band == ElevationBand.Middle;
            }

            public void Generate(OpenFieldMapData mapData, int seed)
            {
                System.Random random = new(seed);
                float offsetX = random.Next(-10000, 10001);
                float offsetY = random.Next(-10000, 10001);
                float baseScale = Mathf.Clamp(Mathf.Min(mapData.Width, mapData.Height) * 0.65f, 30f, 64f);

                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        int index = mapData.GetIndex(x, y);
                        float height = SampleFbmPerlin(x, y, offsetX, offsetY, baseScale);
                        mapData.GroundY[index] = height;
                        mapData.ElevationBands[index] = GetElevationBand(height);
                    }
                }
            }

            private float SampleFbmPerlin(float x, float y, float offsetX, float offsetY, float baseScale)
            {
                float macro = Mathf.PerlinNoise((x + offsetX) / baseScale, (y + offsetY) / baseScale);
                float medium = Mathf.PerlinNoise((x + offsetX) * 2f / baseScale, (y + offsetY) * 2f / baseScale);
                float detail = Mathf.PerlinNoise(
                    (x + offsetX) * _settings.HighFrequencyMultiplier / baseScale,
                    (y + offsetY) * _settings.HighFrequencyMultiplier / baseScale);
                return (macro + medium * 0.5f + detail * _settings.HighFrequencyAmplitude) /
                       (1.5f + _settings.HighFrequencyAmplitude);
            }

            private ElevationBand GetElevationBand(float height)
            {
                if (height < _settings.LowToMiddleHeight)
                    return ElevationBand.Low;
                if (height < _settings.MiddleToHighHeight)
                    return ElevationBand.Middle;

                return ElevationBand.High;
            }
        }

        private sealed class VoronoiTerrainStep : ITerrainGenerationStep
        {
            private readonly VoronoiTerrainSettings _settings;

            private readonly struct VoronoiSite
            {
                public readonly Vector2 Position;
                public readonly ElevationBand Band;

                public VoronoiSite(Vector2 position, ElevationBand band)
                {
                    Position = position;
                    Band = band;
                }
            }

            public VoronoiTerrainStep(VoronoiTerrainSettings settings)
            {
                _settings = settings;
            }

            public bool IsWalkable(ElevationBand band)
            {
                return band != ElevationBand.High;
            }

            public void Generate(OpenFieldMapData mapData, int seed)
            {
                System.Random random = new(seed);
                List<VoronoiSite> sites = CreateSites(mapData, random, _settings.RegionCount);
                float jitterOffsetX = random.Next(-10000, 10001);
                float jitterOffsetY = random.Next(-10000, 10001);

                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        Vector2 samplePosition = GetSamplePosition(x, y, jitterOffsetX, jitterOffsetY);
                        VoronoiSite site = FindClosestSite(sites, samplePosition);
                        int index = mapData.GetIndex(x, y);
                        mapData.ElevationBands[index] = site.Band;
                        mapData.GroundY[index] = GetBandHeight(site.Band);
                    }
                }
            }

            private static List<VoronoiSite> CreateSites(OpenFieldMapData mapData, System.Random random, int count)
            {
                List<VoronoiSite> sites = new(count);
                for (int i = 0; i < count; i++)
                {
                    ElevationBand band = i < 3 ? (ElevationBand)i : (ElevationBand)random.Next(0, 3);
                    Vector2 position = new(
                        (float)random.NextDouble() * (mapData.Width - 1),
                        (float)random.NextDouble() * (mapData.Height - 1));
                    sites.Add(new VoronoiSite(position, band));
                }

                return sites;
            }

            private Vector2 GetSamplePosition(float x, float y, float offsetX, float offsetY)
            {
                if (_settings.EdgeJitter <= 0f)
                    return new Vector2(x, y);

                float jitterX = (Mathf.PerlinNoise((x + offsetX) * 0.06f, (y + offsetY) * 0.06f) - 0.5f) * _settings.EdgeJitter * 2f;
                float jitterY = (Mathf.PerlinNoise((x + offsetX + 163f) * 0.06f, (y + offsetY - 241f) * 0.06f) - 0.5f) * _settings.EdgeJitter * 2f;
                return new Vector2(x + jitterX, y + jitterY);
            }

            private static VoronoiSite FindClosestSite(List<VoronoiSite> sites, Vector2 samplePosition)
            {
                VoronoiSite closest = sites[0];
                float closestDistanceSq = (samplePosition - closest.Position).sqrMagnitude;
                for (int i = 1; i < sites.Count; i++)
                {
                    VoronoiSite candidate = sites[i];
                    float distanceSq = (samplePosition - candidate.Position).sqrMagnitude;
                    if (distanceSq >= closestDistanceSq)
                        continue;

                    closest = candidate;
                    closestDistanceSq = distanceSq;
                }

                return closest;
            }

            private static float GetBandHeight(ElevationBand band)
            {
                return band switch
                {
                    ElevationBand.Low => 0.2f,
                    ElevationBand.Middle => 0.5f,
                    ElevationBand.High => 0.8f,
                    _ => 0.5f,
                };
            }
        }

        [Header("Map Size")]
        [SerializeField, Min(MinimumMapSide)] private int _mapWidth = 80;
        [SerializeField, Min(MinimumMapSide)] private int _mapHeight = 200;
        [Header("Terrain Generation")]
        [SerializeField] private TerrainGenerationMethod _terrainGenerationMethod = TerrainGenerationMethod.Voronoi;
        [SerializeField] private FbmPerlinTerrainSettings _fbmPerlinTerrain = new();
        [SerializeField] private VoronoiTerrainSettings _voronoiTerrain = new();
        [Header("Gameplay Content")]
        [SerializeField] private GameplayContentSettings _gameplayContent = new();

        private static readonly Color LowHeightColor = new(0.03f, 0.07f, 0.1f, 1f);
        private static readonly Color MidHeightColor = new(0.41f, 0.62f, 0.36f, 1f);
        private static readonly Color HighHeightColor = new(0.48f, 0.28f, 0.19f, 1f);
        private static readonly Color SpawnColor = new(0.25f, 0.47f, 0.98f, 1f);
        private static readonly Color ExitColor = new(0.88f, 0.28f, 0.83f, 1f);
        private static readonly Color LargeInterestColor = new(0.88f, 0.35f, 0.26f, 1f);
        private static readonly Color MediumInterestColor = new(0.94f, 0.70f, 0.24f, 1f);
        private static readonly Color SmallInterestColor = new(0.39f, 0.79f, 0.34f, 1f);
        private static readonly Color ChestColor = new(1f, 0.8f, 0.12f, 1f);
        private static readonly Color MonsterLevel1Color = new(0.91f, 0.17f, 0.12f, 1f);
        private static readonly Color MonsterLevel2Color = new(0.93f, 0.43f, 0.08f, 1f);
        private static readonly Color MonsterLevel3Color = new(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color WildMonsterColor = new(0.78f, 0.78f, 0.78f, 1f);

        private Texture2D _mapTexture;
        private GUIStyle _legendLabelStyle;
        private OpenFieldMapData _lastMapData;
        private int _seedStreamState;
        private int _previewSeed;

        public bool HasPreviewTexture => _mapTexture != null;
        public Texture2D PreviewTexture => _mapTexture;
        public int PreviewWidth => _lastMapData?.Width ?? 0;
        public int PreviewHeight => _lastMapData?.Height ?? 0;
        public int PreviewSeed => _previewSeed;
        public string PreviewContentSummary => _lastMapData == null
            ? "Content: none"
            : $"Chests: {_lastMapData.ChestCount}  Interest: L1 {_lastMapData.InterestMonsterLevel1Count} / L2 {_lastMapData.InterestMonsterLevel2Count} / L3 {_lastMapData.InterestMonsterLevel3Count}  Wild: {_lastMapData.WildMonsterCount}";
        public string TerrainGenerationName => _terrainGenerationMethod switch
        {
            TerrainGenerationMethod.FbmPerlin => "fBm Perlin",
            TerrainGenerationMethod.Voronoi => "Voronoi",
            _ => "Voronoi",
        };

        private void Start()
        {
            GenerateDemo();
        }

        private void OnDestroy()
        {
            CleanupTexture();
        }

        private void OnValidate()
        {
            _mapWidth = Mathf.Max(MinimumMapSide, _mapWidth);
            _mapHeight = Mathf.Max(MinimumMapSide, _mapHeight);
            _fbmPerlinTerrain ??= new FbmPerlinTerrainSettings();
            _voronoiTerrain ??= new VoronoiTerrainSettings();
            _gameplayContent ??= new GameplayContentSettings();
            _fbmPerlinTerrain.Validate();
            _voronoiTerrain.Validate();
            _gameplayContent.Validate();
        }

        [ContextMenu("Generate Open Field Map")]
        public void GenerateDemo()
        {
            OpenFieldMapData mapData;
            int seed;
            do
            {
                seed = NextSeed();
                mapData = CreateEmptyMapData(_mapWidth, _mapHeight);
                ITerrainGenerationStep terrainStep = GetTerrainGenerationStep();
                terrainStep.Generate(mapData, seed);
                BuildTraversalData(mapData, terrainStep);
            }
            while (!TryPlanAnchors(mapData, new System.Random(seed)) ||
                   !CanReachAllAnchors(mapData) ||
                   !TryPopulateGameplayContent(mapData, seed));

            _lastMapData = mapData;
            _previewSeed = seed;
            BuildTexture(mapData);
            RequestPreviewRepaint();
        }

        private ITerrainGenerationStep GetTerrainGenerationStep()
        {
            return _terrainGenerationMethod switch
            {
                TerrainGenerationMethod.FbmPerlin => new FbmPerlinTerrainStep(_fbmPerlinTerrain),
                TerrainGenerationMethod.Voronoi => new VoronoiTerrainStep(_voronoiTerrain),
                _ => new VoronoiTerrainStep(_voronoiTerrain),
            };
        }

        private int NextSeed()
        {
            if (_seedStreamState == 0)
                _seedStreamState = Environment.TickCount;

            unchecked
            {
                _seedStreamState = _seedStreamState * 1103515245 + 12345;
            }

            return _seedStreamState;
        }

        private static OpenFieldMapData CreateEmptyMapData(int width, int height)
        {
            return new OpenFieldMapData(
                Mathf.Max(MinimumMapSide, width),
                Mathf.Max(MinimumMapSide, height));
        }

        private static bool TryPlanAnchors(OpenFieldMapData mapData, System.Random random)
        {
            int border = 7;
            if (!TryPlaceAnchor(mapData, random, MapAnchorType.Spawn, 4, border, 0, mapData.Width / 3))
                return false;
            if (!TryPlaceAnchor(mapData, random, MapAnchorType.Exit, 4, border, mapData.Width * 2 / 3, mapData.Width))
                return false;

            int area = mapData.Width * mapData.Height;
            if (!TryPlaceInterestGroup(mapData, random, MapAnchorType.LargeInterest, 7, Mathf.Clamp(area / 7000, 1, 3)))
                return false;
            if (!TryPlaceInterestGroup(mapData, random, MapAnchorType.MediumInterest, 5, Mathf.Clamp(area / 3500, 2, 6)))
                return false;
            if (!TryPlaceInterestGroup(mapData, random, MapAnchorType.SmallInterest, 3, Mathf.Clamp(area / 1600, 4, 12)))
                return false;

            return true;
        }

        private static void BuildTraversalData(OpenFieldMapData mapData, ITerrainGenerationStep terrainStep)
        {
            for (int index = 0; index < mapData.ElevationBands.Length; index++)
            {
                ElevationBand band = mapData.ElevationBands[index];
                mapData.WalkableMask[index] = terrainStep.IsWalkable(band);
                mapData.LineOfSightBlockerMask[index] = band == ElevationBand.High;
            }
        }

        private static bool CanReachAllAnchors(OpenFieldMapData mapData)
        {
            int spawnIndex = -1;
            for (int i = 0; i < mapData.Anchors.Count; i++)
            {
                MapAnchor anchor = mapData.Anchors[i];
                if (anchor.Type == MapAnchorType.Spawn)
                {
                    spawnIndex = mapData.GetIndex(anchor.Cell.x, anchor.Cell.y);
                    break;
                }
            }

            if (spawnIndex < 0 || !mapData.WalkableMask[spawnIndex])
                return false;

            bool[] visited = mapData.ReachableFromSpawnMask;
            Array.Clear(visited, 0, visited.Length);
            Queue<int> pendingCells = new();
            visited[spawnIndex] = true;
            pendingCells.Enqueue(spawnIndex);
            while (pendingCells.Count > 0)
            {
                int index = pendingCells.Dequeue();
                int x = index % mapData.Width;
                int y = index / mapData.Width;
                TryVisitReachableCell(mapData, x - 1, y, visited, pendingCells);
                TryVisitReachableCell(mapData, x + 1, y, visited, pendingCells);
                TryVisitReachableCell(mapData, x, y - 1, visited, pendingCells);
                TryVisitReachableCell(mapData, x, y + 1, visited, pendingCells);
            }

            for (int i = 0; i < mapData.Anchors.Count; i++)
            {
                MapAnchor anchor = mapData.Anchors[i];
                if (anchor.Type == MapAnchorType.Spawn)
                    continue;
                if (!visited[mapData.GetIndex(anchor.Cell.x, anchor.Cell.y)])
                    return false;
            }

            return true;
        }

        private static void TryVisitReachableCell(
            OpenFieldMapData mapData,
            int x,
            int y,
            bool[] visited,
            Queue<int> pendingCells)
        {
            if (!mapData.IsWalkableCell(x, y))
                return;

            int index = mapData.GetIndex(x, y);
            if (visited[index])
                return;

            visited[index] = true;
            pendingCells.Enqueue(index);
        }

        private bool TryPopulateGameplayContent(OpenFieldMapData mapData, int seed)
        {
            System.Random random = new(seed ^ 0x5F3759DF);
            for (int i = 0; i < mapData.Anchors.Count; i++)
            {
                MapAnchor anchor = mapData.Anchors[i];
                if (!TryPopulateInterestPoint(mapData, random, anchor, _gameplayContent))
                    return false;
            }

            for (int i = 0; i < _gameplayContent.WildMonsterCount; i++)
            {
                if (!TryPlaceWildMonster(mapData, random))
                    return false;
            }

            return true;
        }

        private static bool TryPopulateInterestPoint(
            OpenFieldMapData mapData,
            System.Random random,
            MapAnchor anchor,
            GameplayContentSettings settings)
        {
            GetInterestContentCounts(anchor.Type, settings, out int chestCount, out int level1MonsterCount, out int level2MonsterCount, out int level3MonsterCount);
            for (int i = 0; i < chestCount; i++)
            {
                if (!TryPlaceContentInsideInterest(mapData, random, anchor, MapContentType.Chest, MonsterLevel.None))
                    return false;
            }

            for (int i = 0; i < level1MonsterCount; i++)
            {
                if (!TryPlaceContentInsideInterest(mapData, random, anchor, MapContentType.InterestMonster, MonsterLevel.Level1))
                    return false;
            }

            for (int i = 0; i < level2MonsterCount; i++)
            {
                if (!TryPlaceContentInsideInterest(mapData, random, anchor, MapContentType.InterestMonster, MonsterLevel.Level2))
                    return false;
            }

            for (int i = 0; i < level3MonsterCount; i++)
            {
                if (!TryPlaceContentInsideInterest(mapData, random, anchor, MapContentType.InterestMonster, MonsterLevel.Level3))
                    return false;
            }

            return true;
        }

        private static void GetInterestContentCounts(
            MapAnchorType type,
            GameplayContentSettings settings,
            out int chestCount,
            out int level1MonsterCount,
            out int level2MonsterCount,
            out int level3MonsterCount)
        {
            chestCount = settings.GetInterestCount(settings.ChestCounts, type);
            level1MonsterCount = settings.GetInterestCount(settings.MonsterLevel1Counts, type);
            level2MonsterCount = settings.GetInterestCount(settings.MonsterLevel2Counts, type);
            level3MonsterCount = settings.GetInterestCount(settings.MonsterLevel3Counts, type);
        }

        private static bool TryPlaceContentInsideInterest(
            OpenFieldMapData mapData,
            System.Random random,
            MapAnchor anchor,
            MapContentType type,
            MonsterLevel level)
        {
            int innerRadius = Mathf.Max(1, anchor.Radius - 1);
            int innerRadiusSquared = innerRadius * innerRadius;
            for (int attempt = 0; attempt < AnchorPlacementAttempts; attempt++)
            {
                Vector2Int cell = new(
                    random.Next(anchor.Cell.x - innerRadius, anchor.Cell.x + innerRadius + 1),
                    random.Next(anchor.Cell.y - innerRadius, anchor.Cell.y + innerRadius + 1));
                int deltaX = cell.x - anchor.Cell.x;
                int deltaY = cell.y - anchor.Cell.y;
                if (deltaX * deltaX + deltaY * deltaY > innerRadiusSquared)
                    continue;
                if (!mapData.IsWalkableCell(cell.x, cell.y) || !IsContentCellAvailable(mapData, cell))
                    continue;

                AddContentSpawn(mapData, new MapContentSpawn(type, level, cell));
                return true;
            }

            return false;
        }

        private static bool TryPlaceWildMonster(OpenFieldMapData mapData, System.Random random)
        {
            for (int attempt = 0; attempt < AnchorPlacementAttempts; attempt++)
            {
                Vector2Int cell = new(random.Next(0, mapData.Width), random.Next(0, mapData.Height));
                int index = mapData.GetIndex(cell.x, cell.y);
                if (!mapData.ReachableFromSpawnMask[index] ||
                    !IsOutsideAnchorAreas(mapData, cell) ||
                    !IsContentCellAvailable(mapData, cell))
                {
                    continue;
                }

                AddContentSpawn(mapData, new MapContentSpawn(MapContentType.WildMonster, MonsterLevel.Level1, cell));
                return true;
            }

            return false;
        }

        private static bool IsOutsideAnchorAreas(OpenFieldMapData mapData, Vector2Int cell)
        {
            for (int i = 0; i < mapData.Anchors.Count; i++)
            {
                MapAnchor anchor = mapData.Anchors[i];
                int protectedRadius = anchor.Radius + 2;
                if ((cell - anchor.Cell).sqrMagnitude <= protectedRadius * protectedRadius)
                    return false;
            }

            return true;
        }

        private static bool IsContentCellAvailable(OpenFieldMapData mapData, Vector2Int cell)
        {
            for (int i = 0; i < mapData.ContentSpawns.Count; i++)
            {
                if (mapData.ContentSpawns[i].Cell == cell)
                    return false;
            }

            return true;
        }

        private static void AddContentSpawn(OpenFieldMapData mapData, MapContentSpawn content)
        {
            mapData.ContentSpawns.Add(content);
            switch (content.Type)
            {
                case MapContentType.Chest:
                    mapData.ChestCount++;
                    break;
                case MapContentType.InterestMonster when content.Level == MonsterLevel.Level3:
                    mapData.InterestMonsterLevel3Count++;
                    break;
                case MapContentType.InterestMonster when content.Level == MonsterLevel.Level2:
                    mapData.InterestMonsterLevel2Count++;
                    break;
                case MapContentType.InterestMonster:
                    mapData.InterestMonsterLevel1Count++;
                    break;
                case MapContentType.WildMonster:
                    mapData.WildMonsterCount++;
                    break;
            }
        }

        private static bool TryPlaceInterestGroup(
            OpenFieldMapData mapData,
            System.Random random,
            MapAnchorType type,
            int radius,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryPlaceAnchor(mapData, random, type, radius, radius + 3, 0, mapData.Width))
                    return false;
            }

            return true;
        }

        private static bool TryPlaceAnchor(
            OpenFieldMapData mapData,
            System.Random random,
            MapAnchorType type,
            int radius,
            int border,
            int minX,
            int maxX)
        {
            int minY = border;
            int maxY = mapData.Height - border;
            minX = Mathf.Max(border, minX);
            maxX = Mathf.Min(mapData.Width - border, maxX);
            if (minX >= maxX || minY >= maxY)
                return false;

            for (int attempt = 0; attempt < AnchorPlacementAttempts; attempt++)
            {
                Vector2Int cell = new(random.Next(minX, maxX), random.Next(minY, maxY));
                if (!CanPlaceAnchor(mapData, cell, radius))
                    continue;

                mapData.Anchors.Add(new MapAnchor(type, cell, radius));
                return true;
            }

            return false;
        }

        private static bool CanPlaceAnchor(OpenFieldMapData mapData, Vector2Int candidate, int candidateRadius)
        {
            if (!IsWalkableArea(mapData, candidate, candidateRadius))
                return false;

            const int gap = 4;
            for (int i = 0; i < mapData.Anchors.Count; i++)
            {
                MapAnchor existing = mapData.Anchors[i];
                int requiredDistance = candidateRadius + existing.Radius + gap;
                if ((candidate - existing.Cell).sqrMagnitude < requiredDistance * requiredDistance)
                    return false;
            }

            return true;
        }

        private static bool IsWalkableArea(OpenFieldMapData mapData, Vector2Int center, int radius)
        {
            int radiusSquared = radius * radius;
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    int deltaX = x - center.x;
                    int deltaY = y - center.y;
                    if (deltaX * deltaX + deltaY * deltaY > radiusSquared)
                        continue;
                    if (!mapData.IsWalkableCell(x, y))
                        return false;
                }
            }

            return true;
        }

        private void BuildTexture(OpenFieldMapData mapData)
        {
            CleanupTexture();

            _mapTexture = new Texture2D(mapData.Width, mapData.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "OpenFieldMapPreview",
            };

            Color[] pixels = new Color[mapData.Width * mapData.Height];
            for (int y = 0; y < mapData.Height; y++)
            {
                for (int x = 0; x < mapData.Width; x++)
                {
                    int index = mapData.GetIndex(x, y);
                    pixels[index] = GetElevationBandColor(mapData.ElevationBands[index]);
                }
            }

            for (int i = 0; i < mapData.Anchors.Count; i++)
                PaintAnchor(pixels, mapData, mapData.Anchors[i]);
            for (int i = 0; i < mapData.ContentSpawns.Count; i++)
                PaintContent(pixels, mapData, mapData.ContentSpawns[i]);

            _mapTexture.SetPixels(pixels);
            _mapTexture.Apply(false, false);
        }

        private static void PaintAnchor(Color[] pixels, OpenFieldMapData mapData, MapAnchor anchor)
        {
            Color color = GetAnchorColor(anchor.Type);
            int radiusSquared = anchor.Radius * anchor.Radius;
            for (int y = anchor.Cell.y - anchor.Radius; y <= anchor.Cell.y + anchor.Radius; y++)
            {
                for (int x = anchor.Cell.x - anchor.Radius; x <= anchor.Cell.x + anchor.Radius; x++)
                {
                    if (x < 0 || x >= mapData.Width || y < 0 || y >= mapData.Height)
                        continue;

                    int deltaX = x - anchor.Cell.x;
                    int deltaY = y - anchor.Cell.y;
                    if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                        pixels[mapData.GetIndex(x, y)] = color;
                }
            }
        }

        private static void PaintContent(Color[] pixels, OpenFieldMapData mapData, MapContentSpawn content)
        {
            int radius = content.Level switch
            {
                MonsterLevel.Level2 => 1,
                MonsterLevel.Level3 => 2,
                _ => 0,
            };
            Color color = GetContentColor(content);
            for (int y = content.Cell.y - radius; y <= content.Cell.y + radius; y++)
            {
                for (int x = content.Cell.x - radius; x <= content.Cell.x + radius; x++)
                {
                    if (x < 0 || x >= mapData.Width || y < 0 || y >= mapData.Height)
                        continue;

                    pixels[mapData.GetIndex(x, y)] = color;
                }
            }
        }

        private static Color GetContentColor(MapContentSpawn content)
        {
            return content.Type switch
            {
                MapContentType.Chest => ChestColor,
                MapContentType.InterestMonster when content.Level == MonsterLevel.Level3 => MonsterLevel3Color,
                MapContentType.InterestMonster when content.Level == MonsterLevel.Level2 => MonsterLevel2Color,
                MapContentType.InterestMonster => MonsterLevel1Color,
                MapContentType.WildMonster => WildMonsterColor,
                _ => Color.white,
            };
        }

        private static Color GetAnchorColor(MapAnchorType type)
        {
            return type switch
            {
                MapAnchorType.Spawn => SpawnColor,
                MapAnchorType.Exit => ExitColor,
                MapAnchorType.LargeInterest => LargeInterestColor,
                MapAnchorType.MediumInterest => MediumInterestColor,
                MapAnchorType.SmallInterest => SmallInterestColor,
                _ => MidHeightColor,
            };
        }

        private Color GetElevationBandColor(ElevationBand band)
        {
            if (_terrainGenerationMethod == TerrainGenerationMethod.Voronoi && band != ElevationBand.High)
                return MidHeightColor;

            return band switch
            {
                ElevationBand.Low => LowHeightColor,
                ElevationBand.Middle => MidHeightColor,
                ElevationBand.High => HighHeightColor,
                _ => MidHeightColor,
            };
        }

        private void CleanupTexture()
        {
            if (_mapTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_mapTexture);
            else
                DestroyImmediate(_mapTexture);

            _mapTexture = null;
        }

        private void OnGUI()
        {
            if (_mapTexture == null)
                return;

            float padding = 20f;
            float availableWidth = Mathf.Max(1f, Screen.width - padding * 2f);
            float availableHeight = Mathf.Max(1f, Screen.height - padding * 2f);
            float scale = Mathf.Min(availableWidth / _mapTexture.width, availableHeight / _mapTexture.height);
            float drawWidth = _mapTexture.width * scale;
            float drawHeight = _mapTexture.height * scale;
            Rect drawRect = new(
                (Screen.width - drawWidth) * 0.5f,
                (Screen.height - drawHeight) * 0.5f,
                drawWidth,
                drawHeight);

            GUI.DrawTexture(drawRect, _mapTexture, ScaleMode.StretchToFill, false);
            DrawLegend(drawRect);
        }

        private void DrawLegend(Rect mapRect)
        {
            _legendLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontSize = 14,
                richText = false,
            };

            float x = Mathf.Max(20f, mapRect.xMin);
            float y = Mathf.Max(20f, mapRect.yMin - 314f);
            GUI.Box(new Rect(x, y, 300f, 320f), $"Open Field Map - {TerrainGenerationName}");
            GUI.Label(new Rect(x + 12f, y + 24f, 276f, 20f), $"Seed: {PreviewSeed}", _legendLabelStyle);
            GUI.Label(new Rect(x + 12f, y + 44f, 276f, 20f), $"Map: {PreviewWidth} x {PreviewHeight}", _legendLabelStyle);
            GUI.Label(new Rect(x + 12f, y + 64f, 276f, 20f), GetTerrainSettingsSummary(), _legendLabelStyle);
            GUI.Label(new Rect(x + 12f, y + 84f, 276f, 20f), GetTraversalSummary(), _legendLabelStyle);
            GUI.Label(new Rect(x + 12f, y + 104f, 276f, 20f), "Reachability: Spawn reaches every anchor", _legendLabelStyle);
            DrawLegendEntry(x + 12f, y + 132f, GetLowElevationColor(), GetLowElevationLabel());
            DrawLegendEntry(x + 150f, y + 132f, MidHeightColor, "Mid / Walkable");
            DrawLegendEntry(x + 12f, y + 156f, HighHeightColor, "High / Cliff");
            DrawLegendEntry(x + 150f, y + 156f, SpawnColor, "Spawn");
            DrawLegendEntry(x + 12f, y + 180f, ExitColor, "Exit");
            DrawLegendEntry(x + 150f, y + 180f, LargeInterestColor, "Large Interest");
            DrawLegendEntry(x + 12f, y + 204f, MediumInterestColor, "Medium Interest");
            DrawLegendEntry(x + 150f, y + 204f, SmallInterestColor, "Small Interest");
            DrawLegendEntry(x + 12f, y + 228f, ChestColor, "Chest");
            DrawLegendEntry(x + 150f, y + 228f, MonsterLevel1Color, "Monster Level 1");
            DrawLegendEntry(x + 12f, y + 252f, MonsterLevel2Color, "Monster Level 2");
            DrawLegendEntry(x + 150f, y + 252f, MonsterLevel3Color, "Monster Level 3");
            DrawLegendEntry(x + 12f, y + 276f, WildMonsterColor, "Wild Monster (L1)");
        }

        private string GetTerrainSettingsSummary()
        {
            return _terrainGenerationMethod switch
            {
                TerrainGenerationMethod.FbmPerlin => $"Detail: {_fbmPerlinTerrain.HighFrequencyMultiplier:0.0}x  Amplitude: {_fbmPerlinTerrain.HighFrequencyAmplitude:0.00}",
                TerrainGenerationMethod.Voronoi => $"Regions: {_voronoiTerrain.RegionCount}  Edge Jitter: {_voronoiTerrain.EdgeJitter:0.0}",
                _ => string.Empty,
            };
        }

        private string GetTraversalSummary()
        {
            return _terrainGenerationMethod switch
            {
                TerrainGenerationMethod.FbmPerlin => "Walkable: Middle only; Low Void; High Cliff",
                TerrainGenerationMethod.Voronoi => "Walkable: Low + Middle; High Cliff",
                _ => string.Empty,
            };
        }

        private Color GetLowElevationColor()
        {
            return _terrainGenerationMethod == TerrainGenerationMethod.Voronoi ? MidHeightColor : LowHeightColor;
        }

        private string GetLowElevationLabel()
        {
            return _terrainGenerationMethod == TerrainGenerationMethod.FbmPerlin
                ? "Low / Void"
                : "Low / Walkable";
        }

        private void DrawLegendEntry(float x, float y, Color color, string label)
        {
            Color originalColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y + 2f, 14f, 14f), Texture2D.whiteTexture);
            GUI.color = originalColor;
            GUI.Label(new Rect(x + 20f, y, 110f, 20f), label, _legendLabelStyle);
        }

        private static void RequestPreviewRepaint()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
        }
    }
}
