using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrystalMagic.Game.Testing.Editor
{
    public sealed partial class NavigationTestWindow : EditorWindow
    {
        public const string ScenePath = "Assets/Scenes/NavigationTest.unity";
        [SerializeField] private NavigationTestScenario scenario;
        [SerializeField] private int selected;
        [SerializeField] private bool showGrid = true, showPath = true, showVelocity = true;
        [SerializeField] private float playbackSpeed = 1;
        [SerializeField] private bool showSettings, showDisplayOptions;
        private NavigationTestSimulation simulation;
        private readonly List<Vector3> path = new();
        private Vector2 scroll;
        private bool running;
        private double previousTime, accumulator;
        private string error;

        [MenuItem("Tools/Crystal Magic/Navigation/ORCA & A* Test")]
        public static void Open() => GetWindow<NavigationTestWindow>("ORCA / A* 测试");

        public static void OpenFor(NavigationTestScenario value)
        {
            var window = GetWindow<NavigationTestWindow>("ORCA / A* 测试");
            if (window.scenario != value) window.ResetSimulation();
            window.scenario = value;
            window.FocusMap();
        }

        internal static bool IsPreviewing(NavigationTestScenario value)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<NavigationTestWindow>())
                if (window.scenario == value && window.simulation != null) return true;
            return false;
        }

        private void OnEnable()
        {
            minSize = new Vector2(450, 520);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.quitting += ResetSimulation;
            AssemblyReloadEvents.beforeAssemblyReload += ResetSimulation;
            EditorSceneManager.sceneClosing += SceneClosing;
            Undo.undoRedoPerformed += ResetSimulation;
            SceneView.duringSceneGui += DrawScene;
            FindScenario();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.quitting -= ResetSimulation;
            AssemblyReloadEvents.beforeAssemblyReload -= ResetSimulation;
            EditorSceneManager.sceneClosing -= SceneClosing;
            Undo.undoRedoPerformed -= ResetSimulation;
            SceneView.duringSceneGui -= DrawScene;
            ResetSimulation();
        }

        private void PlayModeChanged(PlayModeStateChange _) => ResetSimulation();
        private void SceneClosing(Scene closing, bool removing)
        {
            if (scenario != null && scenario.gameObject.scene == closing) ResetSimulation();
        }

        private void FindScenario()
        {
            if (scenario == null)
                scenario = UnityEngine.Object.FindFirstObjectByType<NavigationTestScenario>();
        }

        private void OnGUI()
        {
            FindScenario();
            EditorGUILayout.HelpBox("编辑模式的 Scene 测试，无需 Unity Play。点击圆形单位查看实时信息和 ORCA 约束；复位后可拖动起点/目标。可暂停或单步检查同一帧。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("打开测试场景"))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        ResetSimulation();
                        EditorSceneManager.OpenScene(ScenePath);
                        scenario = null; FindScenario(); FocusMap();
                    }
                }
                if (GUILayout.Button("聚焦地图")) FocusMap();
                EditorGUILayout.EndHorizontal();
                if (scenario == null) return;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(running ? "暂停" : simulation == null ? "开始（全部出发）" : "继续"))
                {
                    if (running) running = false;
                    else if (Prepare()) { running = true; previousTime = EditorApplication.timeSinceStartup; accumulator = 0; }
                }
                using (new EditorGUI.DisabledScope(running))
                    if (GUILayout.Button("单步")) { if (Prepare()) Advance(); }
                if (GUILayout.Button("复位")) ResetSimulation();
                EditorGUILayout.EndHorizontal();
                playbackSpeed = EditorGUILayout.Slider("播放速度", playbackSpeed, 0.1f, 4f);
                showDisplayOptions = EditorGUILayout.Foldout(showDisplayOptions, "调试显示选项", true);
                if (showDisplayOptions) DrawDisplayOptions();
                if (simulation != null)
                {
                    int arrived = 0;
                    for (int i = 0; i < simulation.Count; i++) if (simulation.Snapshot(i).Arrived) arrived++;
                    EditorGUILayout.LabelField($"模拟时间 {simulation.Elapsed:F2}s　到达 {arrived}/{simulation.Count}");
                    EditorGUILayout.HelpBox("复位后可以修改布局和参数。达到 120 秒会自动暂停，避免无人看管时持续运行。", MessageType.None);
                }
                if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);

                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawSelectedInformation();
                showSettings = EditorGUILayout.Foldout(showSettings, "地图、障碍和寻路参数（复位后可编辑）", true);
                if (showSettings)
                    using (new EditorGUI.DisabledScope(simulation != null)) DrawSettings();
                DrawUnitSelection();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSettings()
        {
            if (GUILayout.Button("恢复默认测试布局…") && EditorUtility.DisplayDialog("恢复默认测试布局",
                    "将当前测试配置恢复为 32×24 地图、8 个单位和两块 2×7 障碍。当前自定义单位、目标点、障碍和参数会被替换，可用 Ctrl+Z 撤销。", "恢复", "取消"))
            {
                Undo.RecordObject(scenario, "恢复默认寻路测试布局");
                scenario.Settings = NavigationTestSettings.CreateDefault();
                selected = 0;
                error = null;
                Changed();
                FocusMap();
            }
            var serialized = new SerializedObject(scenario);
            serialized.Update();
            var settings = serialized.FindProperty("Settings");
            Draw(settings, "UseAStar", "启用 A*（关闭后直线追踪，障碍碰撞仍保留）");
            Draw(settings, "UseOrca", "启用 ORCA（关闭后仍有物理碰撞）");
            Draw(settings, "Width", "地图宽度（格）");
            Draw(settings, "Height", "地图高度（格）");
            Draw(settings, "CellSize", "每格世界尺寸");
            Draw(settings, "FixedDeltaTime", "固定时间步长");
            Draw(settings, "StopDistance", "到达距离");
            Draw(settings, "NeighborDistance", "ORCA 邻居搜索距离");
            Draw(settings, "MaxNeighbors", "ORCA 最多邻居数");
            Draw(settings, "TimeHorizon", "ORCA 预测时间");
            Draw(settings, "RadiusPadding", "ORCA 额外间距");
            Draw(settings, "Obstacles", "障碍矩形（左下角格坐标 X/Y、宽/高）");
            if (serialized.ApplyModifiedProperties()) SceneView.RepaintAll();
        }

        private static void Draw(SerializedProperty parent, string field, string label) =>
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(field), new GUIContent(label), true);

        private void DrawUnitSelection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("单位与各自目标点", EditorStyles.boldLabel);
            var list = scenario.Settings.Agents;
            if (list == null) return;
            using (new EditorGUI.DisabledScope(simulation != null || list.Count >= 256))
            {
                if (GUILayout.Button("＋ 添加单位"))
                {
                    Undo.RecordObject(scenario, "添加寻路测试单位");
                    list.Add(new NavigationTestAgent { Name = "Unit " + (list.Count + 1), Start = new Vector2(-8.5f, 0.5f) });
                    selected = list.Count - 1;
                    Changed();
                }
            }
            int count = simulation?.Count ?? list.Count;
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, count - 1));
            for (int i = 0; i < count; i++)
            {
                var agent = simulation != null ? simulation.GetAgent(i) : list[i];
                if (agent == null) continue;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selected == i, $"{i + 1}. {agent.Name}", "Button") && selected != i) SelectAgent(i);
                if (simulation != null)
                {
                    var snapshot = simulation.Snapshot(i);
                    GUILayout.Label(snapshot.Arrived ? "已到达" : !snapshot.HasDestination ? "待出发" :
                        simulation.UsesAStar && !snapshot.PathFound && simulation.Elapsed > 0 ? "无路径" : "移动中", GUILayout.Width(64));
                }
                using (new EditorGUI.DisabledScope(simulation != null))
                    if (GUILayout.Button("删除", GUILayout.Width(46)))
                    {
                        Undo.RecordObject(scenario, "删除寻路测试单位"); list.RemoveAt(i); Changed();
                        EditorGUILayout.EndHorizontal(); break;
                    }
                EditorGUILayout.EndHorizontal();
            }
            if (selected >= list.Count || list.Count == 0) return;
            using (new EditorGUI.DisabledScope(simulation != null))
            {
                var serialized = new SerializedObject(scenario); serialized.Update();
                var agent = serialized.FindProperty("Settings.Agents").GetArrayElementAtIndex(selected);
                Draw(agent, "Name", "名称"); Draw(agent, "Start", "起点（世界坐标）");
                Draw(agent, "Destination", "目标点（世界坐标）"); Draw(agent, "Radius", "碰撞半径");
                Draw(agent, "Speed", "移动速度"); Draw(agent, "Acceleration", "最大加速度"); Draw(agent, "Color", "颜色");
                if (serialized.ApplyModifiedProperties()) SceneView.RepaintAll();
            }
        }

        private void Changed()
        {
            EditorUtility.SetDirty(scenario);
            EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);
            SceneView.RepaintAll(); Repaint();
        }

        private bool Prepare()
        {
            try
            {
                if (simulation == null) simulation = new NavigationTestSimulation(scenario.Settings);
                simulation.Start(); error = null; return true;
            }
            catch (Exception exception) { Fail(exception); return false; }
        }

        private void Advance()
        {
            try { simulation.Step(); }
            catch (Exception exception) { Fail(exception); }
            SceneView.RepaintAll(); Repaint();
        }

        private void Fail(Exception exception)
        {
            ResetSimulation(); error = exception.Message;
            showSettings = true;
            Debug.LogException(exception);
        }

        private void Tick()
        {
            if (scenario == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (simulation != null) ResetSimulation();
                return;
            }
            if (!running || simulation == null) return;
            double now = EditorApplication.timeSinceStartup;
            accumulator += Math.Min(0.1, now - previousTime) * playbackSpeed;
            previousTime = now;
            double step = simulation.TimeStep;
            int steps = 0;
            while (running && simulation != null && accumulator >= step && steps++ < 8)
            {
                accumulator -= step; Advance();
                if (simulation != null)
                {
                    bool allArrived = true;
                    for (int i = 0; i < simulation.Count; i++) allArrived &= simulation.Snapshot(i).Arrived;
                    if (allArrived || simulation.Elapsed >= 120) running = false;
                }
            }
            accumulator = Math.Min(accumulator, step * 8);
        }

        private void ResetSimulation()
        {
            running = false; accumulator = 0; error = null;
            var previous = simulation; simulation = null;
            try { previous?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            SceneView.RepaintAll(); Repaint();
        }

        private void FocusMap()
        {
            if (scenario == null) return;
            var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            view.in2DMode = true;
            view.LookAt(Vector3.zero, Quaternion.identity,
                Mathf.Max(scenario.Settings.Width, scenario.Settings.Height) * scenario.Settings.CellSize * 0.6f, true);
            Selection.activeGameObject = scenario.gameObject;
        }

        private void DrawScene(SceneView view)
        {
            if (scenario == null || scenario.Settings == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            using var drawingScope = new Handles.DrawingScope(Handles.color);
            var data = scenario.Settings;
            float w = data.Width * data.CellSize * 0.5f, h = data.Height * data.CellSize * 0.5f;
            if (showGrid && data.Width <= 128 && data.Height <= 128 && data.CellSize > 0)
            {
                Handles.color = new Color(0.4f, 0.5f, 0.6f, 0.18f);
                for (int x = 0; x <= data.Width; x++) Handles.DrawLine(new Vector3(-w + x * data.CellSize, -h), new Vector3(-w + x * data.CellSize, h));
                for (int y = 0; y <= data.Height; y++) Handles.DrawLine(new Vector3(-w, -h + y * data.CellSize), new Vector3(w, -h + y * data.CellSize));
            }
            int count = simulation?.Count ?? data.Agents?.Count ?? 0;
            bool pointerOverInfo = showSceneInfo && SceneInfoRect(view).Contains(Event.current.mousePosition);
            for (int i = 0; i < count; i++)
            {
                var agent = simulation != null ? simulation.GetAgent(i) : data.Agents[i];
                if (agent == null) continue;
                var snapshot = simulation != null ? simulation.Snapshot(i, showPath && selected == i ? path : null) :
                    new NavigationTestSnapshot { Position = agent.Start };
                Handles.color = agent.Color;
                if (!pointerOverInfo && Handles.Button(snapshot.Position, Quaternion.identity, agent.Radius, agent.Radius, Handles.CircleHandleCap))
                    SelectAgent(i);
                Handles.DrawSolidDisc(snapshot.Position, Vector3.forward, agent.Radius * 0.9f);
                if (selected == i)
                {
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(snapshot.Position, Vector3.forward, agent.Radius * 1.2f, 3);
                    Handles.color = agent.Color;
                }
                Handles.Label(snapshot.Position + Vector3.up * (agent.Radius + 0.15f), agent.Name);
                Vector3 target = agent.Destination;
                Handles.DrawWireDisc(target, Vector3.forward, agent.Radius);
                Handles.DrawLine(target + Vector3.left * 0.2f, target + Vector3.right * 0.2f);
                Handles.DrawLine(target + Vector3.up * 0.2f, target + Vector3.down * 0.2f);
                if (selected == i) Handles.DrawDottedLine(snapshot.Position, target, 5);
                if (showVelocity && simulation != null) DrawArrow(snapshot.Position, snapshot.Position + snapshot.PhysicalVelocity * 0.35f, agent.Color);
                if (showPath && selected == i && simulation != null)
                {
                    Handles.color = Color.yellow;
                    Vector3 previous = snapshot.Position;
                    foreach (var point in path) { Handles.DrawLine(previous, point); previous = point; }
                }
            }
            DrawSelectedSceneDebug(view);
            if (simulation == null && data.Agents != null && selected >= 0 && selected < data.Agents.Count)
            {
                var agent = data.Agents[selected];
                if (agent == null) return;
                using (new EditorGUI.DisabledScope(pointerOverInfo))
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 start = Handles.PositionHandle(agent.Start, Quaternion.identity);
                    Vector3 destination = Handles.PositionHandle(agent.Destination, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(scenario, "移动单位起点或目标");
                        agent.Start = start; agent.Destination = destination; Changed();
                    }
                }
            }
        }
    }

    [CustomEditor(typeof(NavigationTestScenario))]
    public sealed class NavigationTestScenarioInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var scenario = (NavigationTestScenario)target;
            if (GUILayout.Button("打开 ORCA / A* 测试控制面板")) NavigationTestWindow.OpenFor(scenario);
            EditorGUILayout.HelpBox("此组件只保存测试配置，不会自行启动。单位位置在 XY 平面，修改完成后保存场景即可保留布局。", MessageType.Info);
            using (new EditorGUI.DisabledScope(NavigationTestWindow.IsPreviewing(scenario))) DrawDefaultInspector();
        }
    }
}
