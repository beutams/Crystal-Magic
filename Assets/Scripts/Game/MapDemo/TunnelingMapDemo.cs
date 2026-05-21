using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CrystalMagic.Game.MapDemo
{
    [System.Serializable]
    public sealed class TunnelingMapDemoGenerationSnapshot
    {
        public string Stage;
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
        public int StartRoomRegionId;
        public int NextLevelRoomRegionId;
        public DungeonMakerTunnelingStats Stats;
    }

    [System.Serializable]
    public struct EncounterCountRange
    {
        public int Min;
        public int Max;

        public EncounterCountRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }

    [DisallowMultipleComponent]
    public sealed class TunnelingMapDemo : MonoBehaviour
    {
        private const string GeneratedRootName = "__GeneratedTunnelingDemo";
        private const string AnalysisOverlayName = "AnalysisOverlay";
        private const string SkeletonOverlayName = "SkeletonOverlay";
        private const string DebugCoordinateMarkerName = "DebugCoordinateMarker";
        private const string GenerationReportDirectoryName = "Logs";
        private const string GenerationReportFileName = "TunnelingMapDemoGenerationStats.txt";
        private const string MapDumpFileName = "TunnelingMapDemoMapDump.txt";
        private const bool EnableWallCubeGeneration = false;

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
        [SerializeField] private Color _mobLevel1Color = new(0.83f, 0.34f, 0.34f);
        [SerializeField] private Color _mobLevel2Color = new(0.92f, 0.28f, 0.28f);
        [SerializeField] private Color _mobLevel3Color = new(0.66f, 0.10f, 0.10f);
        [SerializeField] private Color _treasureLevel1Color = new(0.28f, 0.58f, 0.93f);
        [SerializeField] private Color _treasureLevel2Color = new(0.20f, 0.43f, 0.98f);
        [SerializeField] private Color _treasureLevel3Color = new(0.14f, 0.82f, 0.95f);
        [SerializeField] private Color _specialRoomColor = new(0.24f, 0.62f, 0.28f);
        [SerializeField] private bool _drawAnalysisOverlay = true;
        [SerializeField] private Color _deadEndColor = new(0.20f, 0.86f, 1f, 0.76f);
        [SerializeField] private bool _drawSkeletonOverlay;
        [SerializeField] private Color _skeletonCorridorColor = new(1f, 0.22f, 0.22f, 0.86f);
        [SerializeField] private Color _skeletonAnchorColor = new(0.10f, 1f, 0.96f, 0.92f);
        [SerializeField] private bool _generateWallCubes;

        [Header("Quality Filter")]
        [SerializeField] private bool _requireQualifiedMap = true;
        [SerializeField, Min(1)] private int _maxQualificationAttempts = 64;
        [SerializeField] private Vector2Int _largeRoomRange = new(5, 7);
        [SerializeField] private Vector2Int _mediumRoomRange = new(10, 15);
        [SerializeField] private Vector2Int _smallRoomRange = new(20, 25);
        [SerializeField] private Vector2Int _walkableTileRange = new(4000, 6000);

        [Header("Post Process")]
        [SerializeField] private bool _pruneDeadEnds;

        [Header("Encounter Rules")]
        [SerializeField, Min(1)] private int _corridorLevel1SpawnChanceDenominator = 100;
        [SerializeField] private EncounterCountRange _anteRoomMonsterCountRange = new(1, 2);
        [SerializeField] private EncounterCountRange _smallRoomMonsterCountRange = new(1, 2);
        [SerializeField] private EncounterCountRange _mediumRoomMonsterCountRange = new(2, 4);
        [SerializeField] private EncounterCountRange _largeRoomMonsterCountRange = new(4, 7);

        [Header("Debug")]
        [SerializeField] private Vector2Int _debugDisplayCoordinate;
        [SerializeField] private Color _debugCoordinateMarkerColor = new(1f, 0.18f, 0.18f, 1f);
        [SerializeField] private bool _showDebugCoordinateMarker;

        [Header("Last Result")]
        [SerializeField] private string _lastStage;
        [SerializeField] private int _lastGeneratedSeed;
        [SerializeField] private TunnelingMapDemoGenerationSnapshot _lastGeneration;

        private DungeonMakerTunnelingResult _lastResult;
        private DungeonMakerTunnelingResult _lastRawResult;
        private bool[] _lastDeadEndMask;
        private int _lastMasterSeed;
        private int _lastAcceptedAttemptIndex;
        private int _lastAttemptCount;
        private bool _lastQualified;
        private int _lastQualityScore;
        private DungeonMakerTunnelingGenerator.Stepper _debugStepper;
        private int _debugStepCount;
        private Transform _generatedRoot;
        private Texture2D _previewTexture;
        private Sprite _previewSprite;
        private Texture2D _analysisOverlayTexture;
        private Sprite _analysisOverlaySprite;
        private Texture2D _skeletonOverlayTexture;
        private Sprite _skeletonOverlaySprite;
        private Material _debugCoordinateMarkerMaterial;
#if UNITY_EDITOR
        private bool _pendingEditorDebugMarkerRefresh;
#endif

        public int Seed => _seed;
        public int LastGeneratedSeed => _lastGeneratedSeed;
        public DungeonMakerTunnelingConfig Config => _config;
        public static string GenerationReportFilePath => GetGenerationReportFilePath();
        public static string MapDumpFilePath => GetMapDumpFilePath();
        public bool HasDisplayMap => _lastResult != null;

        [ContextMenu("Generate DEMO Map")]
        public void GenerateDemoMap()
        {
            _debugStepper = null;
            _debugStepCount = 0;
            if (_randomizeSeedOnGenerate)
                _seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            ValidateParameters();
            int masterSeed = _seed;
            _lastRawResult = GenerateQualifiedRawResult(
                masterSeed,
                out int acceptedAttemptIndex,
                out int attemptCount,
                out bool qualified,
                out int qualityScore);
            _lastResult = _lastRawResult;
            _lastDeadEndMask = null;
            _lastMasterSeed = masterSeed;
            _lastAcceptedAttemptIndex = acceptedAttemptIndex;
            _lastAttemptCount = attemptCount;
            _lastQualified = qualified;
            _lastQualityScore = qualityScore;
            _lastStage = "Raw";
            _lastGeneratedSeed = _lastResult.Seed;
            _lastGeneration = BuildGenerationSnapshot(masterSeed, _lastResult, acceptedAttemptIndex, attemptCount, qualified, qualityScore, 0, _lastStage);
            RebuildVisuals();
            LogGenerationSummary();
            AppendGenerationReport();
            AppendMapDump();
        }

        [ContextMenu("Generate DEMO Map With New Seed")]
        public void GenerateDemoMapWithNewSeed()
        {
            bool originalRandomize = _randomizeSeedOnGenerate;

            _randomizeSeedOnGenerate = true;
            GenerateDemoMap();
            _randomizeSeedOnGenerate = originalRandomize;
        }

        [ContextMenu("Apply DEMO Dead End Deletions")]
        public void ApplyDemoDeadEndDeletions()
        {
            if (_lastRawResult == null)
                return;

            ApplyProcessedResult(ProcessDeadEndsOnly(_lastRawResult, out int removedDeadEndTiles, out bool[] deadEndMask), deadEndMask, removedDeadEndTiles, "DeadEndOnly");
        }

        [ContextMenu("Initialize DEMO Step Debug")]
        public void InitializeStepDebug()
        {
            _debugStepper = null;
            _debugStepCount = 0;

            if (_randomizeSeedOnGenerate)
                _seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            ValidateParameters();
            _debugStepper = new DungeonMakerTunnelingGenerator.Stepper(_seed, _config);
            _lastMasterSeed = _seed;
            _lastAcceptedAttemptIndex = 0;
            _lastAttemptCount = 0;
            _lastQualified = false;
            _lastQualityScore = 0;
            ApplyStepResult(_debugStepper.BuildResult(), "StepInit");
        }

        [ContextMenu("Step DEMO Debug Once")]
        public void StepDebugOnce()
        {
            if (_debugStepper == null)
                return;

            DungeonMakerTunnelingGenerator.StepResult stepResult = _debugStepper.StepOnce();
            _debugStepCount++;
            string stage = $"Step{_debugStepCount}";
            if (!stepResult.HasMoreBuilders)
                stage += "_Done";

            ApplyStepResult(_debugStepper.BuildResult(), stage);
        }

        [ContextMenu("Clear DEMO Map")]
        public void ClearDemoMap()
        {
            _debugStepper = null;
            _debugStepCount = 0;
            _lastResult = null;
            _lastRawResult = null;
            _lastDeadEndMask = null;
            _lastMasterSeed = 0;
            _lastAcceptedAttemptIndex = 0;
            _lastAttemptCount = 0;
            _lastQualified = false;
            _lastQualityScore = 0;
            _lastStage = null;
            _lastGeneratedSeed = 0;
            _lastGeneration = null;
            DestroyGeneratedRoot();
            DestroyPreviewAssets();
        }

        private void ApplyProcessedResult(
            DungeonMakerTunnelingResult processedResult,
            bool[] deadEndMask,
            int removedTileCount,
            string stage)
        {
            _lastResult = FinalizeGeneratedLayout(processedResult, true);
            _lastDeadEndMask = deadEndMask;
            _lastStage = stage;
            _lastGeneratedSeed = _lastResult.Seed;
            _lastGeneration = BuildGenerationSnapshot(
                _lastMasterSeed,
                _lastResult,
                _lastAcceptedAttemptIndex,
                _lastAttemptCount,
                _lastQualified,
                _lastQualityScore,
                removedTileCount,
                _lastStage);
            RebuildVisuals();
            LogGenerationSummary();
            AppendGenerationReport();
            AppendMapDump();
        }

        private void ApplyStepResult(DungeonMakerTunnelingResult result, string stage)
        {
            _lastRawResult = result;
            _lastResult = result;
            _lastDeadEndMask = null;
            _lastStage = stage;
            _lastGeneratedSeed = result.Seed;
            _lastGeneration = BuildGenerationSnapshot(
                _lastMasterSeed,
                result,
                _lastAcceptedAttemptIndex,
                _lastAttemptCount,
                _lastQualified,
                _lastQualityScore,
                0,
                _lastStage);
            RebuildVisuals();
            LogGenerationSummary();
            AppendGenerationReport();
            AppendMapDump();
        }

        [ContextMenu("Clear Generation Report File")]
        public void ClearGenerationReportFile()
        {
            string reportPath = GetGenerationReportFilePath();
            string dumpPath = GetMapDumpFilePath();
            string directoryPath = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);

            File.WriteAllText(reportPath, string.Empty, Encoding.UTF8);
            File.WriteAllText(dumpPath, string.Empty, Encoding.UTF8);
            Debug.Log($"[TunnelingMapDemo] Cleared generation outputs: {reportPath} | {dumpPath}");
        }

        [ContextMenu("Toggle Analysis Overlay")]
        public void ToggleAnalysisOverlay()
        {
            _drawAnalysisOverlay = !_drawAnalysisOverlay;
            ApplyOverlayVisibility();
        }

        [ContextMenu("Toggle Skeleton Overlay")]
        public void ToggleSkeletonOverlay()
        {
            _drawSkeletonOverlay = !_drawSkeletonOverlay;
            ApplyOverlayVisibility();
        }

        [ContextMenu("Log Debug Display Coordinate")]
        public void LogDebugDisplayCoordinate()
        {
            if (_lastRawResult == null)
            {
                Debug.LogWarning("[TunnelingMapDemo] No raw map is available.");
                return;
            }

            int displayX = _debugDisplayCoordinate.x;
            int displayY = _debugDisplayCoordinate.y;
            int displayWidth = _lastRawResult.DisplayWidth;
            int displayHeight = _lastRawResult.DisplayHeight;
            if (displayX < 0 || displayY < 0 || displayX >= displayWidth || displayY >= displayHeight)
            {
                Debug.LogWarning($"[TunnelingMapDemo] Debug display coordinate out of range: ({displayX}, {displayY}) / {displayWidth}x{displayHeight}");
                return;
            }

            int sourceX = displayY;
            int sourceY = displayX;
            int sourceIndex = GetIndex(sourceX, sourceY, _lastRawResult.SourceHeight);
            DungeonMakerSquareData rawTile = _lastRawResult.GetDisplayTile(displayX, displayY);
            DungeonMakerTileOrigin rawOrigin = _lastRawResult.GetDisplayOrigin(displayX, displayY);
            DungeonMakerSquareData processedTile = _lastResult != null
                ? _lastResult.GetDisplayTile(displayX, displayY)
                : rawTile;
            DungeonMakerTileOrigin processedOrigin = _lastResult != null
                ? _lastResult.GetDisplayOrigin(displayX, displayY)
                : rawOrigin;
            bool deadEnd = _lastDeadEndMask != null && _lastDeadEndMask[sourceIndex];

            Debug.Log(
                $"[TunnelingMapDemo] DebugDisplay ({displayX}, {displayY}) => Source ({sourceX}, {sourceY}) " +
                $"Raw={rawTile} RawOrigin={rawOrigin} Processed={processedTile} ProcessedOrigin={processedOrigin} " +
                $"DeadEnd={deadEnd} " +
                $"CurrentStage={_lastStage}");
        }

        [ContextMenu("Show Debug Coordinate Marker")]
        public void ShowDebugCoordinateMarker()
        {
            _showDebugCoordinateMarker = true;
            RefreshDebugCoordinateMarkerSafely();
        }

        [ContextMenu("Clear Debug Coordinate Marker")]
        public void ClearDebugCoordinateMarker()
        {
            _showDebugCoordinateMarker = false;
            DestroyDebugCoordinateMarker();
        }

        public bool TryGetDisplayCoordinateFromWorld(Vector3 worldPoint, out Vector2Int coordinate)
        {
            coordinate = default;
            if (_lastResult == null)
                return false;

            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            float halfWidth = _lastResult.DisplayWidth * 0.5f;
            float halfHeight = _lastResult.DisplayHeight * 0.5f;
            int displayX = Mathf.FloorToInt(localPoint.x / _cellSize + halfWidth);
            int displayY = Mathf.FloorToInt(localPoint.y / _cellSize + halfHeight);
            if (displayX < 0 || displayY < 0 || displayX >= _lastResult.DisplayWidth || displayY >= _lastResult.DisplayHeight)
                return false;

            coordinate = new Vector2Int(displayX, displayY);
            return true;
        }

        public void SetDebugDisplayCoordinate(Vector2Int coordinate)
        {
            _debugDisplayCoordinate = coordinate;
            _showDebugCoordinateMarker = true;
            RefreshDebugCoordinateMarkerSafely();
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
            ApplyOverlayVisibility();
#if UNITY_EDITOR
            ScheduleEditorDebugMarkerRefresh();
#else
            RefreshDebugCoordinateMarker();
#endif
        }

        private void ValidateParameters()
        {
            _cellSize = Mathf.Max(0.25f, _cellSize);
            _maxQualificationAttempts = Mathf.Max(1, _maxQualificationAttempts);
            _largeRoomRange = NormalizeRange(_largeRoomRange);
            _mediumRoomRange = NormalizeRange(_mediumRoomRange);
            _smallRoomRange = NormalizeRange(_smallRoomRange);
            _walkableTileRange = NormalizeRange(_walkableTileRange);
            _corridorLevel1SpawnChanceDenominator = Mathf.Max(1, _corridorLevel1SpawnChanceDenominator);
            _anteRoomMonsterCountRange = NormalizeEncounterRange(_anteRoomMonsterCountRange);
            _smallRoomMonsterCountRange = NormalizeEncounterRange(_smallRoomMonsterCountRange);
            _mediumRoomMonsterCountRange = NormalizeEncounterRange(_mediumRoomMonsterCountRange);
            _largeRoomMonsterCountRange = NormalizeEncounterRange(_largeRoomMonsterCountRange);
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

        private static EncounterCountRange NormalizeEncounterRange(EncounterCountRange range)
        {
            int min = Mathf.Max(0, range.Min);
            int max = Mathf.Max(min, range.Max);
            return new EncounterCountRange(min, max);
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
            CreateAnalysisOverlay();
            CreateSkeletonOverlay();
            if (EnableWallCubeGeneration && _generateWallCubes)
                CreateWallCubes();
            RefreshDebugCoordinateMarker();
        }

        private void ApplyOverlayVisibility()
        {
            if (_generatedRoot == null || _lastResult == null)
                return;

            ApplySingleOverlayVisibility(AnalysisOverlayName, _drawAnalysisOverlay, CreateAnalysisOverlay);
            ApplySingleOverlayVisibility(SkeletonOverlayName, _drawSkeletonOverlay, CreateSkeletonOverlay);
        }

        private void RefreshDebugCoordinateMarker()
        {
            if (!_showDebugCoordinateMarker || _generatedRoot == null || _lastResult == null)
            {
                DestroyDebugCoordinateMarker();
                return;
            }

            int displayX = _debugDisplayCoordinate.x;
            int displayY = _debugDisplayCoordinate.y;
            if (displayX < 0 || displayY < 0 || displayX >= _lastResult.DisplayWidth || displayY >= _lastResult.DisplayHeight)
            {
                DestroyDebugCoordinateMarker();
                return;
            }

            DestroyDebugCoordinateMarker();

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = DebugCoordinateMarkerName;
            marker.transform.SetParent(_generatedRoot, false);

            float halfWidth = _lastResult.DisplayWidth * 0.5f;
            float halfHeight = _lastResult.DisplayHeight * 0.5f;
            float centerX = (displayX + 0.5f - halfWidth) * _cellSize;
            float centerY = (displayY + 0.5f - halfHeight) * _cellSize;
            marker.transform.localPosition = new Vector3(centerX, centerY, 0.65f);
            marker.transform.localScale = new Vector3(_cellSize * 0.75f, _cellSize * 0.75f, _cellSize * 0.2f);

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(markerCollider);
                else
                    DestroyImmediate(markerCollider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = GetOrCreateDebugCoordinateMarkerMaterial();
        }

        private void RefreshDebugCoordinateMarkerSafely()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ScheduleEditorDebugMarkerRefresh();
                return;
            }
#endif
            RefreshDebugCoordinateMarker();
        }

        private void DestroyDebugCoordinateMarker()
        {
            if (_generatedRoot == null)
                return;

            Transform marker = _generatedRoot.Find(DebugCoordinateMarkerName);
            if (marker == null)
                return;

            if (Application.isPlaying)
                Destroy(marker.gameObject);
            else
                DestroyImmediate(marker.gameObject);
        }

#if UNITY_EDITOR
        private void ScheduleEditorDebugMarkerRefresh()
        {
            if (_pendingEditorDebugMarkerRefresh)
                return;

            _pendingEditorDebugMarkerRefresh = true;
            EditorApplication.delayCall += RefreshDebugCoordinateMarkerDelayed;
        }

        private void RefreshDebugCoordinateMarkerDelayed()
        {
            EditorApplication.delayCall -= RefreshDebugCoordinateMarkerDelayed;
            _pendingEditorDebugMarkerRefresh = false;

            if (this == null)
                return;

            RefreshDebugCoordinateMarker();
        }
#endif

        private Material GetOrCreateDebugCoordinateMarkerMaterial()
        {
            if (_debugCoordinateMarkerMaterial != null)
            {
                _debugCoordinateMarkerMaterial.color = _debugCoordinateMarkerColor;
                return _debugCoordinateMarkerMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _debugCoordinateMarkerMaterial = new Material(shader)
            {
                color = _debugCoordinateMarkerColor,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return _debugCoordinateMarkerMaterial;
        }

        private void ApplySingleOverlayVisibility(string overlayName, bool shouldShow, Action createOverlay)
        {
            Transform existingOverlay = _generatedRoot.Find(overlayName);
            if (shouldShow)
            {
                if (existingOverlay == null)
                    createOverlay();
            }
            else if (existingOverlay != null)
            {
                if (Application.isPlaying)
                    Destroy(existingOverlay.gameObject);
                else
                    DestroyImmediate(existingOverlay.gameObject);
            }
        }

        private DungeonMakerTunnelingResult GenerateQualifiedRawResult(
            int masterSeed,
            out int acceptedAttemptIndex,
            out int attemptCount,
            out bool qualified,
            out int qualityScore)
        {
            int maxAttempts = _requireQualifiedMap ? _maxQualificationAttempts : 1;
            DungeonMakerTunnelingResult bestResult = null;
            int bestScore = int.MinValue;
            int bestAttemptIndex = 0;

            for (int attemptIndex = 0; attemptIndex < maxAttempts; attemptIndex++)
            {
                int candidateSeed = DeriveCandidateSeed(masterSeed, attemptIndex);
                DungeonMakerTunnelingResult candidateResult = DungeonMakerTunnelingGenerator.Generate(candidateSeed, _config);
                DungeonMakerTunnelingStats stats = candidateResult.Stats;
                int candidateScore = ScoreMapQuality(stats);

                if (bestResult == null || candidateScore > bestScore)
                {
                    bestResult = candidateResult;
                    bestScore = candidateScore;
                    bestAttemptIndex = attemptIndex;
                }

                if (!_requireQualifiedMap || IsMapQualified(stats))
                {
                    acceptedAttemptIndex = attemptIndex;
                    attemptCount = attemptIndex + 1;
                    qualified = true;
                    qualityScore = candidateScore;
                    return candidateResult;
                }
            }

            acceptedAttemptIndex = bestAttemptIndex;
            attemptCount = maxAttempts;
            qualified = false;
            qualityScore = bestScore;
            return bestResult;
        }

        private TunnelingMapDemoGenerationSnapshot BuildGenerationSnapshot(
            int masterSeed,
            DungeonMakerTunnelingResult result,
            int acceptedAttemptIndex,
            int attemptCount,
            bool qualified,
            int qualityScore,
            int prunedDeadEndTiles,
            string stage)
        {
            GetSpecialRoomRegionIds(result, out int startRoomRegionId, out int nextLevelRoomRegionId);
            return new TunnelingMapDemoGenerationSnapshot
            {
                Stage = stage,
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
                StartRoomRegionId = startRoomRegionId,
                NextLevelRoomRegionId = nextLevelRoomRegionId,
                Stats = result.Stats,
            };
        }

        private DungeonMakerTunnelingResult AssignSpecialRooms(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            if (regions.Count == 0)
                return result;

            DungeonMakerSquareData[] map = CopySourceMap(result);
            List<int> smallRoomIndices = new();
            List<int> largeRoomIndices = new();
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                if (region.Kind != DungeonMakerRegionKind.Room || !RegionHasWalkableTile(region, map))
                    continue;

                switch (region.RoomSizeClass)
                {
                    case DungeonMakerRoomSizeClass.Small:
                        smallRoomIndices.Add(i);
                        break;
                    case DungeonMakerRoomSizeClass.Large:
                        largeRoomIndices.Add(i);
                        break;
                }
            }

            if (smallRoomIndices.Count == 0 && largeRoomIndices.Count == 0)
                return result;

            System.Random random = new(unchecked(result.Seed ^ 0x51A7F00D));
            int selectedSmallIndex = PickRegionIndex(random, smallRoomIndices);
            int selectedLargeIndex = PickRegionIndex(random, largeRoomIndices);

            DungeonMakerRegion[] assignedRegions = new DungeonMakerRegion[regions.Count];
            bool changed = false;
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                DungeonMakerSpecialRoomRole role = DungeonMakerSpecialRoomRole.None;
                if (i == selectedSmallIndex)
                    role = DungeonMakerSpecialRoomRole.Start;
                else if (i == selectedLargeIndex)
                    role = DungeonMakerSpecialRoomRole.NextLevel;

                if (region.SpecialRoomRole != role)
                {
                    changed = true;
                    assignedRegions[i] = new DungeonMakerRegion(region.Id, region.Kind, region.TileIndices, region.RoomSizeClass, role);
                }
                else
                {
                    assignedRegions[i] = region;
                }
            }

            // TODO: pick other functional rooms from small/medium/large rooms and anterooms via dedicated config.

            if (!changed)
                return result;

            return new DungeonMakerTunnelingResult(
                result.SourceWidth,
                result.SourceHeight,
                result.Seed,
                CopySourceMap(result),
                CopySourceOrigins(result),
                CopySourceSkeletonKinds(result),
                assignedRegions,
                CopySkeletonSegments(result),
                CopySkeletonLinks(result),
                CopySkeletonAttachments(result),
                CopyStats(result.Stats));
        }

        private DungeonMakerTunnelingResult FinalizeGeneratedLayout(DungeonMakerTunnelingResult result, bool spawnEncounters)
        {
            DungeonMakerTunnelingResult finalized = AssignSpecialRooms(result);
            if (spawnEncounters)
                finalized = AssignEncounterMarkers(finalized);

            return finalized;
        }

        private DungeonMakerTunnelingResult AssignEncounterMarkers(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            if (regions.Count == 0)
                return result;

            DungeonMakerSquareData[] map = CopySourceMap(result);
            DungeonMakerTileOrigin[] origins = CopySourceOrigins(result);
            bool changed = false;
            System.Random random = new(unchecked(result.Seed ^ 0x2D7E4A11));

            List<int> corridorCandidates = new();
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                List<int> candidates = CollectRegionCandidates(region, map);
                if (candidates.Count == 0)
                    continue;

                switch (region.Kind)
                {
                    case DungeonMakerRegionKind.Corridor:
                        corridorCandidates.AddRange(candidates);
                        break;

                    case DungeonMakerRegionKind.AnteRoom:
                        changed |= PlaceAnteRoomEncounters(candidates, map, origins, random);
                        break;

                    case DungeonMakerRegionKind.Room:
                        changed |= PlaceRoomEncounters(region, candidates, map, origins, random);
                        break;
                }
            }

            changed |= PlaceCorridorEncounters(corridorCandidates, map, origins, random, _corridorLevel1SpawnChanceDenominator);

            if (!changed)
                return result;

            return new DungeonMakerTunnelingResult(
                result.SourceWidth,
                result.SourceHeight,
                result.Seed,
                map,
                origins,
                CopySourceSkeletonKinds(result),
                CopyRegions(result),
                CopySkeletonSegments(result),
                CopySkeletonLinks(result),
                CopySkeletonAttachments(result),
                BuildStatsFromMap(result, map));
        }

        private static int PickRegionIndex(System.Random random, List<int> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            return candidates[random.Next(candidates.Count)];
        }

        private static void GetSpecialRoomRegionIds(
            DungeonMakerTunnelingResult result,
            out int startRoomRegionId,
            out int nextLevelRoomRegionId)
        {
            startRoomRegionId = -1;
            nextLevelRoomRegionId = -1;

            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                switch (region.SpecialRoomRole)
                {
                    case DungeonMakerSpecialRoomRole.Start:
                        startRoomRegionId = region.Id;
                        break;
                    case DungeonMakerSpecialRoomRole.NextLevel:
                        nextLevelRoomRegionId = region.Id;
                        break;
                }
            }
        }

        private static List<int> CollectRegionCandidates(DungeonMakerRegion region, DungeonMakerSquareData[] map)
        {
            List<int> candidates = new(region.TileIndices.Length);
            int[] tiles = region.TileIndices;
            for (int i = 0; i < tiles.Length; i++)
            {
                int tileIndex = tiles[i];
                DungeonMakerSquareData tile = map[tileIndex];
                if (!CanHostEncounterMarker(tile))
                    continue;

                candidates.Add(tileIndex);
            }

            return candidates;
        }

        private static bool PlaceCorridorEncounters(
            List<int> corridorCandidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            int spawnChanceDenominator)
        {
            if (corridorCandidates.Count == 0)
                return false;

            bool changed = false;
            for (int i = corridorCandidates.Count - 1; i >= 0; i--)
            {
                if (random.Next(spawnChanceDenominator) != 0)
                    continue;

                int tileIndex = corridorCandidates[i];
                corridorCandidates.RemoveAt(i);
                map[tileIndex] = DungeonMakerSquareData.MOB1;
                origins[tileIndex] = DungeonMakerTileOrigin.MonsterPlacement;
                changed = true;
            }

            return changed;
        }

        private bool PlaceAnteRoomEncounters(
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random)
        {
            int totalCount = RollEncounterCount(random, _anteRoomMonsterCountRange, candidates.Count);
            return PlaceRandomMonsterMix(candidates, map, origins, random, totalCount, allowLevel3: false);
        }

        private bool PlaceRoomEncounters(
            DungeonMakerRegion region,
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random)
        {
            bool changed = false;
            DungeonMakerSquareData chestTile = GetTreasureTileForRoom(region.RoomSizeClass);
            if (chestTile != DungeonMakerSquareData.CLOSED)
                changed |= TryPlaceMarker(candidates, map, origins, random, chestTile, DungeonMakerTileOrigin.TreasurePlacement);

            if (region.SpecialRoomRole == DungeonMakerSpecialRoomRole.Start)
                return changed;

            switch (region.RoomSizeClass)
            {
                case DungeonMakerRoomSizeClass.Small:
                    changed |= PlaceRandomMonsterMix(
                        candidates,
                        map,
                        origins,
                        random,
                        RollEncounterCount(random, _smallRoomMonsterCountRange, candidates.Count),
                        allowLevel3: false);
                    break;

                case DungeonMakerRoomSizeClass.Medium:
                    changed |= PlaceRandomMonsterMix(
                        candidates,
                        map,
                        origins,
                        random,
                        RollEncounterCount(random, _mediumRoomMonsterCountRange, candidates.Count),
                        allowLevel3: false);
                    break;

                case DungeonMakerRoomSizeClass.Large:
                    changed |= PlaceRandomMonsterMix(
                        candidates,
                        map,
                        origins,
                        random,
                        RollEncounterCount(random, _largeRoomMonsterCountRange, candidates.Count),
                        allowLevel3: true);
                    break;
            }

            return changed;
        }

        private static int RollEncounterCount(System.Random random, EncounterCountRange range, int candidateCount)
        {
            if (candidateCount <= 0 || range.Max <= 0)
                return 0;

            int rolled = random.Next(range.Min, range.Max + 1);
            return Math.Min(candidateCount, rolled);
        }

        private static bool PlaceRandomMonsterMix(
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            int totalCount,
            bool allowLevel3)
        {
            if (totalCount <= 0 || candidates.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < totalCount; i++)
            {
                DungeonMakerSquareData monsterTile = allowLevel3
                    ? (DungeonMakerSquareData)((int)DungeonMakerSquareData.MOB1 + random.Next(3))
                    : (DungeonMakerSquareData)((int)DungeonMakerSquareData.MOB1 + random.Next(2));
                changed |= TryPlaceMarker(candidates, map, origins, random, monsterTile, DungeonMakerTileOrigin.MonsterPlacement);
            }

            return changed;
        }

        private static bool TryPlaceMarker(
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            DungeonMakerSquareData markerTile,
            DungeonMakerTileOrigin origin)
        {
            if (candidates.Count == 0)
                return false;

            int candidateIndex = random.Next(candidates.Count);
            int tileIndex = candidates[candidateIndex];
            candidates.RemoveAt(candidateIndex);
            map[tileIndex] = markerTile;
            origins[tileIndex] = origin;
            return true;
        }

        private static DungeonMakerSquareData GetTreasureTileForRoom(DungeonMakerRoomSizeClass roomSizeClass)
        {
            return roomSizeClass switch
            {
                DungeonMakerRoomSizeClass.Small => DungeonMakerSquareData.TREAS1,
                DungeonMakerRoomSizeClass.Medium => DungeonMakerSquareData.TREAS2,
                DungeonMakerRoomSizeClass.Large => DungeonMakerSquareData.TREAS3,
                _ => DungeonMakerSquareData.CLOSED,
            };
        }

        private void LogGenerationSummary()
        {
            if (_lastGeneration?.Stats == null)
                return;

            DungeonMakerTunnelingStats stats = _lastGeneration.Stats;
            Debug.Log(
                $"[TunnelingMapDemo] Stage={_lastGeneration.Stage} MasterSeed={_lastGeneration.MasterSeed} SelectedSeed={_lastGeneration.Seed} " +
                $"Attempt={_lastGeneration.AcceptedAttemptIndex + 1}/{_lastGeneration.AttemptCount} Qualified={_lastGeneration.Qualified} Score={_lastGeneration.QualityScore} " +
                $"PrunedDeadEnds={_lastGeneration.PrunedDeadEndTiles} " +
                $"SpecialRooms(Start={_lastGeneration.StartRoomRegionId}, NextLevel={_lastGeneration.NextLevelRoomRegionId}) " +
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

        private void AppendMapDump()
        {
            if (_lastGeneration?.Stats == null || _lastResult == null)
                return;

            string filePath = GetMapDumpFilePath();
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);

            File.AppendAllText(filePath, BuildMapDumpText(), Encoding.UTF8);
            Debug.Log($"[TunnelingMapDemo] Appended map dump: {filePath}");
        }

        private string BuildGenerationReportText()
        {
            DungeonMakerTunnelingStats stats = _lastGeneration.Stats;
            StringBuilder builder = new();
            builder.AppendLine("============================================================");
            builder.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Stage: {_lastGeneration.Stage}");
            builder.AppendLine($"MasterSeed: {_lastGeneration.MasterSeed}");
            builder.AppendLine($"SelectedSeed: {_lastGeneration.Seed}");
            builder.AppendLine($"AcceptedAttemptIndex: {_lastGeneration.AcceptedAttemptIndex}");
            builder.AppendLine($"AttemptCount: {_lastGeneration.AttemptCount}");
            builder.AppendLine($"Qualified: {_lastGeneration.Qualified}");
            builder.AppendLine($"QualityScore: {_lastGeneration.QualityScore}");
            builder.AppendLine($"PrunedDeadEndTiles: {_lastGeneration.PrunedDeadEndTiles}");
            builder.AppendLine($"StartRoomRegionId: {_lastGeneration.StartRoomRegionId}");
            builder.AppendLine($"NextLevelRoomRegionId: {_lastGeneration.NextLevelRoomRegionId}");
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
            builder.AppendLine("[Encounter Rules]");
            builder.AppendLine($"CorridorMob1Chance: 1 / {_corridorLevel1SpawnChanceDenominator}");
            builder.AppendLine($"AnteRoomMonsterCount: {_anteRoomMonsterCountRange.Min} - {_anteRoomMonsterCountRange.Max}");
            builder.AppendLine($"SmallRoomMonsterCount: {_smallRoomMonsterCountRange.Min} - {_smallRoomMonsterCountRange.Max}");
            builder.AppendLine($"MediumRoomMonsterCount: {_mediumRoomMonsterCountRange.Min} - {_mediumRoomMonsterCountRange.Max}");
            builder.AppendLine($"LargeRoomMonsterCount: {_largeRoomMonsterCountRange.Min} - {_largeRoomMonsterCountRange.Max}");
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
            builder.AppendLine("[Special Rooms]");
            builder.AppendLine($"StartRoomRegionId: {_lastGeneration.StartRoomRegionId}");
            builder.AppendLine($"NextLevelRoomRegionId: {_lastGeneration.NextLevelRoomRegionId}");
            builder.AppendLine("TODO: other functional rooms will be selected later from configurable room/anteroom pools.");
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

        private string BuildMapDumpText()
        {
            StringBuilder builder = new();
            builder.AppendLine("============================================================");
            builder.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Stage: {_lastGeneration.Stage}");
            builder.AppendLine($"MasterSeed: {_lastGeneration.MasterSeed}");
            builder.AppendLine($"SelectedSeed: {_lastGeneration.Seed}");
            builder.AppendLine($"DisplaySize: {_lastResult.DisplayWidth} x {_lastResult.DisplayHeight}");
            builder.AppendLine("Legend: O=OPEN GO=G_OPEN NJO=NJ_OPEN NJGO=NJ_G_OPEN T=IT_OPEN R=IR_OPEN A=IA_OPEN H=H_DOOR V=V_DOOR C=COLUMN X=CLOSED GX=G_CLOSED NJX=NJ_CLOSED NJGX=NJ_G_CLOSED M1/M2/M3=$mob TR1/TR2/TR3=$treasure");
            builder.AppendLine("[DisplayEnumMap]");

            for (int y = 0; y < _lastResult.DisplayHeight; y++)
            {
                StringBuilder row = new();
                for (int x = 0; x < _lastResult.DisplayWidth; x++)
                {
                    if (x > 0)
                        row.Append(' ');

                    row.Append(GetTileDumpToken(_lastResult.GetDisplayTile(x, y)));
                }
                builder.AppendLine(row.ToString());
            }

            builder.AppendLine();
            builder.AppendLine("OriginLegend: ..=None BC=BoundaryClosed TC=TunnelCarve OJ=OffsetJoin PJ=ParallelJoinLead PX=ParallelJoinExtend PR=ProbeRestore AC=AnteRoomCarve RC=RoomCarve DP=DoorPlacement CP=ColumnPlacement MP=MonsterPlacement TP=TreasurePlacement");
            builder.AppendLine("[DisplayOriginMap]");
            for (int y = 0; y < _lastResult.DisplayHeight; y++)
            {
                StringBuilder row = new();
                for (int x = 0; x < _lastResult.DisplayWidth; x++)
                {
                    if (x > 0)
                        row.Append(' ');

                    row.Append(GetOriginDumpToken(_lastResult.GetDisplayOrigin(x, y)));
                }
                builder.AppendLine(row.ToString());
            }

            if (_lastDeadEndMask != null)
            {
                builder.AppendLine();
                builder.AppendLine("[DeletionMap]");
                builder.AppendLine("Legend: D=DeadEnd .=None");
                for (int y = 0; y < _lastResult.DisplayHeight; y++)
                {
                    StringBuilder row = new(_lastResult.DisplayWidth);
                    for (int x = 0; x < _lastResult.DisplayWidth; x++)
                    {
                        int sourceIndex = GetIndex(y, x, _lastResult.SourceHeight);
                        bool isDeadEnd = _lastDeadEndMask != null && _lastDeadEndMask[sourceIndex];
                        row.Append(isDeadEnd ? 'D' : '.');
                    }
                    builder.AppendLine(row.ToString());
                }
            }

            builder.AppendLine();
            return builder.ToString();
        }

        private DungeonMakerTunnelingResult PostProcessCandidateResult(
            DungeonMakerTunnelingResult result,
            out int prunedDeadEndTiles,
            out bool[] detectedLoopMask,
            out bool[] deletedLoopMask,
            out bool[] deadEndMask)
        {
            prunedDeadEndTiles = 0;
            DungeonMakerSquareData[] map = CopySourceMap(result);
            DungeonMakerTileOrigin[] origins = CopySourceOrigins(result);
            detectedLoopMask = null;
            deletedLoopMask = null;
            deadEndMask = null;
            if (!_pruneDeadEnds)
                return result;

            DungeonMakerSkeletonKind[] skeletonKinds = CopySourceSkeletonKinds(result);
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            bool[] removedDeadEndMask = new bool[map.Length];

            PruneDeadEndCorridorsByLogicalSkeleton(result, map, skeletonKinds, result.SourceWidth, result.SourceHeight, removedDeadEndMask, ref prunedDeadEndTiles);

            if (prunedDeadEndTiles == 0)
                return result;

            deadEndMask = removedDeadEndMask;
            DungeonMakerTunnelingStats stats = BuildStatsFromMap(result, map);
            DungeonMakerRegion[] copiedRegions = new DungeonMakerRegion[regions.Count];
            for (int i = 0; i < regions.Count; i++)
                copiedRegions[i] = regions[i];
            return new DungeonMakerTunnelingResult(
                result.SourceWidth,
                result.SourceHeight,
                result.Seed,
                map,
                origins,
                skeletonKinds,
                copiedRegions,
                CopySkeletonSegments(result),
                CopySkeletonLinks(result),
                CopySkeletonAttachments(result),
                stats);
        }

        private DungeonMakerTunnelingResult ProcessDeadEndsOnly(
            DungeonMakerTunnelingResult result,
            out int removedDeadEndTiles,
            out bool[] deadEndMask)
        {
            removedDeadEndTiles = 0;
            deadEndMask = new bool[result.SourceWidth * result.SourceHeight];

            DungeonMakerSquareData[] map = CopySourceMap(result);
            DungeonMakerTileOrigin[] origins = CopySourceOrigins(result);
            DungeonMakerSkeletonKind[] skeletonKinds = CopySourceSkeletonKinds(result);
            PruneDeadEndCorridorsByLogicalSkeleton(result, map, skeletonKinds, result.SourceWidth, result.SourceHeight, deadEndMask, ref removedDeadEndTiles);
            if (removedDeadEndTiles == 0)
                return result;

            DungeonMakerTunnelingStats stats = BuildStatsFromMap(result, map);
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            DungeonMakerRegion[] copiedRegions = new DungeonMakerRegion[regions.Count];
            for (int i = 0; i < regions.Count; i++)
                copiedRegions[i] = regions[i];
            return new DungeonMakerTunnelingResult(
                result.SourceWidth,
                result.SourceHeight,
                result.Seed,
                map,
                origins,
                skeletonKinds,
                copiedRegions,
                CopySkeletonSegments(result),
                CopySkeletonLinks(result),
                CopySkeletonAttachments(result),
                stats);
        }

        private static void PruneDeadEndCorridorsByLogicalSkeleton(
            DungeonMakerTunnelingResult result,
            DungeonMakerSquareData[] map,
            DungeonMakerSkeletonKind[] skeletonKinds,
            int width,
            int height,
            bool[] removedDeadEndMask,
            ref int prunedDeadEndTiles)
        {
            IReadOnlyList<DungeonMakerSkeletonSegment> segments = result.SkeletonSegments;
            if (segments.Count == 0)
                return;

            PruneDeadEndSkeletonSegments(
                result,
                width,
                height,
                out bool[] removedCorridorMask);

            for (int i = 0; i < removedCorridorMask.Length; i++)
            {
                if (!removedCorridorMask[i])
                    continue;

                removedDeadEndMask[i] = true;
                map[i] = GetDeadEndFilledTile(map[i]);
                if (skeletonKinds[i] != DungeonMakerSkeletonKind.None)
                    skeletonKinds[i] = DungeonMakerSkeletonKind.None;
                prunedDeadEndTiles++;
            }
        }

        private static void PruneDeadEndSkeletonSegments(
            DungeonMakerTunnelingResult result,
            int width,
            int height,
            out bool[] removedCorridorMask)
        {
            IReadOnlyList<DungeonMakerSkeletonSegment> segments = result.SkeletonSegments;
            IReadOnlyList<DungeonMakerSkeletonLink> links = result.SkeletonLinks;
            IReadOnlyList<DungeonMakerSkeletonAttachment> attachments = result.SkeletonAttachments;
            int segmentCount = segments.Count;
            bool[] aliveSegments = new bool[segmentCount];
            Array.Fill(aliveSegments, true);

            Dictionary<int, int> segmentIndexById = new(segmentCount);
            for (int i = 0; i < segmentCount; i++)
                segmentIndexById[segments[i].Id] = i;

            HashSet<int>[] allNeighbors = new HashSet<int>[segmentCount];
            int[] degree = new int[segmentCount];
            bool[] valuableSegments = new bool[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                allNeighbors[i] = new HashSet<int>();
            }

            for (int i = 0; i < attachments.Count; i++)
            {
                DungeonMakerSkeletonAttachment attachment = attachments[i];
                if (segmentIndexById.TryGetValue(attachment.SegmentId, out int segmentIndex))
                    valuableSegments[segmentIndex] = true;
            }

            for (int i = 0; i < links.Count; i++)
            {
                DungeonMakerSkeletonLink link = links[i];
                int fromSegmentIndex = ResolveSegmentIndex(link.FromSegmentId, link.From, segments, segmentIndexById);
                int toSegmentIndex = ResolveSegmentIndex(link.ToSegmentId, link.To, segments, segmentIndexById);
                if (fromSegmentIndex < 0 || toSegmentIndex < 0 || fromSegmentIndex == toSegmentIndex)
                    continue;

                if (allNeighbors[fromSegmentIndex].Add(toSegmentIndex))
                    degree[fromSegmentIndex]++;
                if (allNeighbors[toSegmentIndex].Add(fromSegmentIndex))
                    degree[toSegmentIndex]++;
            }

            Queue<int> queue = new();
            for (int i = 0; i < segmentCount; i++)
            {
                if (aliveSegments[i] && !valuableSegments[i] && degree[i] <= 1)
                    queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int segmentIndex = queue.Dequeue();
                if (!aliveSegments[segmentIndex])
                    continue;

                if (valuableSegments[segmentIndex] || degree[segmentIndex] > 1)
                    continue;

                aliveSegments[segmentIndex] = false;
                foreach (int neighborIndex in allNeighbors[segmentIndex])
                {
                    if (!aliveSegments[neighborIndex])
                        continue;

                    degree[neighborIndex]--;
                    if (!valuableSegments[neighborIndex] && degree[neighborIndex] <= 1)
                        queue.Enqueue(neighborIndex);
                }
            }

            removedCorridorMask = new bool[width * height];

            for (int i = 0; i < segmentCount; i++)
            {
                if (!aliveSegments[i])
                    MarkOwnedTiles(removedCorridorMask, segments[i].OwnedTileIndices);
            }
        }

        private static int ResolveSegmentIndex(
            int segmentId,
            Vector2Int point,
            IReadOnlyList<DungeonMakerSkeletonSegment> segments,
            Dictionary<int, int> segmentIndexById)
        {
            if (segmentId >= 0 && segmentIndexById.TryGetValue(segmentId, out int exactIndex))
                return exactIndex;

            for (int i = 0; i < segments.Count; i++)
            {
                if (PointLiesOnSegment(point, segments[i]))
                    return i;
            }

            return -1;
        }

        private static bool PointLiesOnSegment(Vector2Int point, DungeonMakerSkeletonSegment segment)
        {
            if (segment.Start.x == segment.End.x)
            {
                if (point.x != segment.Start.x)
                    return false;

                int minY = Math.Min(segment.Start.y, segment.End.y);
                int maxY = Math.Max(segment.Start.y, segment.End.y);
                return point.y >= minY && point.y <= maxY;
            }

            if (segment.Start.y == segment.End.y)
            {
                if (point.y != segment.Start.y)
                    return false;

                int minX = Math.Min(segment.Start.x, segment.End.x);
                int maxX = Math.Max(segment.Start.x, segment.End.x);
                return point.x >= minX && point.x <= maxX;
            }

            return point == segment.Start || point == segment.End;
        }

        private static void MarkSegmentLine(bool[] mask, DungeonMakerSkeletonSegment segment, int height)
        {
            MarkLine(mask, segment.Start, segment.End, height);
        }

        private static void MarkOwnedTiles(bool[] mask, int[] ownedTileIndices)
        {
            for (int i = 0; i < ownedTileIndices.Length; i++)
                mask[ownedTileIndices[i]] = true;
        }

        private static void MarkLine(bool[] mask, Vector2Int start, Vector2Int end, int height)
        {
            Vector2Int current = start;
            mask[GetIndex(current.x, current.y, height)] = true;

            while (current.x != end.x)
            {
                current.x += Math.Sign(end.x - current.x);
                mask[GetIndex(current.x, current.y, height)] = true;
            }

            while (current.y != end.y)
            {
                current.y += Math.Sign(end.y - current.y);
                mask[GetIndex(current.x, current.y, height)] = true;
            }
        }

        private static void PruneDeadEndCorridorsByLeafStripping(
            DungeonMakerSquareData[] map,
            int width,
            int height,
            bool[] removedDeadEndMask,
            ref int prunedDeadEndTiles)
        {
            Queue<int> queue = new();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int index = GetIndex(x, y, height);
                    if (!IsPrunableDeadEndTile(map[index]))
                        continue;

                    if (CountActiveWalkableNeighbors(map, width, height, x, y) <= 1)
                        queue.Enqueue(index);
                }
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current / height;
                int y = current % height;
                if (!IsPrunableDeadEndTile(map[current]))
                    continue;

                if (CountActiveWalkableNeighbors(map, width, height, x, y) > 1)
                    continue;

                removedDeadEndMask[current] = true;
                map[current] = GetDeadEndFilledTile(map[current]);
                prunedDeadEndTiles++;

                TryEnqueueDeadEndNeighbor(queue, map, width, height, x + 1, y);
                TryEnqueueDeadEndNeighbor(queue, map, width, height, x - 1, y);
                TryEnqueueDeadEndNeighbor(queue, map, width, height, x, y + 1);
                TryEnqueueDeadEndNeighbor(queue, map, width, height, x, y - 1);
            }
        }

        private static bool[] BuildDetectedLoopMask(DungeonMakerSquareData[] map, int width, int height)
        {
            bool[] detectedLoopMask = new bool[map.Length];
            bool[] visited = new bool[map.Length];
            Queue<int> queue = new();

            for (int index = 0; index < map.Length; index++)
            {
                if (visited[index] || !IsBlockedTile(map[index]))
                    continue;

                visited[index] = true;
                queue.Enqueue(index);
                bool touchesBoundary = false;
                bool hasProtectedAdjacency = false;
                HashSet<int> surroundingCorridorTiles = new();

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    int x = current / height;
                    int y = current % height;
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                        touchesBoundary = true;

                    CollectBlockedRegionNeighbor(map, visited, queue, surroundingCorridorTiles, ref hasProtectedAdjacency, width, height, x + 1, y);
                    CollectBlockedRegionNeighbor(map, visited, queue, surroundingCorridorTiles, ref hasProtectedAdjacency, width, height, x - 1, y);
                    CollectBlockedRegionNeighbor(map, visited, queue, surroundingCorridorTiles, ref hasProtectedAdjacency, width, height, x, y + 1);
                    CollectBlockedRegionNeighbor(map, visited, queue, surroundingCorridorTiles, ref hasProtectedAdjacency, width, height, x, y - 1);
                }

                if (touchesBoundary || hasProtectedAdjacency || surroundingCorridorTiles.Count == 0)
                    continue;

                foreach (int corridorTile in surroundingCorridorTiles)
                    detectedLoopMask[corridorTile] = true;
            }

            return detectedLoopMask;
        }

        private static void RemoveRemovableCorridorLoops(
            DungeonMakerSquareData[] map,
            bool[] detectedLoopMask,
            bool[] removedLoopMask,
            ref int prunedDeadEndTiles)
        {
            if (!HasAnyMarkedTile(detectedLoopMask))
                return;

            for (int i = 0; i < map.Length; i++)
            {
                if (!detectedLoopMask[i] || !IsPrunableDeadEndTile(map[i]) || removedLoopMask[i])
                    continue;

                removedLoopMask[i] = true;
                map[i] = GetDeadEndFilledTile(map[i]);
                prunedDeadEndTiles++;
            }
        }

        private static bool[] AnalyzeCycleCore(
            DungeonMakerTunnelingResult result,
            out bool hasCycleCore,
            out int cycleCoreRegionCount,
            out int cycleCoreCorridorCount,
            out int cycleCoreRoomCount,
            out int cycleCoreAnteRoomCount)
        {
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            if (regions.Count == 0)
            {
                hasCycleCore = false;
                cycleCoreRegionCount = 0;
                cycleCoreCorridorCount = 0;
                cycleCoreRoomCount = 0;
                cycleCoreAnteRoomCount = 0;
                return new bool[result.SourceWidth * result.SourceHeight];
            }

            DungeonMakerSquareData[] map = CopySourceMap(result);
            int[] tileOwners = BuildRegionOwnershipMap(map.Length, regions, map);
            List<HashSet<int>> adjacency = BuildRegionAdjacency(regions, tileOwners, map, result.SourceWidth, result.SourceHeight);
            bool[] activeRegions = new bool[regions.Count];
            int[] activeDegrees = new int[regions.Count];
            Queue<int> queue = new();

            for (int i = 0; i < regions.Count; i++)
            {
                activeRegions[i] = RegionHasWalkableTile(regions[i], map);
            }

            for (int i = 0; i < regions.Count; i++)
            {
                if (!activeRegions[i])
                    continue;

                activeDegrees[i] = CountActiveRegionNeighbors(i, adjacency, activeRegions);
            }

            for (int i = 0; i < regions.Count; i++)
            {
                if (activeRegions[i] && activeDegrees[i] <= 1)
                    queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int regionIndex = queue.Dequeue();
                if (!activeRegions[regionIndex] || activeDegrees[regionIndex] > 1)
                    continue;

                activeRegions[regionIndex] = false;
                foreach (int neighbor in adjacency[regionIndex])
                {
                    if (!activeRegions[neighbor])
                        continue;

                    activeDegrees[neighbor]--;
                    if (activeDegrees[neighbor] == 1)
                        queue.Enqueue(neighbor);
                }
            }

            cycleCoreRegionCount = 0;
            cycleCoreCorridorCount = 0;
            cycleCoreRoomCount = 0;
            cycleCoreAnteRoomCount = 0;
            bool[] cycleCoreMask = new bool[map.Length];
            for (int i = 0; i < regions.Count; i++)
            {
                if (!activeRegions[i])
                    continue;

                cycleCoreRegionCount++;
                int[] tiles = regions[i].TileIndices;
                for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
                {
                    int index = tiles[tileIndex];
                    if (IsWalkableTile(map[index]))
                        cycleCoreMask[index] = true;
                }

                switch (regions[i].Kind)
                {
                    case DungeonMakerRegionKind.Corridor:
                        cycleCoreCorridorCount++;
                        break;
                    case DungeonMakerRegionKind.Room:
                        cycleCoreRoomCount++;
                        break;
                    case DungeonMakerRegionKind.AnteRoom:
                        cycleCoreAnteRoomCount++;
                        break;
                }
            }

            hasCycleCore = cycleCoreRegionCount > 0;
            return cycleCoreMask;
        }

        private static int[] BuildRegionOwnershipMap(int cellCount, IReadOnlyList<DungeonMakerRegion> regions, DungeonMakerSquareData[] map)
        {
            int[] tileOwners = new int[cellCount];
            Array.Fill(tileOwners, -1);

            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                int[] tiles = regions[regionIndex].TileIndices;
                for (int i = 0; i < tiles.Length; i++)
                {
                    int tileIndex = tiles[i];
                    if (tileIndex >= 0
                        && tileIndex < cellCount
                        && tileOwners[tileIndex] < 0
                        && IsWalkableTile(map[tileIndex]))
                    {
                        tileOwners[tileIndex] = regionIndex;
                    }
                }
            }

            return tileOwners;
        }

        private static List<List<int>> BuildBiconnectedComponents(List<HashSet<int>> adjacency, bool[] activeRegions)
        {
            int regionCount = adjacency.Count;
            int[] discovery = new int[regionCount];
            int[] low = new int[regionCount];
            Array.Fill(discovery, -1);
            Stack<(int from, int to)> edgeStack = new();
            List<List<int>> components = new();
            int time = 0;

            void PopComponentUntil((int from, int to) stopEdge)
            {
                HashSet<int> vertices = new();
                while (edgeStack.Count > 0)
                {
                    (int from, int to) edge = edgeStack.Pop();
                    vertices.Add(edge.from);
                    vertices.Add(edge.to);
                    if (edge.from == stopEdge.from && edge.to == stopEdge.to)
                        break;
                }

                if (vertices.Count > 0)
                    components.Add(new List<int>(vertices));
            }

            void PopAllRemainingEdges()
            {
                HashSet<int> vertices = new();
                while (edgeStack.Count > 0)
                {
                    (int from, int to) edge = edgeStack.Pop();
                    vertices.Add(edge.from);
                    vertices.Add(edge.to);
                }

                if (vertices.Count > 0)
                    components.Add(new List<int>(vertices));
            }

            void Dfs(int current, int parent)
            {
                discovery[current] = low[current] = time++;

                foreach (int neighbor in adjacency[current])
                {
                    if (!activeRegions[neighbor])
                        continue;

                    if (discovery[neighbor] < 0)
                    {
                        edgeStack.Push((current, neighbor));
                        Dfs(neighbor, current);
                        low[current] = Math.Min(low[current], low[neighbor]);
                        if (low[neighbor] >= discovery[current])
                            PopComponentUntil((current, neighbor));
                    }
                    else if (neighbor != parent && discovery[neighbor] < discovery[current])
                    {
                        edgeStack.Push((current, neighbor));
                        low[current] = Math.Min(low[current], discovery[neighbor]);
                    }
                }
            }

            for (int regionIndex = 0; regionIndex < regionCount; regionIndex++)
            {
                if (!activeRegions[regionIndex] || discovery[regionIndex] >= 0)
                    continue;

                Dfs(regionIndex, -1);
                if (edgeStack.Count > 0)
                    PopAllRemainingEdges();
            }

            return components;
        }

        private static bool TryGetRemovableCorridorLoopRegions(
            List<int> component,
            IReadOnlyList<DungeonMakerRegion> regions,
            List<HashSet<int>> adjacency,
            bool[] activeRegions,
            DungeonMakerSquareData[] map,
            int width,
            int height,
            out List<int> removableRegionIndices)
        {
            removableRegionIndices = null;
            if (component.Count < 3)
                return false;

            HashSet<int> componentSet = new(component);
            for (int i = 0; i < component.Count; i++)
            {
                int regionIndex = component[i];
                if (!activeRegions[regionIndex] || regions[regionIndex].Kind != DungeonMakerRegionKind.Corridor)
                    return false;
            }

            List<int> attachmentRegions = new();
            for (int i = 0; i < component.Count; i++)
            {
                int regionIndex = component[i];
                foreach (int neighbor in adjacency[regionIndex])
                {
                    if (activeRegions[neighbor] && !componentSet.Contains(neighbor))
                    {
                        attachmentRegions.Add(regionIndex);
                        break;
                    }
                }
            }

            if (attachmentRegions.Count > 1)
                return false;

            if (!EnclosesOnlyBlockedInterior(component, regions, map, width, height))
                return false;

            removableRegionIndices = new List<int>(component.Count);
            for (int i = 0; i < component.Count; i++)
            {
                removableRegionIndices.Add(component[i]);
            }

            return removableRegionIndices.Count > 0;
        }

        private static HashSet<int> ExpandRemovableCorridorRegions(
            List<int> loopRegionIndices,
            IReadOnlyList<DungeonMakerRegion> regions,
            List<HashSet<int>> adjacency,
            bool[] activeRegions,
            bool[] removedRegions)
        {
            HashSet<int> removableRegions = new(loopRegionIndices);
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
                {
                    if (!activeRegions[regionIndex]
                        || removedRegions[regionIndex]
                        || removableRegions.Contains(regionIndex)
                        || regions[regionIndex].Kind != DungeonMakerRegionKind.Corridor
                        || !HasNeighborInSet(regionIndex, adjacency, removableRegions))
                    {
                        continue;
                    }

                    if (HasSpecialNeighborOutsideSet(regionIndex, regions, adjacency, activeRegions, removableRegions))
                        continue;

                    if (CountCorridorNeighborsOutsideSet(regionIndex, regions, adjacency, activeRegions, removableRegions) > 1)
                        continue;

                    removableRegions.Add(regionIndex);
                    changed = true;
                }
            }

            return removableRegions;
        }

        private static bool EnclosesOnlyBlockedInterior(
            List<int> component,
            IReadOnlyList<DungeonMakerRegion> regions,
            DungeonMakerSquareData[] map,
            int width,
            int height)
        {
            HashSet<int> componentTiles = new();
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int componentIndex = 0; componentIndex < component.Count; componentIndex++)
            {
                int[] tiles = regions[component[componentIndex]].TileIndices;
                for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
                {
                    int index = tiles[tileIndex];
                    if (!IsWalkableTile(map[index]))
                        continue;

                    componentTiles.Add(index);
                    int x = index / height;
                    int y = index % height;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (componentTiles.Count == 0)
                return false;

            minX = Math.Max(0, minX - 1);
            minY = Math.Max(0, minY - 1);
            maxX = Math.Min(width - 1, maxX + 1);
            maxY = Math.Min(height - 1, maxY + 1);

            bool[] visited = new bool[map.Length];
            Queue<int> queue = new();

            void EnqueueBoundaryCell(int x, int y)
            {
                int index = GetIndex(x, y, height);
                if (visited[index] || componentTiles.Contains(index))
                    return;

                visited[index] = true;
                queue.Enqueue(index);
            }

            for (int x = minX; x <= maxX; x++)
            {
                EnqueueBoundaryCell(x, minY);
                EnqueueBoundaryCell(x, maxY);
            }

            for (int y = minY; y <= maxY; y++)
            {
                EnqueueBoundaryCell(minX, y);
                EnqueueBoundaryCell(maxX, y);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current / height;
                int y = current % height;
                TryFloodInterior(queue, visited, componentTiles, width, height, x + 1, y);
                TryFloodInterior(queue, visited, componentTiles, width, height, x - 1, y);
                TryFloodInterior(queue, visited, componentTiles, width, height, x, y + 1);
                TryFloodInterior(queue, visited, componentTiles, width, height, x, y - 1);
            }

            bool hasEnclosedInterior = false;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    int index = GetIndex(x, y, height);
                    if (visited[index] || componentTiles.Contains(index))
                        continue;

                    hasEnclosedInterior = true;
                    if (IsWalkableTile(map[index]))
                        return false;
                }
            }

            return hasEnclosedInterior;
        }

        private static bool HasNeighborInSet(int regionIndex, List<HashSet<int>> adjacency, HashSet<int> regionSet)
        {
            foreach (int neighbor in adjacency[regionIndex])
            {
                if (regionSet.Contains(neighbor))
                    return true;
            }

            return false;
        }

        private static bool HasSpecialNeighborOutsideSet(
            int regionIndex,
            IReadOnlyList<DungeonMakerRegion> regions,
            List<HashSet<int>> adjacency,
            bool[] activeRegions,
            HashSet<int> regionSet)
        {
            foreach (int neighbor in adjacency[regionIndex])
            {
                if (!activeRegions[neighbor] || regionSet.Contains(neighbor))
                    continue;

                if (regions[neighbor].Kind != DungeonMakerRegionKind.Corridor)
                    return true;
            }

            return false;
        }

        private static int CountCorridorNeighborsOutsideSet(
            int regionIndex,
            IReadOnlyList<DungeonMakerRegion> regions,
            List<HashSet<int>> adjacency,
            bool[] activeRegions,
            HashSet<int> regionSet)
        {
            int count = 0;
            foreach (int neighbor in adjacency[regionIndex])
            {
                if (!activeRegions[neighbor] || regionSet.Contains(neighbor))
                    continue;

                if (regions[neighbor].Kind == DungeonMakerRegionKind.Corridor)
                    count++;
            }

            return count;
        }

        private static void TryFloodInterior(
            Queue<int> queue,
            bool[] visited,
            HashSet<int> blockedByComponent,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = GetIndex(x, y, height);
            if (visited[index] || blockedByComponent.Contains(index))
                return;

            visited[index] = true;
            queue.Enqueue(index);
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
                    if (!IsWalkableTile(map[tileIndex]))
                        continue;

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

        private static bool RegionHasWalkableTile(DungeonMakerRegion region, DungeonMakerSquareData[] map)
        {
            int[] tiles = region.TileIndices;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (IsWalkableTile(map[tiles[i]]))
                    return true;
            }

            return false;
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

        private static DungeonMakerTunnelingStats CopyStats(DungeonMakerTunnelingStats source)
        {
            return new DungeonMakerTunnelingStats
            {
                SourceWidth = source.SourceWidth,
                SourceHeight = source.SourceHeight,
                DisplayWidth = source.DisplayWidth,
                DisplayHeight = source.DisplayHeight,
                TotalCells = source.TotalCells,
                TotalRooms = source.TotalRooms,
                SmallRooms = source.SmallRooms,
                MediumRooms = source.MediumRooms,
                LargeRooms = source.LargeRooms,
                AnteRooms = source.AnteRooms,
                OpenTiles = source.OpenTiles,
                ClosedTiles = source.ClosedTiles,
                BoundaryOpenTiles = source.BoundaryOpenTiles,
                BoundaryClosedTiles = source.BoundaryClosedTiles,
                NonJoinOpenTiles = source.NonJoinOpenTiles,
                NonJoinClosedTiles = source.NonJoinClosedTiles,
                NonJoinBoundaryOpenTiles = source.NonJoinBoundaryOpenTiles,
                NonJoinBoundaryClosedTiles = source.NonJoinBoundaryClosedTiles,
                RoomTiles = source.RoomTiles,
                TunnelTiles = source.TunnelTiles,
                AnteRoomTiles = source.AnteRoomTiles,
                HorizontalDoorTiles = source.HorizontalDoorTiles,
                VerticalDoorTiles = source.VerticalDoorTiles,
                MobTiles = source.MobTiles,
                TreasureTiles = source.TreasureTiles,
                ColumnTiles = source.ColumnTiles,
                WalkableTiles = source.WalkableTiles,
                BlockedTiles = source.BlockedTiles,
            };
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

        private static DungeonMakerRegion[] CopyRegions(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            DungeonMakerRegion[] copiedRegions = new DungeonMakerRegion[regions.Count];
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                copiedRegions[i] = new DungeonMakerRegion(region.Id, region.Kind, region.TileIndices, region.RoomSizeClass, region.SpecialRoomRole);
            }

            return copiedRegions;
        }

        private static DungeonMakerTileOrigin[] CopySourceOrigins(DungeonMakerTunnelingResult result)
        {
            int width = result.SourceWidth;
            int height = result.SourceHeight;
            DungeonMakerTileOrigin[] origins = new DungeonMakerTileOrigin[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    origins[x * height + y] = result.GetSourceOrigin(x, y);
            }

            return origins;
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

        private static DungeonMakerSkeletonSegment[] CopySkeletonSegments(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerSkeletonSegment> source = result.SkeletonSegments;
            DungeonMakerSkeletonSegment[] copied = new DungeonMakerSkeletonSegment[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DungeonMakerSkeletonSegment segment = source[i];
                int[] ownedTileIndices = new int[segment.OwnedTileIndices.Length];
                Array.Copy(segment.OwnedTileIndices, ownedTileIndices, ownedTileIndices.Length);
                copied[i] = new DungeonMakerSkeletonSegment(segment.Id, segment.BuilderId, segment.Start, segment.End, ownedTileIndices);
            }

            return copied;
        }

        private static DungeonMakerSkeletonLink[] CopySkeletonLinks(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerSkeletonLink> source = result.SkeletonLinks;
            DungeonMakerSkeletonLink[] copied = new DungeonMakerSkeletonLink[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DungeonMakerSkeletonLink link = source[i];
                copied[i] = new DungeonMakerSkeletonLink(link.FromSegmentId, link.ToSegmentId, link.From, link.To);
            }

            return copied;
        }

        private static DungeonMakerSkeletonAttachment[] CopySkeletonAttachments(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerSkeletonAttachment> source = result.SkeletonAttachments;
            DungeonMakerSkeletonAttachment[] copied = new DungeonMakerSkeletonAttachment[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DungeonMakerSkeletonAttachment attachment = source[i];
                copied[i] = new DungeonMakerSkeletonAttachment(attachment.SegmentId);
            }

            return copied;
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

        private static bool IsBfsAnchorTile(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.IR_OPEN
                or DungeonMakerSquareData.IA_OPEN
                or DungeonMakerSquareData.H_DOOR
                or DungeonMakerSquareData.V_DOOR;
        }

        private static int CountActiveWalkableNeighbors(
            DungeonMakerSquareData[] map,
            int width,
            int height,
            int x,
            int y)
        {
            int count = 0;
            if (IsActiveWalkableTile(map, width, height, x + 1, y))
                count++;
            if (IsActiveWalkableTile(map, width, height, x - 1, y))
                count++;
            if (IsActiveWalkableTile(map, width, height, x, y + 1))
                count++;
            if (IsActiveWalkableTile(map, width, height, x, y - 1))
                count++;
            return count;
        }

        private static bool IsActiveWalkableTile(
            DungeonMakerSquareData[] map,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            return IsWalkableTile(map[GetIndex(x, y, height)]);
        }

        private static void TryEnqueueDeadEndNeighbor(
            Queue<int> queue,
            DungeonMakerSquareData[] map,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int index = GetIndex(x, y, height);
            if (IsPrunableDeadEndTile(map[index]))
                queue.Enqueue(index);
        }

        private static bool IsBlockedTile(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.CLOSED
                or DungeonMakerSquareData.G_CLOSED
                or DungeonMakerSquareData.NJ_CLOSED
                or DungeonMakerSquareData.NJ_G_CLOSED
                or DungeonMakerSquareData.COLUMN;
        }

        private static bool IsWalkableTile(DungeonMakerSquareData tile)
        {
            return !IsBlockedTile(tile);
        }

        private static bool CanHostEncounterMarker(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.IT_OPEN
                or DungeonMakerSquareData.IR_OPEN
                or DungeonMakerSquareData.IA_OPEN
                or DungeonMakerSquareData.OPEN
                or DungeonMakerSquareData.G_OPEN
                or DungeonMakerSquareData.NJ_OPEN
                or DungeonMakerSquareData.NJ_G_OPEN;
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

        private static void TryEnqueueReachableCorridor(
            bool[] reachableCorridorMask,
            Queue<int> queue,
            DungeonMakerSquareData[] map,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = GetIndex(x, y, height);
            if (reachableCorridorMask[index] || !IsPrunableDeadEndTile(map[index]))
                return;

            reachableCorridorMask[index] = true;
            queue.Enqueue(index);
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

        private static void CollectBlockedRegionNeighbor(
            DungeonMakerSquareData[] map,
            bool[] visited,
            Queue<int> queue,
            HashSet<int> surroundingCorridorTiles,
            ref bool hasProtectedAdjacency,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = GetIndex(x, y, height);
            DungeonMakerSquareData tile = map[index];
            if (IsBlockedTile(tile))
            {
                if (visited[index])
                    return;

                visited[index] = true;
                queue.Enqueue(index);
                return;
            }

            if (!IsWalkableTile(tile))
                return;

            if (IsPrunableDeadEndTile(tile))
                surroundingCorridorTiles.Add(index);
            else
                hasProtectedAdjacency = true;
        }

        private static bool[] BuildProtectedPrunableAnchorMask(DungeonMakerSquareData[] map, bool[] prunableMask, int width, int height)
        {
            bool[] anchorMask = new bool[map.Length];
            for (int index = 0; index < map.Length; index++)
            {
                if (!prunableMask[index])
                    continue;

                int x = index / height;
                int y = index % height;
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    anchorMask[index] = true;
                    continue;
                }

                if (TouchesProtectedWalkable(map, width, height, x + 1, y)
                    || TouchesProtectedWalkable(map, width, height, x - 1, y)
                    || TouchesProtectedWalkable(map, width, height, x, y + 1)
                    || TouchesProtectedWalkable(map, width, height, x, y - 1))
                {
                    anchorMask[index] = true;
                }
            }

            return anchorMask;
        }

        private static bool TouchesProtectedWalkable(DungeonMakerSquareData[] map, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return true;

            DungeonMakerSquareData tile = map[GetIndex(x, y, height)];
            return IsWalkableTile(tile) && !IsPrunableDeadEndTile(tile);
        }

        private static void ExplorePrunableComponentNeighbor(
            DungeonMakerSquareData[] map,
            bool[] prunableMask,
            bool[] removedLoopMask,
            bool[] removedDeadEndMask,
            bool[] visited,
            Queue<int> queue,
            ref bool touchesRemovedSeed,
            int width,
            int height,
            int x,
            int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = GetIndex(x, y, height);
            if (removedLoopMask[index] || removedDeadEndMask[index])
            {
                touchesRemovedSeed = true;
                return;
            }

            if (!prunableMask[index] || visited[index] || !IsPrunableDeadEndTile(map[index]))
                return;

            visited[index] = true;
            queue.Enqueue(index);
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

        private static string GetMapDumpFilePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, GenerationReportDirectoryName, MapDumpFileName);
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

        private void CreateAnalysisOverlay()
        {
            Transform existingOverlay = _generatedRoot.Find(AnalysisOverlayName);
            if (existingOverlay != null)
            {
                if (Application.isPlaying)
                    Destroy(existingOverlay.gameObject);
                else
                    DestroyImmediate(existingOverlay.gameObject);
            }

            DestroyPreviewObject(_analysisOverlaySprite);
            DestroyPreviewObject(_analysisOverlayTexture);
            _analysisOverlaySprite = null;
            _analysisOverlayTexture = null;

            if (!_drawAnalysisOverlay || !HasAnyAnalysisTile())
                return;

            GameObject overlayObject = new(AnalysisOverlayName);
            overlayObject.transform.SetParent(_generatedRoot, false);
            overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.45f);

            _analysisOverlayTexture = BuildAnalysisOverlayTexture();
            _analysisOverlaySprite = Sprite.Create(
                _analysisOverlayTexture,
                new Rect(0f, 0f, _analysisOverlayTexture.width, _analysisOverlayTexture.height),
                new Vector2(0.5f, 0.5f),
                1f / _cellSize,
                0,
                SpriteMeshType.FullRect);

            SpriteRenderer spriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _analysisOverlaySprite;
            spriteRenderer.sortingOrder = 10;
        }

        private void CreateSkeletonOverlay()
        {
            Transform existingOverlay = _generatedRoot.Find(SkeletonOverlayName);
            if (existingOverlay != null)
            {
                if (Application.isPlaying)
                    Destroy(existingOverlay.gameObject);
                else
                    DestroyImmediate(existingOverlay.gameObject);
            }

            DestroyPreviewObject(_skeletonOverlaySprite);
            DestroyPreviewObject(_skeletonOverlayTexture);
            _skeletonOverlaySprite = null;
            _skeletonOverlayTexture = null;

            if (!_drawSkeletonOverlay || !HasAnySkeletonTile())
                return;

            GameObject overlayObject = new(SkeletonOverlayName);
            overlayObject.transform.SetParent(_generatedRoot, false);
            overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.4f);

            _skeletonOverlayTexture = BuildSkeletonOverlayTexture();
            _skeletonOverlaySprite = Sprite.Create(
                _skeletonOverlayTexture,
                new Rect(0f, 0f, _skeletonOverlayTexture.width, _skeletonOverlayTexture.height),
                new Vector2(0.5f, 0.5f),
                1f / _cellSize,
                0,
                SpriteMeshType.FullRect);

            SpriteRenderer spriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _skeletonOverlaySprite;
            spriteRenderer.sortingOrder = 20;
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

            DungeonMakerSpecialRoomRole[] roomRoleMap = BuildSpecialRoomRoleMap(_lastResult);
            Color[] pixels = new Color[_lastResult.DisplayWidth * _lastResult.DisplayHeight];
            for (int y = 0; y < _lastResult.DisplayHeight; y++)
            {
                for (int x = 0; x < _lastResult.DisplayWidth; x++)
                {
                    int pixelIndex = y * _lastResult.DisplayWidth + x;
                    pixels[pixelIndex] = GetTileColor(
                        _lastResult.GetDisplayTile(x, y),
                        roomRoleMap[GetIndex(y, x, _lastResult.SourceHeight)]);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static DungeonMakerSpecialRoomRole[] BuildSpecialRoomRoleMap(DungeonMakerTunnelingResult result)
        {
            DungeonMakerSpecialRoomRole[] roleMap = new DungeonMakerSpecialRoomRole[result.SourceWidth * result.SourceHeight];
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                if (region.Kind != DungeonMakerRegionKind.Room || region.SpecialRoomRole == DungeonMakerSpecialRoomRole.None)
                    continue;

                int[] tiles = region.TileIndices;
                for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
                    roleMap[tiles[tileIndex]] = region.SpecialRoomRole;
            }

            return roleMap;
        }

        private Texture2D BuildAnalysisOverlayTexture()
        {
            Texture2D texture = new(_lastResult.DisplayWidth, _lastResult.DisplayHeight, TextureFormat.RGBA32, false)
            {
                name = "TunnelingDemoAnalysisOverlay",
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
                    int sourceX = y;
                    int sourceY = x;
                    int sourceIndex = GetIndex(sourceX, sourceY, _lastResult.SourceHeight);
                    int pixelIndex = y * _lastResult.DisplayWidth + x;
                    bool isDeadEnd = _lastDeadEndMask != null && _lastDeadEndMask[sourceIndex];
                    if (isDeadEnd)
                        pixels[pixelIndex] = _deadEndColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D BuildSkeletonOverlayTexture()
        {
            Texture2D texture = new(_lastResult.DisplayWidth, _lastResult.DisplayHeight, TextureFormat.RGBA32, false)
            {
                name = "TunnelingDemoSkeletonOverlay",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[_lastResult.DisplayWidth * _lastResult.DisplayHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            IReadOnlyList<DungeonMakerSkeletonLink> links = _lastResult.SkeletonLinks;
            for (int i = 0; i < links.Count; i++)
            {
                DungeonMakerSkeletonLink link = links[i];
                DrawOverlayLine(pixels, link.From, link.To, _skeletonAnchorColor);
            }

            IReadOnlyList<DungeonMakerSkeletonSegment> segments = _lastResult.SkeletonSegments;
            for (int i = 0; i < segments.Count; i++)
            {
                DungeonMakerSkeletonSegment segment = segments[i];
                DrawOverlayLine(pixels, segment.Start, segment.End, _skeletonCorridorColor);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private bool HasAnySkeletonTile()
        {
            if (_lastResult == null)
                return false;

            return _lastResult.SkeletonSegments.Count > 0 || _lastResult.SkeletonLinks.Count > 0;
        }

        private void DrawOverlayLine(Color[] pixels, Vector2Int sourceFrom, Vector2Int sourceTo, Color color)
        {
            Vector2Int current = sourceFrom;
            SetOverlayPixel(pixels, current, color);

            while (current.x != sourceTo.x)
            {
                current.x += Math.Sign(sourceTo.x - current.x);
                SetOverlayPixel(pixels, current, color);
            }

            while (current.y != sourceTo.y)
            {
                current.y += Math.Sign(sourceTo.y - current.y);
                SetOverlayPixel(pixels, current, color);
            }
        }

        private void SetOverlayPixel(Color[] pixels, Vector2Int sourcePoint, Color color)
        {
            int displayX = sourcePoint.y;
            int displayY = sourcePoint.x;
            if (displayX < 0 || displayY < 0 || displayX >= _lastResult.DisplayWidth || displayY >= _lastResult.DisplayHeight)
                return;

            int pixelIndex = displayY * _lastResult.DisplayWidth + displayX;
            pixels[pixelIndex] = color;
        }

        private Color GetTileColor(DungeonMakerSquareData tile, DungeonMakerSpecialRoomRole specialRoomRole)
        {
            if (specialRoomRole is DungeonMakerSpecialRoomRole.Start or DungeonMakerSpecialRoomRole.NextLevel)
            {
                if (tile == DungeonMakerSquareData.IR_OPEN)
                    return _specialRoomColor;
            }

            return tile switch
            {
                DungeonMakerSquareData.MOB1 => _mobLevel1Color,
                DungeonMakerSquareData.MOB2 => _mobLevel2Color,
                DungeonMakerSquareData.MOB3 => _mobLevel3Color,
                DungeonMakerSquareData.TREAS1 => _treasureLevel1Color,
                DungeonMakerSquareData.TREAS2 => _treasureLevel2Color,
                DungeonMakerSquareData.TREAS3 => _treasureLevel3Color,
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

        private static string GetTileDumpToken(DungeonMakerSquareData tile)
        {
            return tile switch
            {
                DungeonMakerSquareData.OPEN => "O",
                DungeonMakerSquareData.G_OPEN => "GO",
                DungeonMakerSquareData.NJ_OPEN => "NJO",
                DungeonMakerSquareData.NJ_G_OPEN => "NJGO",
                DungeonMakerSquareData.IT_OPEN => "T",
                DungeonMakerSquareData.IR_OPEN => "R",
                DungeonMakerSquareData.IA_OPEN => "A",
                DungeonMakerSquareData.H_DOOR => "H",
                DungeonMakerSquareData.V_DOOR => "V",
                DungeonMakerSquareData.COLUMN => "C",
                DungeonMakerSquareData.CLOSED => "X",
                DungeonMakerSquareData.G_CLOSED => "GX",
                DungeonMakerSquareData.NJ_CLOSED => "NJX",
                DungeonMakerSquareData.NJ_G_CLOSED => "NJGX",
                DungeonMakerSquareData.MOB1 => "M1",
                DungeonMakerSquareData.MOB2 => "M2",
                DungeonMakerSquareData.MOB3 => "M3",
                DungeonMakerSquareData.TREAS1 => "TR1",
                DungeonMakerSquareData.TREAS2 => "TR2",
                DungeonMakerSquareData.TREAS3 => "TR3",
                _ => "?",
            };
        }

        private static string GetOriginDumpToken(DungeonMakerTileOrigin origin)
        {
            return origin switch
            {
                DungeonMakerTileOrigin.None => "..",
                DungeonMakerTileOrigin.BoundaryClosed => "BC",
                DungeonMakerTileOrigin.TunnelCarve => "TC",
                DungeonMakerTileOrigin.TunnelOffsetJoin => "OJ",
                DungeonMakerTileOrigin.TunnelParallelJoinLead => "PJ",
                DungeonMakerTileOrigin.TunnelParallelJoinExtend => "PX",
                DungeonMakerTileOrigin.TunnelProbeRestore => "PR",
                DungeonMakerTileOrigin.AnteRoomCarve => "AC",
                DungeonMakerTileOrigin.RoomCarve => "RC",
                DungeonMakerTileOrigin.DoorPlacement => "DP",
                DungeonMakerTileOrigin.ColumnPlacement => "CP",
                DungeonMakerTileOrigin.MonsterPlacement => "MP",
                DungeonMakerTileOrigin.TreasurePlacement => "TP",
                _ => "??",
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

        private bool HasAnyAnalysisTile()
        {
            return HasAnyMarkedTile(_lastDeadEndMask);
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
            DestroyPreviewObject(_analysisOverlaySprite);
            DestroyPreviewObject(_analysisOverlayTexture);
            DestroyPreviewObject(_skeletonOverlaySprite);
            DestroyPreviewObject(_skeletonOverlayTexture);
            DestroyPreviewObject(_debugCoordinateMarkerMaterial);
            _previewSprite = null;
            _previewTexture = null;
            _analysisOverlaySprite = null;
            _analysisOverlayTexture = null;
            _skeletonOverlaySprite = null;
            _skeletonOverlayTexture = null;
            _debugCoordinateMarkerMaterial = null;
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
