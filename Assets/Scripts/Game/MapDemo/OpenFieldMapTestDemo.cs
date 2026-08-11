using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo
{
    [DisallowMultipleComponent]
    public sealed class OpenFieldMapTestDemo : MonoBehaviour
    {
        private const int MinimumMapSide = 48;
        private const int AnchorPlacementAttempts = 512;

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
        private sealed class OpenFieldMapData
        {
            public readonly int Width;
            public readonly int Height;
            public readonly float[] GroundY;
            public readonly ElevationBand[] ElevationBands;
            public readonly bool[] ObstacleMask;
            public readonly List<MapAnchor> Anchors = new();

            public OpenFieldMapData(int width, int height)
            {
                Width = width;
                Height = height;
                GroundY = new float[width * height];
                ElevationBands = new ElevationBand[width * height];
                ObstacleMask = new bool[width * height];
            }

            public int GetIndex(int x, int y)
            {
                return y * Width + x;
            }
        }

        [Header("Map Size")]
        [SerializeField, Min(MinimumMapSide)] private int _mapWidth = 80;
        [SerializeField, Min(MinimumMapSide)] private int _mapHeight = 200;
        [Header("Terrain Noise")]
        [SerializeField, Range(0.01f, 0.99f)] private float _lowToMiddleHeight = 0.42f;
        [SerializeField, Range(0.01f, 0.99f)] private float _middleToHighHeight = 0.58f;
        [SerializeField, Min(2f)] private float _highFrequencyMultiplier = 6f;
        [SerializeField, Range(0f, 1.5f)] private float _highFrequencyAmplitude = 0.65f;

        private static readonly Color LowHeightColor = new(0.12f, 0.35f, 0.48f, 1f);
        private static readonly Color MidHeightColor = new(0.41f, 0.59f, 0.36f, 1f);
        private static readonly Color HighHeightColor = new(0.77f, 0.54f, 0.29f, 1f);
        private static readonly Color SpawnColor = new(0.25f, 0.47f, 0.98f, 1f);
        private static readonly Color ExitColor = new(0.88f, 0.28f, 0.83f, 1f);
        private static readonly Color LargeInterestColor = new(0.88f, 0.35f, 0.26f, 1f);
        private static readonly Color MediumInterestColor = new(0.94f, 0.70f, 0.24f, 1f);
        private static readonly Color SmallInterestColor = new(0.39f, 0.79f, 0.34f, 1f);

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
            _lowToMiddleHeight = Mathf.Clamp(_lowToMiddleHeight, 0.01f, 0.98f);
            _middleToHighHeight = Mathf.Clamp(_middleToHighHeight, _lowToMiddleHeight + 0.01f, 0.99f);
            _highFrequencyMultiplier = Mathf.Max(2f, _highFrequencyMultiplier);
            _highFrequencyAmplitude = Mathf.Clamp(_highFrequencyAmplitude, 0f, 1.5f);
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
            }
            while (!TryPlanAnchors(mapData, new System.Random(seed)));

            BuildPerlinHeight(mapData, seed);
            QuantizeElevationBands(mapData);
            _lastMapData = mapData;
            _previewSeed = seed;
            BuildTexture(mapData);
            RequestPreviewRepaint();
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

        private void BuildPerlinHeight(OpenFieldMapData mapData, int seed)
        {
            System.Random random = new(seed);
            float offsetX = random.Next(-10000, 10001);
            float offsetY = random.Next(-10000, 10001);
            float baseScale = Mathf.Clamp(Mathf.Min(mapData.Width, mapData.Height) * 0.65f, 30f, 64f);

            for (int y = 0; y < mapData.Height; y++)
            {
                for (int x = 0; x < mapData.Width; x++)
                    mapData.GroundY[mapData.GetIndex(x, y)] = SampleFbmPerlin(x, y, offsetX, offsetY, baseScale, _highFrequencyMultiplier, _highFrequencyAmplitude);
            }
        }

        private static float SampleFbmPerlin(
            float x,
            float y,
            float offsetX,
            float offsetY,
            float baseScale,
            float highFrequencyMultiplier,
            float highFrequencyAmplitude)
        {
            float macro = Mathf.PerlinNoise((x + offsetX) / baseScale, (y + offsetY) / baseScale);
            float medium = Mathf.PerlinNoise((x + offsetX) / baseScale * 2f, (y + offsetY) / baseScale * 2f);
            float detail = Mathf.PerlinNoise(
                (x + offsetX) / baseScale * highFrequencyMultiplier,
                (y + offsetY) / baseScale * highFrequencyMultiplier);
            return (macro + medium * 0.5f + detail * highFrequencyAmplitude) / (1.5f + highFrequencyAmplitude);
        }
        private void QuantizeElevationBands(OpenFieldMapData mapData)
        {
            for (int i = 0; i < mapData.GroundY.Length; i++)
                mapData.ElevationBands[i] = GetElevationBand(mapData.GroundY[i]);
        }

        private ElevationBand GetElevationBand(float height)
        {
            if (height < _lowToMiddleHeight)
                return ElevationBand.Low;
            if (height < _middleToHighHeight)
                return ElevationBand.Middle;

            return ElevationBand.High;
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

        private static Color GetElevationBandColor(ElevationBand band)
        {
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
            float y = Mathf.Max(20f, mapRect.yMin - 214f);
            GUI.Box(new Rect(x, y, 300f, 220f), "Open Field Map - Stage 5");
            GUI.Label(new Rect(x + 12f, y + 24f, 276f, 20f), $"Seed: {PreviewSeed}", _legendLabelStyle);
            GUI.Label(new Rect(x + 12f, y + 44f, 276f, 20f), $"Map: {PreviewWidth} x {PreviewHeight}", _legendLabelStyle);
            GUI.Label(new Rect(x + 12f, y + 64f, 276f, 20f), "Height: Low / Middle / High", _legendLabelStyle);
            GUI.Label(new Rect(x + 12f, y + 84f, 276f, 20f), $"Bands: 0-{_lowToMiddleHeight:0.00} / {_lowToMiddleHeight:0.00}-{_middleToHighHeight:0.00} / {_middleToHighHeight:0.00}-1", _legendLabelStyle);
            DrawLegendEntry(x + 12f, y + 110f, LowHeightColor, "Low Height");
            DrawLegendEntry(x + 150f, y + 110f, MidHeightColor, "Mid Height");
            DrawLegendEntry(x + 12f, y + 134f, HighHeightColor, "High Height");
            DrawLegendEntry(x + 150f, y + 134f, SpawnColor, "Spawn");
            DrawLegendEntry(x + 12f, y + 158f, ExitColor, "Exit");
            DrawLegendEntry(x + 150f, y + 158f, LargeInterestColor, "Large Interest");
            DrawLegendEntry(x + 12f, y + 182f, MediumInterestColor, "Medium Interest");
            DrawLegendEntry(x + 150f, y + 182f, SmallInterestColor, "Small Interest");
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
