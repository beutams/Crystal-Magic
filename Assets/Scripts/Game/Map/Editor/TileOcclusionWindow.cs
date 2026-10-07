using System;
using System.Linq;
using CrystalMagic.Game.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalMagic.Editor.Map
{
    public sealed class TileOcclusionWindow : EditorWindow
    {
        [SerializeField] private GameObject _target;
        [SerializeField] private TileOcclusionMap _map;
        [SerializeField] private int _regionIndex;
        [SerializeField] private int _tool;
        private Vector2 _scroll;
        private bool _dragging;
        private Vector3 _dragStart, _dragEnd;
        private string _message;
        [SerializeField] private bool _showCollision = true;

        [MenuItem("Tools/Map/Tile Occlusion Editor")]
        public static void Open() => GetWindow<TileOcclusionWindow>("Tile 遮挡分组");

        public static void OpenForMap(TileOcclusionMap map)
        {
            var window = GetWindow<TileOcclusionWindow>("Tile 遮挡分组");
            window._map = map;
            window._target = map.gameObject;
            window._regionIndex = 0;
            window._tool = 0;
            window.Repaint();
        }

        private void OnEnable()
        {
            minSize = new Vector2(380, 500);
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            if (_target == null) UseSelection();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_dragging) GUIUtility.hotControl = 0;
            _dragging = false;
        }

        private void OnUndoRedo() { Repaint(); SceneView.RepaintAll(); }

        private void UseSelection()
        {
            _target = Selection.activeGameObject;
            _map = _target != null ? _target.GetComponentInParent<TileOcclusionMap>() : null;
            if (_map != null) _target = _map.gameObject;
            _regionIndex = 0;
            _tool = 0;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("1 选择原始地图根节点 → 2 勾选障碍图层 → 3 新建组并框选 → 4 点落地点 → 5 生成。\n不拆 PNG，不改源图块/碰撞。生成物仅用于普通 GameObject 场景。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("自动处理 Project 当前选中的 TMX（生成独立副本）"))
                    Run(TileOcclusionTmxWorkflow.ProcessSelection);
                EditorGUI.BeginChangeCheck();
                GameObject target = (GameObject)EditorGUILayout.ObjectField("地图根节点", _target, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    _target = target;
                    _map = target != null ? target.GetComponent<TileOcclusionMap>() : null;
                    _regionIndex = 0;
                    _tool = 0;
                }
                if (GUILayout.Button("使用 Hierarchy 当前选择")) UseSelection();
                if (_target == null) return;
                if (EditorUtility.IsPersistent(_target))
                {
                    EditorGUILayout.HelpBox("请把原始地图放入普通编辑场景，或打开 Prefab 编辑模式。不能直接编辑 Project 中的资产。", MessageType.Warning);
                    return;
                }
                if (_map == null)
                {
                    if (GUILayout.Button("为此地图创建遮挡标注")) Run(() =>
                    {
                        if (_target.GetComponentsInChildren<Tilemap>(true).Length == 0)
                            throw new InvalidOperationException("此节点下没有原始 Tilemap。整张烘焙图片/Mesh 不能在此工具中选格。");
                        if (_target.scene.isSubScene)
                            throw new InvalidOperationException("请先在普通场景中放置原始 Tilemap；首版不支持 DOTS SubScene。");
                        _map = Undo.AddComponent<TileOcclusionMap>(_target);
                        TileOcclusionBuilder.SyncLayers(_map);
                    });
                    DrawMessage();
                    return;
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                if (GUILayout.Button("按村庄素材自动分组（保留手工组）", GUILayout.Height(28)))
                    Run(() => _message = TileOcclusionAutoGrouper.Apply(_map));
                EditorGUILayout.HelpBox("遮挡分组与碰撞占地独立。先分组，再批量生成碰撞；房屋忽略屋顶，只按底层房框补齐断墙、闭合并填满内部。树干竖向两格、树桩主体一格、井底两格，其余障碍全格。", MessageType.None);
                if (GUILayout.Button("批量生成碰撞占地（保留手工碰撞）"))
                    Run(() => _message = TileCollisionBuilder.ApplyRules(_map));
                _showCollision = EditorGUILayout.Toggle("显示红色碰撞占地", _showCollision);
                if (_map.CollisionNeedsRebuild)
                    EditorGUILayout.HelpBox("碰撞标注有变化，已导出的物理/寻路副本尚未更新。请重新导出。", MessageType.Warning);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("碰撞与寻路 Prefab", _map.CollisionPrefab, typeof(GameObject), false);
                if (GUILayout.Button("导出碰撞 + 寻路配套 Prefab…")) Run(() =>
                {
                    string path = EditorUtility.SaveFilePanelInProject("新建碰撞输出目录", _map.name + "_Collision", "asset", "按所选名称创建新目录，已有输出不覆盖。");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var result = TileCollisionBuilder.Export(_map, System.IO.Path.ChangeExtension(path, null));
                        EditorGUIUtility.PingObject(result);
                        _message = "已导出物理/寻路副本。放入 SubScene，显示地图留在普通场景；两者根节点变换需一致。";
                    }
                });
                EditorGUI.BeginChangeCheck();
                var preset = (TileOcclusionPreset)EditorGUILayout.ObjectField("遮挡标注配置", _map.Preset, typeof(TileOcclusionPreset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_map, "Select occlusion preset");
                    _map.Preset = preset;
                    EditorUtility.SetDirty(_map);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(_map);
                }
                if (_map.Preset != null)
                {
                    EditorGUILayout.HelpBox(_map.Preset.Summary, MessageType.Info);
                    if (_map.Preset.SourceMap != null && _map.Preset.SourceDependencyHash !=
                        AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(_map.Preset.SourceMap)).ToString())
                        EditorGUILayout.HelpBox("源 TMX 或图集已改变。保存的显示副本不会自动更新；请从新导入的 TMX 重新分组，避免遗漏新图块。", MessageType.Warning);
                    if (GUILayout.Button("载入配置中的标注（替换当前标注）")) Run(() => TileOcclusionTmxWorkflow.LoadPreset(_map, _map.Preset));
                }
                if (GUILayout.Button("另存当前标注配置…")) Run(() =>
                {
                    string path = EditorUtility.SaveFilePanelInProject("另存遮挡标注", _map.name + "_OcclusionPreset", "asset", "标注保存独立于 TMX，选择新文件。");
                    if (!string.IsNullOrEmpty(path))
                        TileOcclusionTmxWorkflow.SavePreset(_map, _map.Preset != null ? _map.Preset.SourceMap :
                            PrefabUtility.GetCorrespondingObjectFromOriginalSource(_map.gameObject), path, $"手工保存 {_map.Regions.Count} 组");
                });
                if (GUILayout.Button("刷新源图层列表")) Run(() => TileOcclusionBuilder.SyncLayers(_map));
                EditorGUILayout.LabelField("框选仅作用于勾选的图层", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("未分组图块默认放在地面层。确实需要始终遮挡的图层可选 Foreground。树/房子混在地面图层时，请按格移除误选地面。", MessageType.None);
                foreach (TileOcclusionLayer layer in _map.Layers.ToArray())
                {
                    if (layer == null) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    bool selectable = EditorGUILayout.Toggle(layer.Selectable, GUILayout.Width(20));
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(layer.Source, typeof(Tilemap), true);
                    var mode = (TileOcclusionRemainderMode)EditorGUILayout.EnumPopup(layer.RemainderMode, GUILayout.Width(95));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_map, "Edit occlusion layer");
                        layer.Selectable = selectable;
                        layer.RemainderMode = mode;
                        TileOcclusionBuilder.MarkChanged(_map);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                SortingLayer[] sortingLayers = SortingLayer.layers;
                int sortingIndex = Array.FindIndex(sortingLayers, l => l.id == _map.SortingLayerId);
                EditorGUI.BeginChangeCheck();
                int chosen = EditorGUILayout.Popup("与单位一致的 Sorting Layer", Mathf.Max(0, sortingIndex), sortingLayers.Select(l => l.name).ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_map, "Edit occlusion sorting layer");
                    _map.SortingLayerId = sortingLayers[chosen].id;
                    TileOcclusionBuilder.MarkChanged(_map);
                }

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("新建遮挡组"))
                {
                    Undo.RecordObject(_map, "Add occlusion group");
                    _map.Regions.Add(new TileOcclusionRegion { Name = $"障碍 {_map.Regions.Count + 1}" });
                    _regionIndex = _map.Regions.Count - 1;
                    _tool = 1;
                    TileOcclusionBuilder.MarkChanged(_map);
                }
                using (new EditorGUI.DisabledScope(_map.Regions.Count == 0))
                    if (GUILayout.Button("删除当前组"))
                    {
                        Undo.RecordObject(_map, "Remove occlusion group");
                        _map.Regions.RemoveAt(Mathf.Clamp(_regionIndex, 0, _map.Regions.Count - 1));
                        _regionIndex = Mathf.Max(0, _regionIndex - 1);
                        TileOcclusionBuilder.MarkChanged(_map);
                    }
                EditorGUILayout.EndHorizontal();
                if (_map.Regions.Count > 0)
                {
                    if (_map.Regions.Any(r => !string.IsNullOrEmpty(r.ReviewNote) || !string.IsNullOrEmpty(r.CollisionReviewNote)) && GUILayout.Button("定位下一个待复核组"))
                    {
                        for (int step = 1; step <= _map.Regions.Count; step++)
                        {
                            int index = (_regionIndex + step) % _map.Regions.Count;
                            if (string.IsNullOrEmpty(_map.Regions[index].ReviewNote) && string.IsNullOrEmpty(_map.Regions[index].CollisionReviewNote)) continue;
                            _regionIndex = index;
                            SceneView.lastActiveSceneView?.Frame(new Bounds(_map.transform.TransformPoint(_map.Regions[index].LocalAnchor), Vector3.one * 6), false);
                            break;
                        }
                    }
                    _regionIndex = EditorGUILayout.Popup("当前组", Mathf.Clamp(_regionIndex, 0, _map.Regions.Count - 1),
                        _map.Regions.Select(r => $"{r.Name} ({r.Cells.Count} 格)").ToArray());
                    TileOcclusionRegion region = _map.Regions[_regionIndex];
                    if (!string.IsNullOrEmpty(region.ReviewNote)) EditorGUILayout.HelpBox(region.ReviewNote, MessageType.Warning);
                    if (!string.IsNullOrEmpty(region.CollisionReviewNote)) EditorGUILayout.HelpBox(region.CollisionReviewNote, MessageType.Warning);
                    EditorGUILayout.LabelField($"碰撞：{region.CollisionMode} / {region.CollisionCells.Count} 格");
                    if (GUILayout.Button("当前组设为无碰撞"))
                    {
                        Undo.RecordObject(_map, "Disable region collision");
                        region.CollisionMode = TileCollisionMode.None;
                        region.CollisionCells.Clear();
                        region.CollisionReviewNote = "";
                        region.AutoGenerated = false;
                        TileCollisionBuilder.MarkChanged(_map);
                    }
                    EditorGUI.BeginChangeCheck();
                    string name = EditorGUILayout.TextField("名称", region.Name);
                    Vector3 anchor = EditorGUILayout.Vector3Field("落地点（地图局部坐标）", region.LocalAnchor);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_map, "Edit occlusion group");
                        region.Name = name;
                        region.LocalAnchor = anchor;
                        region.AutoGenerated = false;
                        TileOcclusionBuilder.MarkChanged(_map);
                        SceneView.RepaintAll();
                    }
                    EditorGUI.BeginChangeCheck();
                    int visualTool = GUILayout.Toolbar(_tool < 4 ? _tool : -1, new[] { "浏览", "框选添加", "框选移除", "点落地点" });
                    if (EditorGUI.EndChangeCheck()) _tool = visualTool;
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Toggle(_tool == 4, "画碰撞格", "Button")) _tool = 4;
                    if (GUILayout.Toggle(_tool == 5, "擦碰撞格", "Button")) _tool = 5;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.LabelField("在 Scene 视图左键拖动/点击；Alt 保留视图操作，Esc 取消。", EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("定位当前组"))
                    {
                        Vector3 world = _map.transform.TransformPoint(region.LocalAnchor);
                        SceneView.lastActiveSceneView?.Frame(new Bounds(world, Vector3.one * 8), false);
                    }
                }
                if (_map.NeedsRebuild)
                    EditorGUILayout.HelpBox("标注已修改，当前显示还是上次生成结果。请重新生成。", MessageType.Warning);
                if (_map.IsGenerated)
                {
                    EditorGUILayout.HelpBox("原 Tilemap Renderer 暂时隐藏，原数据与碰撞仍在。绘制源地图前请先恢复；不要手动修改 __TileOcclusion_Generated。", MessageType.Info);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("显示快照资产", _map.SnapshotAsset, typeof(Tile), false);
                }
                EditorGUILayout.HelpBox("静态图块的当前外观会保存到 Res/Tile/OcclusionSnapshots。重建创建新快照，旧快照不自动删除，以保留撤销和已导出 Prefab 的引用。", MessageType.None);
                EditorGUILayout.Space();
                if (GUILayout.Button("生成 / 重建遮挡显示", GUILayout.Height(30))) Run(() =>
                {
                    TileOcclusionBuilder.Generate(_map);
                    _message = $"已生成 {_map.Regions.Count} 组。可 Ctrl+Z 撤销，或恢复原显示。";
                });
                using (new EditorGUI.DisabledScope(!_map.IsGenerated))
                    if (GUILayout.Button("恢复原 Tilemap 显示")) Run(() => TileOcclusionBuilder.Restore(_map));
                if (GUILayout.Button("另存为可运行 Prefab…")) Run(ExportPrefab);
                EditorGUILayout.EndScrollView();
            }
            DrawMessage();
        }

        private void ExportPrefab()
        {
            TileOcclusionBuilder.Validate(_map);
            string path = EditorUtility.SaveFilePanelInProject("保存遮挡地图 Prefab", _map.name + "_Occlusion", "prefab",
                "保存新 Prefab，不会修改源 TMX。运行时在普通场景中使用，不要放进 DOTS SubScene。", "Assets/Res/Tile");
            if (string.IsNullOrEmpty(path)) return;
            if (path == PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_map.gameObject))
                throw new InvalidOperationException("请选择不同于原始 Prefab 的新路径。");
            TileOcclusionBuilder.Generate(_map);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(_map.gameObject, path);
            if (prefab == null) throw new InvalidOperationException("Prefab 保存失败，请检查目标路径。");
            _message = "已保存 " + path;
            EditorGUIUtility.PingObject(prefab);
        }

        private void DrawMessage()
        {
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception exception) { _message = exception.Message; Debug.LogWarning("[TileOcclusion] " + exception.Message); }
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView view)
        {
            if (_map == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (_map.Regions.Count == 0) return;
            _regionIndex = Mathf.Clamp(_regionIndex, 0, _map.Regions.Count - 1);
            TileOcclusionRegion region = _map.Regions[_regionIndex];
            DrawRegions();
            if (_tool == 0) return;
            Event e = Event.current;
            int control = GUIUtility.GetControlID(GetType().GetHashCode(), FocusType.Passive);
            if (e.type == EventType.Layout && !e.alt) HandleUtility.AddDefaultControl(control);
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _dragging = false;
                GUIUtility.hotControl = 0;
                _tool = 0;
                e.Use(); Repaint(); return;
            }
            if (e.alt) return;
            if (e.type == EventType.MouseDown && e.button == 0 && TryWorld(e.mousePosition, out Vector3 start))
            {
                GUIUtility.hotControl = control;
                _dragStart = _dragEnd = start;
                _dragging = true;
                e.Use();
            }
            if (_dragging && e.type == EventType.MouseDrag && TryWorld(e.mousePosition, out Vector3 current))
            {
                _dragEnd = current; view.Repaint(); e.Use();
            }
            if (_dragging && e.type == EventType.MouseUp && e.button == 0)
            {
                if (TryWorld(e.mousePosition, out Vector3 end)) _dragEnd = end;
                _dragging = false;
                GUIUtility.hotControl = 0;
                Run(() => ApplyGesture(region));
                e.Use();
            }
            if (_dragging && _tool != 3)
            {
                Handles.color = _tool == 1 ? Color.green : Color.red;
                Handles.DrawWireCube((_dragStart + _dragEnd) * 0.5f,
                    new Vector3(Mathf.Abs(_dragEnd.x - _dragStart.x), Mathf.Abs(_dragEnd.y - _dragStart.y), 0));
            }
        }

        private void ApplyGesture(TileOcclusionRegion region)
        {
            if (_tool == 4 || _tool == 5)
            {
                TileCollisionBuilder.Paint(_map, region, _dragStart, _dragEnd, _tool == 4);
                _message = $"当前组碰撞 {region.CollisionCells.Count} 格；遮挡范围未改变。请重新导出碰撞副本。";
                return;
            }
            if (_tool == 3)
            {
                Undo.RecordObject(_map, "Set occlusion foot anchor");
                region.LocalAnchor = _map.transform.InverseTransformPoint(_dragEnd);
                region.AutoGenerated = false;
                TileOcclusionBuilder.MarkChanged(_map);
                return;
            }
            var cells = TileOcclusionBuilder.CollectRectangle(_map, _dragStart, _dragEnd);
            if (_tool == 1)
            {
                bool wasEmpty = region.Cells.Count == 0;
                int conflicts = TileOcclusionBuilder.AddCells(_map, _regionIndex, cells);
                if (wasEmpty && region.Cells.Count > 0)
                {
                    // Convenient initial line only; the user can click the actual root.
                    float x = region.Cells.Average(c => c.Source.GetCellCenterWorld(c.Position).x);
                    float y = region.Cells.Min(c => c.Source.CellToWorld(c.Position).y);
                    region.LocalAnchor = _map.transform.InverseTransformPoint(new Vector3(x, y, _map.transform.position.z));
                    TileOcclusionBuilder.MarkChanged(_map);
                }
                _message = $"当前 {region.Cells.Count} 格；跳过 {conflicts} 个已属于其他组的图块。";
            }
            else if (_tool == 2)
            {
                Undo.RecordObject(_map, "Remove occlusion cells");
                var selected = new System.Collections.Generic.HashSet<TileOcclusionCell>(cells);
                region.Cells.RemoveAll(selected.Contains);
                region.AutoGenerated = false;
                TileOcclusionBuilder.MarkChanged(_map);
            }
        }

        private bool TryWorld(Vector2 mouse, out Vector3 world)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            var plane = new Plane(Vector3.forward, _map.transform.position);
            if (plane.Raycast(ray, out float distance)) { world = ray.GetPoint(distance); return true; }
            world = default;
            return false;
        }

        private void DrawRegions()
        {
            for (int i = 0; i < _map.Regions.Count; i++)
            {
                TileOcclusionRegion region = _map.Regions[i];
                Color color = i == _regionIndex ? Color.cyan : new Color(1f, 0.75f, 0.15f, 0.65f);
                Handles.color = color;
                foreach (TileOcclusionCell cell in region.Cells.Take(2000))
                {
                    if (cell.Source == null) continue;
                    Vector3Int p = cell.Position;
                    Vector3[] corners = { cell.Source.CellToWorld(p), cell.Source.CellToWorld(p + Vector3Int.right),
                        cell.Source.CellToWorld(p + Vector3Int.right + Vector3Int.up), cell.Source.CellToWorld(p + Vector3Int.up) };
                    Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, 0.08f), color);
                }
                Vector3 anchor = _map.transform.TransformPoint(region.LocalAnchor);
                float size = HandleUtility.GetHandleSize(anchor) * 0.12f;
                Handles.DrawLine(anchor - Vector3.right * size * 3, anchor + Vector3.right * size * 3);
                Handles.DrawWireDisc(anchor, Vector3.forward, size);
                Handles.Label(anchor + Vector3.up * size, region.Name + " / 落地点");
            }
            if (_showCollision)
            {
                Tilemap grid = TileCollisionBuilder.ReferenceGrid(_map);
                foreach (Vector3Int p in _map.Regions.Where(r => r.CollisionMode != TileCollisionMode.None).SelectMany(r => r.CollisionCells).Distinct())
                {
                    Vector3[] corners = { grid.CellToWorld(p), grid.CellToWorld(p + Vector3Int.right),
                        grid.CellToWorld(p + Vector3Int.right + Vector3Int.up), grid.CellToWorld(p + Vector3Int.up) };
                    Handles.DrawSolidRectangleWithOutline(corners, new Color(1, 0, 0, 0.3f), Color.red);
                }
            }
        }
    }
}
