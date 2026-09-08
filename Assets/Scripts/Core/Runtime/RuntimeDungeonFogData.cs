using System;
using CrystalMagic.Game.OpenField;
using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class RuntimeDungeonFogData
    {
        private static readonly Color32 UnexploredColor = new(0, 0, 0, 255);
        private static readonly Color32 ExploredColor = new(0, 0, 0, 150);
        private static readonly Color32 VisibleColor = new(0, 0, 0, 0);
        private const float VisualFadeDurationSeconds = 0.2f;

        private readonly OpenFieldDungeonLayout _layout;
        private readonly bool[] _exploredCells;
        private readonly bool[] _visibleCells;
        private readonly float[] _displayAlpha;
        private readonly Color32[] _pixels;
        private readonly Vector2 _worldOrigin;
        private readonly float _cellWorldSize;

        private Vector2Int _lastPlayerCell = new(int.MinValue, int.MinValue);

        public RuntimeDungeonFogData(OpenFieldDungeonLayout layout, RuntimeDungeonSceneData sceneData, int visionRadiusCells = 8)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Width = layout.Width;
            Height = layout.Height;
            TextureWidth = Width + 2;
            TextureHeight = Height + 2;
            VisionRadiusCells = Mathf.Max(1, visionRadiusCells);
            _exploredCells = new bool[Width * Height];
            _visibleCells = new bool[Width * Height];
            _displayAlpha = new float[Width * Height];
            _pixels = new Color32[TextureWidth * TextureHeight];
            for (int index = 0; index < _displayAlpha.Length; index++)
                _displayAlpha[index] = UnexploredColor.a;
            _cellWorldSize = sceneData != null && sceneData.CellWorldSize > 0f
                ? sceneData.CellWorldSize
                : 1f;
            _worldOrigin = sceneData?.TerrainVisual != null
                ? sceneData.TerrainVisual.WorldOrigin
                : new Vector2(-Width * _cellWorldSize * 0.5f, -Height * _cellWorldSize * 0.5f);

            RebuildPixels();
        }

        public int Width { get; }
        public int Height { get; }
        public int TextureWidth { get; }
        public int TextureHeight { get; }
        public int VisionRadiusCells { get; }
        public int Version { get; private set; }
        public Vector2 WorldOrigin => _worldOrigin;
        public float CellWorldSize => _cellWorldSize;
        public Texture2D Texture { get; private set; }
        public Sprite WorldSprite { get; private set; }
        public Sprite MinimapSprite { get; private set; }

        public void CreateVisualAssets()
        {
            if (Texture != null)
                return;

            Texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                name = "RuntimeDungeonFog",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            Texture.SetPixels32(_pixels);
            Texture.Apply(false, false);

            WorldSprite = Sprite.Create(
                Texture,
                new Rect(1f, 1f, Width, Height),
                new Vector2(0.5f, 0.5f),
                1f / _cellWorldSize);
            WorldSprite.name = "RuntimeDungeonFogWorldSprite";
            WorldSprite.hideFlags = HideFlags.DontSave;

            MinimapSprite = Sprite.Create(
                Texture,
                new Rect(0f, 0f, TextureWidth, TextureHeight),
                new Vector2(0.5f, 0.5f),
                1f);
            MinimapSprite.name = "RuntimeDungeonFogMinimapSprite";
            MinimapSprite.hideFlags = HideFlags.DontSave;
            Version++;
        }

        public bool TryGetCell(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = new Vector2Int(
                Mathf.FloorToInt((worldPosition.x - _worldOrigin.x) / _cellWorldSize),
                Mathf.FloorToInt((worldPosition.y - _worldOrigin.y) / _cellWorldSize));
            return _layout.IsInside(cell.x, cell.y);
        }

        public bool IsExplored(int x, int y)
        {
            return _layout.IsInside(x, y) && _exploredCells[GetIndex(x, y)];
        }

        public bool IsVisible(int x, int y)
        {
            return _layout.IsInside(x, y) && _visibleCells[GetIndex(x, y)];
        }

        public bool UpdateVisibility(Vector2Int playerCell)
        {
            if (!_layout.IsInside(playerCell.x, playerCell.y) || playerCell == _lastPlayerCell)
                return false;

            _lastPlayerCell = playerCell;
            Array.Clear(_visibleCells, 0, _visibleCells.Length);
            int radiusSquared = VisionRadiusCells * VisionRadiusCells;
            int minimumX = Mathf.Max(0, playerCell.x - VisionRadiusCells);
            int maximumX = Mathf.Min(Width - 1, playerCell.x + VisionRadiusCells);
            int minimumY = Mathf.Max(0, playerCell.y - VisionRadiusCells);
            int maximumY = Mathf.Min(Height - 1, playerCell.y + VisionRadiusCells);

            for (int y = minimumY; y <= maximumY; y++)
            {
                int offsetY = y - playerCell.y;
                for (int x = minimumX; x <= maximumX; x++)
                {
                    int offsetX = x - playerCell.x;
                    if (offsetX * offsetX + offsetY * offsetY > radiusSquared || !HasLineOfSight(playerCell, new Vector2Int(x, y)))
                        continue;

                    int index = GetIndex(x, y);
                    _visibleCells[index] = true;
                    _exploredCells[index] = true;
                }
            }

            return true;
        }

        public bool UpdateVisual(float deltaTime)
        {
            float maxAlphaChange = 255f * Mathf.Max(0f, deltaTime) / VisualFadeDurationSeconds;
            bool changed = false;
            for (int index = 0; index < _displayAlpha.Length; index++)
            {
                float nextAlpha = Mathf.MoveTowards(_displayAlpha[index], GetTargetAlpha(index), maxAlphaChange);
                if (Mathf.Abs(nextAlpha - _displayAlpha[index]) < 0.01f)
                    continue;

                _displayAlpha[index] = nextAlpha;
                SetPixel(index);
                changed = true;
            }

            if (!changed)
                return false;

            if (Texture != null)
            {
                Texture.SetPixels32(_pixels);
                Texture.Apply(false, false);
            }

            Version++;
            return true;
        }

        private bool HasLineOfSight(Vector2Int source, Vector2Int target)
        {
            int x = source.x;
            int y = source.y;
            int deltaX = Mathf.Abs(target.x - source.x);
            int deltaY = Mathf.Abs(target.y - source.y);
            int stepX = source.x < target.x ? 1 : -1;
            int stepY = source.y < target.y ? 1 : -1;
            int error = deltaX - deltaY;

            while (x != target.x || y != target.y)
            {
                int doubleError = error * 2;
                if (doubleError > -deltaY)
                {
                    error -= deltaY;
                    x += stepX;
                }
                if (doubleError < deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }

                if (x == target.x && y == target.y)
                    return true;
                if (_layout.BlocksLineOfSight(x, y))
                    return false;
            }

            return true;
        }

        private void RebuildPixels()
        {
            for (int index = 0; index < _pixels.Length; index++)
                _pixels[index] = UnexploredColor;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = GetIndex(x, y);
                    SetPixel(index);
                }
            }

            if (Texture == null)
                return;

            Texture.SetPixels32(_pixels);
            Texture.Apply(false, false);
        }

        private int GetIndex(int x, int y)
        {
            return y * Width + x;
        }

        private float GetTargetAlpha(int index)
        {
            return _visibleCells[index]
                ? VisibleColor.a
                : _exploredCells[index]
                    ? ExploredColor.a
                    : UnexploredColor.a;
        }

        private void SetPixel(int index)
        {
            int x = index % Width;
            int y = index / Width;
            _pixels[(x + 1) + (y + 1) * TextureWidth] = new Color32(
                0,
                0,
                0,
                (byte)Mathf.RoundToInt(_displayAlpha[index]));
        }
    }
}
