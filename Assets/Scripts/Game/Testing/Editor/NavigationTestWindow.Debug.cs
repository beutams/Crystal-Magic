using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Game.Testing.Editor
{
    public sealed partial class NavigationTestWindow
    {
        [SerializeField] private bool showSceneInfo = true, showNeighbors = true, showConstraints = true;
        [SerializeField] private float velocityScale = 0.6f;
        [SerializeField] private bool showConstraintValues;
        private readonly List<UnitAvoidanceDebugConstraint> constraints = new();
        private readonly List<Vector2> feasiblePolygon = new(), clippedPolygon = new();
        private static readonly Color PhysicalColor = new(0.1f, 0.85f, 1f);
        private static readonly Color TargetColor = new(1f, 0.8f, 0.15f);
        private static readonly Color PreferredColor = new(0.2f, 1f, 0.4f);
        private static readonly Color ResolvedColor = new(1f, 0.25f, 0.8f);

        private void SelectAgent(int index)
        {
            selected = index;
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawDisplayOptions()
        {
            EditorGUI.BeginChangeCheck();
            showGrid = EditorGUILayout.Toggle("网格", showGrid);
            showPath = EditorGUILayout.Toggle("所选单位 A* 路径", showPath);
            showVelocity = EditorGUILayout.Toggle("所有单位实际速度箭头", showVelocity);
            showSceneInfo = EditorGUILayout.Toggle("Scene 信息面板", showSceneInfo);
            showNeighbors = EditorGUILayout.Toggle("所选单位邻居 / 搜索范围", showNeighbors);
            showConstraints = EditorGUILayout.Toggle("所选单位速度空间 / 半平面", showConstraints);
            velocityScale = EditorGUILayout.Slider("速度图缩放（世界单位 / 速度）", velocityScale, 0.1f, 2);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        }

        private bool TryGetSelected(out NavigationTestAgent agent, out NavigationTestSnapshot snapshot)
        {
            agent = null; snapshot = default;
            int count = simulation?.Count ?? scenario?.Settings?.Agents?.Count ?? 0;
            if (selected < 0 || selected >= count) return false;
            agent = simulation != null ? simulation.GetAgent(selected) : scenario.Settings.Agents[selected];
            if (agent == null) return false;
            snapshot = simulation != null ? simulation.Snapshot(selected) : new NavigationTestSnapshot
            {
                Position = agent.Start, Destination = agent.Destination, StopDistance = scenario.Settings.StopDistance,
            };
            return true;
        }

        private void DrawSelectedInformation()
        {
            if (!TryGetSelected(out var agent, out var snapshot)) return;
            EditorGUILayout.LabelField($"所选单位 #{selected + 1}：{agent.Name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("状态", Status(snapshot));
            EditorGUILayout.LabelField("当前位置", XY(snapshot.Position));
            EditorGUILayout.LabelField("最终目标", XY(snapshot.Destination));
            EditorGUILayout.LabelField("剩余直线距离 / 到达距离",
                $"{Vector2.Distance(snapshot.Position, snapshot.Destination):F3} / {snapshot.StopDistance:F3}");
            EditorGUILayout.LabelField("下一路径点", snapshot.HasWaypoint ? XY(snapshot.Waypoint) : "—");
            EditorGUILayout.LabelField("路径进度（当前 / 总点数）", snapshot.PathLength > 0 ?
                $"{Mathf.Min(snapshot.WaypointIndex + 1, snapshot.PathLength)} / {snapshot.PathLength}" : "—");
            EditorGUILayout.LabelField("碰撞半径 / 配置最大速度", $"{agent.Radius:F3} / {agent.Speed:F3}");
            if (simulation == null)
            {
                EditorGUILayout.HelpBox("点击开始或单步后显示速度、邻居及半平面数据。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("实际速度（物理碰撞后）", Velocity(snapshot.PhysicalVelocity));
            EditorGUILayout.LabelField("本步位移 / dt", Velocity(snapshot.MeasuredVelocity));
            EditorGUILayout.LabelField("导航目标速度（加速前）", Velocity(snapshot.TargetVelocity));
            EditorGUILayout.LabelField("移动执行速度（碰撞前）", Velocity(snapshot.Velocity));
            var debug = snapshot.Avoidance;
            if (debug.HasSample == 0)
            {
                EditorGUILayout.HelpBox(simulation.UsesOrca ? "等待首个 ORCA 采样。" : "ORCA 已关闭：没有半平面约束，仍可查看路径和物理速度。", MessageType.None);
                return;
            }
            EditorGUILayout.LabelField("ORCA 输入速度（上一步命令）", Velocity(V(debug.InputVelocity)));
            EditorGUILayout.LabelField("期望速度（加速后 / 避障前）", Velocity(V(debug.PreferredVelocity)));
            EditorGUILayout.LabelField("ORCA 求解速度", Velocity(V(debug.ResolvedVelocity)));
            EditorGUILayout.LabelField("避障半径 / 邻居搜索距离", $"{debug.Radius:F3} / {debug.NeighborDistance:F3}");
            EditorGUILayout.LabelField("预测时间 / 求解最大速度", $"{debug.TimeHorizon:F3}s / {debug.MaxSpeed:F3}");
            EditorGUILayout.LabelField("邻居 / 半平面数量", debug.ConstraintCount.ToString());
            EditorGUILayout.LabelField("求解状态", debug.UsedFallback ? $"LP3 回退（首个失败约束 #{debug.FirstFailedLine + 1}）" : "LP2 正常求解");
            EditorGUILayout.HelpBox("以下数据来自最近一个固定步的真实 ORCA 求解。箭头朝向允许半平面；绿色填充为最大速度圆内的可行区域近似。墙体不生成 ORCA 线，由 A* / 物理碰撞处理。", MessageType.None);

            showConstraintValues = EditorGUILayout.Foldout(showConstraintValues, "各邻居与约束数值", true);
            if (!showConstraintValues) return;
            simulation.CopyAvoidanceConstraints(selected, constraints);
            for (int i = 0; i < constraints.Count; i++)
            {
                var line = constraints[i];
                float margin = line.SignedMargin(debug.ResolvedVelocity);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"线 {i + 1} ← {NeighborName(line)}   距离 {line.Distance:F3}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("采样位置 / 避障半径", $"{XY(V(line.NeighborPosition))} / {line.NeighborRadius:F3}");
                    EditorGUILayout.LabelField("邻居输入速度", Velocity(V(line.NeighborVelocity)));
                    EditorGUILayout.LabelField("线上的点 p（速度坐标）", XY(V(line.Point)));
                    EditorGUILayout.LabelField("线方向 d / 允许侧法线 n", $"{XY(V(line.Direction))} / {XY(V(line.AllowedNormal))}");
                    EditorGUILayout.LabelField("n·(ORCA速度 − p) ≥ 0", $"{margin:F5}  {(margin >= -0.0001f ? "满足" : "未满足 / 回退折中")}");
                }
            }
        }

        private string Status(NavigationTestSnapshot snapshot) => simulation == null ? "待开始" :
            snapshot.Arrived ? "已到达" : !snapshot.HasDestination ? "无目标" :
            simulation.UsesAStar && !snapshot.PathFound && simulation.Elapsed > 0 ? "无可用路径" :
            running ? "运行中" : "已暂停";

        private static string XY(Vector3 value) => $"({value.x:F3}, {value.y:F3})";
        private static string Velocity(Vector3 value) => $"{XY(value)}  |v|={new Vector2(value.x, value.y).magnitude:F3}";
        private static Vector3 V(float2 value) => new(value.x, value.y, 0);
        private string NeighborName(UnitAvoidanceDebugConstraint line)
        {
            int index = simulation.GetAgentIndex(line.Neighbor);
            return index >= 0 ? $"#{index + 1} {simulation.GetAgent(index).Name}" : line.Neighbor.ToString();
        }

        private Color ConstraintColor(UnitAvoidanceDebugConstraint line)
        {
            int index = simulation.GetAgentIndex(line.Neighbor);
            return Color.HSVToRGB(Mathf.Repeat((index + 1) * 0.618034f, 1), 0.65f, 1);
        }

        private static Rect SceneInfoRect(SceneView view) => new(12, 12,
            Mathf.Min(420, Mathf.Max(180, view.position.width - 24)), Mathf.Min(302, Mathf.Max(120, view.position.height - 60)));

        private void DrawSelectedSceneDebug(SceneView view)
        {
            if (!TryGetSelected(out var agent, out var snapshot)) return;
            if (simulation != null && Event.current.type == EventType.Repaint)
            {
                simulation.CopyAvoidanceConstraints(selected, constraints);
                if (snapshot.HasWaypoint && showPath)
                {
                    Handles.color = TargetColor;
                    Handles.DrawWireDisc(snapshot.Waypoint, Vector3.forward, 0.2f, 2);
                    Handles.Label(snapshot.Waypoint + Vector3.up * 0.3f, "下一路径点");
                }
                if (showNeighbors && snapshot.Avoidance.HasSample != 0) DrawNeighborSamples(snapshot);
                if (showConstraints) DrawVelocitySpace(agent, snapshot);
            }
            if (!showSceneInfo) return;
            Handles.BeginGUI();
            GUILayout.BeginArea(SceneInfoRect(view), GUIContent.none, EditorStyles.helpBox);
            GUILayout.Label($"#{selected + 1} {agent.Name} · {Status(snapshot)} · t={simulation?.Elapsed ?? 0:F2}s", EditorStyles.boldLabel);
            GUILayout.Label($"位置 {XY(snapshot.Position)}   目标 {XY(snapshot.Destination)}");
            GUILayout.Label(snapshot.HasWaypoint ? $"下一路径点 {XY(snapshot.Waypoint)}" : "下一路径点 —");
            GUILayout.Label("青色 实际速度   " + Velocity(snapshot.PhysicalVelocity));
            GUILayout.Label("黄色 导航目标   " + Velocity(snapshot.TargetVelocity));
            var debug = snapshot.Avoidance;
            GUILayout.Label(debug.HasSample != 0 ? "绿色 加速后期望 " + Velocity(V(debug.PreferredVelocity)) : "期望 / ORCA：尚无采样或已关闭");
            GUILayout.Label(debug.HasSample != 0 ? "粉色 ORCA输出   " + Velocity(V(debug.ResolvedVelocity)) : "");
            GUILayout.Label($"邻居 / 半平面 {debug.ConstraintCount}   {(debug.UsedFallback ? "LP3 回退求解" : "")}");
            GUILayout.Label("旁图是速度空间（非地图障碍），白圆为最大速度。", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("线上的短箭头指向允许侧，邻居与对应约束线同色。", EditorStyles.wordWrappedMiniLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(running ? "暂停" : simulation == null ? "开始" : "继续"))
            {
                if (running) running = false;
                else if (Prepare()) { running = true; previousTime = EditorApplication.timeSinceStartup; accumulator = 0; }
                Repaint();
            }
            using (new EditorGUI.DisabledScope(running))
                if (GUILayout.Button("单步")) { if (Prepare()) Advance(); }
            if (GUILayout.Button("复位")) ResetSimulation();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void DrawNeighborSamples(NavigationTestSnapshot snapshot)
        {
            var debug = snapshot.Avoidance;
            Vector3 origin = V(debug.Position);
            Handles.color = new Color(0.7f, 0.8f, 1f, 0.35f);
            Handles.DrawWireDisc(origin, Vector3.forward, debug.NeighborDistance);
            Handles.DrawWireDisc(origin, Vector3.forward, debug.Radius, 2);
            for (int i = 0; i < constraints.Count; i++)
            {
                var line = constraints[i];
                Handles.color = ConstraintColor(line);
                Vector3 position = V(line.NeighborPosition);
                Handles.DrawDottedLine(origin, position, 4);
                Handles.DrawWireDisc(position, Vector3.forward, line.NeighborRadius, 2);
                Handles.Label(position + Vector3.down * (line.NeighborRadius + 0.3f),
                    $"线 {i + 1} · {NeighborName(line)} · d={line.Distance:F2}");
            }
        }

        private void DrawVelocitySpace(NavigationTestAgent agent, NavigationTestSnapshot snapshot)
        {
            var debug = snapshot.Avoidance;
            float maxSpeed = debug.HasSample != 0 ? debug.MaxSpeed : agent.Speed;
            float extent = Mathf.Max(0.5f, maxSpeed * 1.25f, snapshot.PhysicalVelocity.magnitude * 1.1f);
            float scale = Mathf.Max(0.1f, velocityScale);
            float radius = extent * scale;
            Vector3 center = snapshot.Position + Vector3.right * (radius + agent.Radius + 1);
            Handles.color = new Color(0.04f, 0.06f, 0.1f, 0.85f);
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = new Color(1, 1, 1, 0.35f);
            Handles.DrawLine(center - Vector3.right * radius, center + Vector3.right * radius);
            Handles.DrawLine(center - Vector3.up * radius, center + Vector3.up * radius);
            Handles.Label(center + Vector3.right * radius, "vx");
            Handles.Label(center + Vector3.up * radius, $"vy · #{selected + 1} 速度空间");
            Handles.Label(center + Vector3.down * (radius + 0.25f), $"缩放 ×{scale:F2}   白圆 |v|≤{maxSpeed:F2}");

            if (debug.HasSample != 0)
            {
                DrawFeasibleRegion(center, scale, maxSpeed);
                int outside = 0;
                for (int i = 0; i < constraints.Count; i++)
                {
                    var line = constraints[i];
                    Vector3 direction = V(line.Direction).normalized;
                    Vector3 point = V(line.Point);
                    Vector3 closest = point - direction * Vector3.Dot(point, direction);
                    if (closest.sqrMagnitude > extent * extent) { outside++; continue; }
                    float halfLength = Mathf.Sqrt(Mathf.Max(0, extent * extent - closest.sqrMagnitude));
                    Vector3 start = center + (closest - direction * halfLength) * scale;
                    Vector3 end = center + (closest + direction * halfLength) * scale;
                    Color color = ConstraintColor(line);
                    Handles.color = color;
                    Handles.DrawAAPolyLine(2, start, end);
                    Vector3 middle = center + closest * scale;
                    DrawArrow(middle, middle + V(line.AllowedNormal) * radius * 0.16f, color);
                    Handles.Label(end, (i + 1).ToString());
                }
                if (outside > 0) Handles.Label(center - Vector3.up * radius, $"{outside} 条边界在图外（仍参与求解）");
                DrawArrow(center, center + V(debug.InputVelocity) * scale, Color.gray);
                DrawArrow(center, center + V(debug.PreferredVelocity) * scale, PreferredColor);
                DrawArrow(center, center + V(debug.ResolvedVelocity) * scale, ResolvedColor);
            }
            Handles.color = Color.white;
            Handles.DrawWireDisc(center, Vector3.forward, maxSpeed * scale, 2);
            DrawArrow(center, center + snapshot.TargetVelocity * scale, TargetColor);
            DrawArrow(center, center + snapshot.PhysicalVelocity * scale, PhysicalColor);
            Handles.color = Color.white;
            Handles.DrawSolidDisc(center, Vector3.forward, radius * 0.016f);
        }

        private void DrawFeasibleRegion(Vector3 center, float scale, float maxSpeed)
        {
            feasiblePolygon.Clear();
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                feasiblePolygon.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * maxSpeed);
            }
            foreach (var line in constraints)
            {
                ClipHalfPlane(feasiblePolygon, in line, clippedPolygon);
                feasiblePolygon.Clear(); feasiblePolygon.AddRange(clippedPolygon);
                if (feasiblePolygon.Count == 0) break;
            }
            if (feasiblePolygon.Count < 3) return;
            var points = new Vector3[feasiblePolygon.Count];
            for (int i = 0; i < points.Length; i++) points[i] = center + (Vector3)feasiblePolygon[i] * scale;
            Handles.color = new Color(0.15f, 1, 0.35f, 0.18f);
            Handles.DrawAAConvexPolygon(points);
        }

        internal static void ClipHalfPlane(List<Vector2> input, in UnitAvoidanceDebugConstraint line, List<Vector2> output)
        {
            output.Clear();
            if (input.Count == 0) return;
            Vector2 previous = input[input.Count - 1];
            float previousMargin = line.SignedMargin(new float2(previous.x, previous.y));
            foreach (Vector2 current in input)
            {
                float margin = line.SignedMargin(new float2(current.x, current.y));
                if ((margin >= 0) != (previousMargin >= 0))
                    output.Add(Vector2.LerpUnclamped(previous, current, previousMargin / (previousMargin - margin)));
                if (margin >= 0) output.Add(current);
                previous = current; previousMargin = margin;
            }
        }

        private static void DrawArrow(Vector3 from, Vector3 to, Color color)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return;
            Handles.color = color;
            Handles.DrawAAPolyLine(2, from, to);
            Vector3 direction = delta.normalized;
            Vector3 side = new(-direction.y, direction.x, 0);
            float size = Mathf.Min(delta.magnitude * 0.3f, HandleUtility.GetHandleSize(to) * 0.09f);
            Handles.DrawAAPolyLine(2, to - direction * size + side * size * 0.5f, to,
                to - direction * size - side * size * 0.5f);
        }
    }
}
