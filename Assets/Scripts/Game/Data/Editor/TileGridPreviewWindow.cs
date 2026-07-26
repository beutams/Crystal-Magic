using System;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public sealed class TileGridPreviewWindow : EditorWindow
    {
        private const float Padding = 16f;
        private DungeonTileGridData _tileGrid;
        private Action _onChanged;
        private bool _isSizeLocked;
        private bool _isCenterCellDisabled;
        private int _selectedCellIndex = -1;

        [MenuItem("Tools/Config/Tile Grid Preview")]
        public static void OpenStandalone()
        {
            Open("Tile Grid Preview", new DungeonTileGridData(), false, null);
        }

        public static void Open(
            string title,
            DungeonTileGridData tileGrid,
            bool isSizeLocked,
            Action onChanged,
            bool isCenterCellDisabled = false)
        {
            TileGridPreviewWindow window = GetWindow<TileGridPreviewWindow>(title);
            window.minSize = new Vector2(420f, 480f);
            window.titleContent = new GUIContent(title);
            window._tileGrid = tileGrid ?? new DungeonTileGridData();
            window._tileGrid.EnsureValid();
            window._isSizeLocked = isSizeLocked;
            window._isCenterCellDisabled = isCenterCellDisabled;
            window._onChanged = onChanged;
            window._selectedCellIndex = -1;
            if (window.SyncMissingSpriteUvs())
                onChanged?.Invoke();
            window.Show();
        }

        private void OnGUI()
        {
            if (_tileGrid == null)
            {
                EditorGUILayout.HelpBox("Open this window from a dungeon visual setting, or use the standalone preview menu.", MessageType.Info);
                return;
            }

            _tileGrid.EnsureValid();
            DrawToolbar();
            DrawSelectedCellSettings();
            DrawGrid();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Drag a Sprite from the Project window into a cell.", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(46f)))
            {
                for (int i = 0; i < _tileGrid.Cells.Count; i++)
                {
                    _tileGrid.Cells[i].SpritePath = string.Empty;
                    _tileGrid.Cells[i].SpriteName = string.Empty;
                    _tileGrid.Cells[i].HasCollision = false;
                }
                NotifyChanged();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedCellSettings()
        {
            EditorGUILayout.BeginVertical("box");
            if (_isSizeLocked)
            {
                EditorGUILayout.LabelField($"Grid Size: {_tileGrid.Columns} x {_tileGrid.Rows}", EditorStyles.miniBoldLabel);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                int columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", _tileGrid.Columns));
                int rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", _tileGrid.Rows));
                if (GUILayout.Button("Resize", GUILayout.Width(64f))
                    && (columns != _tileGrid.Columns || rows != _tileGrid.Rows))
                {
                    _tileGrid.EnsureSize(columns, rows);
                    _selectedCellIndex = -1;
                    NotifyChanged();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (_selectedCellIndex < 0 || _selectedCellIndex >= _tileGrid.Cells.Count)
            {
                EditorGUILayout.LabelField("Select a cell to configure collision.", EditorStyles.miniLabel);
            }
            else
            {
                DungeonTileGridCellData selectedCell = _tileGrid.Cells[_selectedCellIndex];
                int column = _selectedCellIndex % _tileGrid.Columns + 1;
                int row = _selectedCellIndex / _tileGrid.Columns + 1;
                EditorGUILayout.LabelField($"Selected Cell: {column}, {row}", EditorStyles.miniBoldLabel);
                bool hasCollision = EditorGUILayout.Toggle("Has Collision", selectedCell.HasCollision);
                if (hasCollision != selectedCell.HasCollision)
                {
                    selectedCell.HasCollision = hasCollision;
                    NotifyChanged();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawGrid()
        {
            Rect availableRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float cellSize = Mathf.Floor(Mathf.Min(
                (availableRect.width - Padding * 2f) / _tileGrid.Columns,
                (availableRect.height - Padding * 2f) / _tileGrid.Rows));
            if (cellSize <= 0f)
                return;

            float gridWidth = cellSize * _tileGrid.Columns;
            float gridHeight = cellSize * _tileGrid.Rows;
            Rect gridRect = new(
                availableRect.x + (availableRect.width - gridWidth) * 0.5f,
                availableRect.y + (availableRect.height - gridHeight) * 0.5f,
                gridWidth,
                gridHeight);

            for (int row = 0; row < _tileGrid.Rows; row++)
            {
                for (int column = 0; column < _tileGrid.Columns; column++)
                {
                    int index = row * _tileGrid.Columns + column;
                    Rect cellRect = new(
                        gridRect.x + column * cellSize,
                        gridRect.y + row * cellSize,
                        cellSize,
                        cellSize);
                    bool isDisabled = IsCellDisabled(index);
                    DrawCell(cellRect, _tileGrid.Cells[index], index == _selectedCellIndex, isDisabled);
                    if (!isDisabled)
                        HandleCellInput(cellRect, index);
                }
            }
        }

        private static void DrawCell(Rect rect, DungeonTileGridCellData cell, bool isSelected, bool isDisabled)
        {
            EditorGUI.DrawRect(rect, Color.black);
            if (isDisabled)
            {
                EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f));
                DrawBorder(rect, new Color(0.25f, 0.25f, 0.25f));
                return;
            }

            Sprite sprite = LoadSprite(cell);
            if (sprite != null && sprite.texture != null)
            {
                Rect textureRect = sprite.textureRect;
                Rect textureCoordinates = new(
                    textureRect.x / sprite.texture.width,
                    textureRect.y / sprite.texture.height,
                    textureRect.width / sprite.texture.width,
                    textureRect.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(rect, sprite.texture, textureCoordinates, true);
            }

            Color borderColor = isSelected
                ? new Color(0.95f, 0.75f, 0.2f)
                : cell.HasCollision ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.35f, 0.35f, 0.35f);
            DrawBorder(rect, borderColor);
        }

        private void HandleCellInput(Rect cellRect, int index)
        {
            Event currentEvent = Event.current;
            if (!cellRect.Contains(currentEvent.mousePosition))
                return;

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    if (TryGetDraggedSprite(out _))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        currentEvent.Use();
                    }
                    break;
                case EventType.DragPerform:
                    if (TryGetDraggedSprite(out Sprite sprite))
                    {
                        DragAndDrop.AcceptDrag();
                        SetCellSprite(_tileGrid.Cells[index], sprite);
                        _selectedCellIndex = index;
                        NotifyChanged();
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseDown:
                    _selectedCellIndex = index;
                    Repaint();
                    currentEvent.Use();
                    break;
            }
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static bool TryGetDraggedSprite(out Sprite sprite)
        {
            UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences;
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                if (draggedObjects[i] is Sprite draggedSprite)
                {
                    sprite = draggedSprite;
                    return true;
                }
            }

            sprite = null;
            return false;
        }

        private static Sprite LoadSprite(DungeonTileGridCellData cell)
        {
            if (cell == null
                || string.IsNullOrWhiteSpace(cell.SpritePath)
                || string.IsNullOrWhiteSpace(cell.SpriteName))
                return null;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(cell.SpritePath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && string.Equals(sprite.name, cell.SpriteName, StringComparison.Ordinal))
                    return sprite;
            }

            return null;
        }

        private bool SyncMissingSpriteUvs()
        {
            bool changed = false;
            for (int i = 0; i < _tileGrid.Cells.Count; i++)
            {
                DungeonTileGridCellData cell = _tileGrid.Cells[i];
                if (cell == null || cell.HasSpriteUv)
                    continue;

                Sprite sprite = LoadSprite(cell);
                if (sprite == null)
                    continue;

                SetCellSprite(cell, sprite);
                changed = true;
            }

            return changed;
        }

        private static void SetCellSprite(DungeonTileGridCellData cell, Sprite sprite)
        {
            if (cell == null || sprite == null)
                return;

            cell.SpritePath = AssetDatabase.GetAssetPath(sprite);
            cell.SpriteName = sprite.name;
            Texture2D texture = sprite.texture;
            cell.SetSpriteUv(sprite.textureRect, texture != null ? texture.width : 0, texture != null ? texture.height : 0);
        }

        private void NotifyChanged()
        {
            _onChanged?.Invoke();
            Repaint();
        }

        private bool IsCellDisabled(int index)
        {
            return _isCenterCellDisabled
                && _tileGrid.Columns == 3
                && _tileGrid.Rows == 3
                && index == 4;
        }
    }
}
