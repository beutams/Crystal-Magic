#if LEGACY_ROOM_DUNGEON_REFERENCE
using CrystalMagic.Game.MapDemo;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo.Editor
{
    [CustomEditor(typeof(TunnelingMapDemo))]
    public sealed class TunnelingMapDemoEditor : UnityEditor.Editor
    {
        private static bool _pickDebugCoordinateMode;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            TunnelingMapDemo demo = (TunnelingMapDemo)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"统计文件: {TunnelingMapDemo.GenerationReportFilePath}", MessageType.Info);
            EditorGUILayout.HelpBox($"地图Dump: {TunnelingMapDemo.MapDumpFilePath}", MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成"))
                    demo.GenerateDemoMap();

                if (GUILayout.Button("初始化步进"))
                    demo.InitializeStepDebug();

                if (GUILayout.Button("步进一步"))
                    demo.StepDebugOnce();

                if (GUILayout.Button("执行死路"))
                    demo.ApplyDemoDeadEndDeletions();

                if (GUILayout.Button("新种子生成"))
                    demo.GenerateDemoMapWithNewSeed();

                if (GUILayout.Button("清空"))
                    demo.ClearDemoMap();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("开关分析显示"))
                    demo.ToggleAnalysisOverlay();

                if (GUILayout.Button("开关骨架显示"))
                    demo.ToggleSkeletonOverlay();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打印调试坐标"))
                    demo.LogDebugDisplayCoordinate();

                if (GUILayout.Button(_pickDebugCoordinateMode ? "退出拾取坐标" : "拾取调试坐标"))
                {
                    _pickDebugCoordinateMode = !_pickDebugCoordinateMode;
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("显示调试方块"))
                    demo.ShowDebugCoordinateMarker();

                if (GUILayout.Button("删除调试方块"))
                    demo.ClearDebugCoordinateMarker();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("清空统计文件"))
                demo.ClearGenerationReportFile();
        }

        private void OnSceneGUI()
        {
            if (!_pickDebugCoordinateMode)
                return;

            TunnelingMapDemo demo = (TunnelingMapDemo)target;
            if (!demo.HasDisplayMap)
                return;

            Event currentEvent = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 220f, 40f), "调试坐标拾取中：点击地图", GUI.skin.window);
            GUILayout.EndArea();
            Handles.EndGUI();

            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                _pickDebugCoordinateMode = false;
                currentEvent.Use();
                SceneView.RepaintAll();
                return;
            }

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0 || currentEvent.alt)
                return;

            Plane plane = new Plane(demo.transform.forward, demo.transform.position);
            Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            if (!plane.Raycast(ray, out float distance))
                return;

            Vector3 hitPoint = ray.GetPoint(distance);
            if (!demo.TryGetDisplayCoordinateFromWorld(hitPoint, out Vector2Int coordinate))
                return;

            Undo.RecordObject(demo, "Pick Debug Display Coordinate");
            demo.SetDebugDisplayCoordinate(coordinate);
            EditorUtility.SetDirty(demo);
            _pickDebugCoordinateMode = false;
            currentEvent.Use();
            SceneView.RepaintAll();
        }
    }
}


#endif
