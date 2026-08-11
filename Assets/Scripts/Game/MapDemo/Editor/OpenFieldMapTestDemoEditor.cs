using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo.Editor
{
    [CustomEditor(typeof(OpenFieldMapTestDemo))]
    public sealed class OpenFieldMapTestDemoEditor : UnityEditor.Editor
    {
        private const float InspectorPreviewMaxHeight = 320f;

        private SerializedProperty _mapWidth;
        private SerializedProperty _mapHeight;
        private SerializedProperty _terrainGenerationMethod;
        private SerializedProperty _fbmPerlinTerrain;
        private SerializedProperty _voronoiTerrain;
        private SerializedProperty _gameplayContent;

        private void OnEnable()
        {
            _mapWidth = serializedObject.FindProperty("_mapWidth");
            _mapHeight = serializedObject.FindProperty("_mapHeight");
            _terrainGenerationMethod = serializedObject.FindProperty("_terrainGenerationMethod");
            _fbmPerlinTerrain = serializedObject.FindProperty("_fbmPerlinTerrain");
            _voronoiTerrain = serializedObject.FindProperty("_voronoiTerrain");
            _gameplayContent = serializedObject.FindProperty("_gameplayContent");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Map Size", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mapWidth);
            EditorGUILayout.PropertyField(_mapHeight);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain Generation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_terrainGenerationMethod);

            bool usesFbmPerlin = _terrainGenerationMethod.enumValueIndex == 0;
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(usesFbmPerlin ? _fbmPerlinTerrain : _voronoiTerrain, true);
            DrawGameplayContentSettings(_gameplayContent);
            serializedObject.ApplyModifiedProperties();

            OpenFieldMapTestDemo demo = (OpenFieldMapTestDemo)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                usesFbmPerlin
                    ? "fBm Perlin creates low void, middle walkable ground, and high cliffs that block line of sight."
                    : "Voronoi keeps low and middle regions walkable, while high regions become cliffs that block line of sight. Edge Jitter bends otherwise straight region borders.",
                MessageType.Info);
            EditorGUILayout.HelpBox("Terrain is generated before anchors. Points may occupy any walkable area, then a path search from spawn must reach the exit and every interest point; unsuitable seeds are skipped automatically.", MessageType.None);
            EditorGUILayout.HelpBox("Interest points contain chests and Monster Level 1/2/3. Wild monsters are always Level 1 and are placed only on walkable cells reachable from spawn and outside every anchor area.", MessageType.None);

            if (GUILayout.Button("Generate Map"))
            {
                Undo.RecordObject(demo, "Generate Open Field Map");
                demo.GenerateDemo();
                EditorUtility.SetDirty(demo);

                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);

                GUIUtility.ExitGUI();
            }

            DrawInspectorPreview(demo);
        }

        private static void DrawGameplayContentSettings(SerializedProperty gameplayContent)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gameplay Content", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Vector3 X/Y/Z maps to Small/Medium/Large interest points. Counts are rounded to whole numbers when generated.", MessageType.None);
            EditorGUILayout.PropertyField(gameplayContent.FindPropertyRelative("ChestCounts"));
            EditorGUILayout.PropertyField(gameplayContent.FindPropertyRelative("MonsterLevel1Counts"));
            EditorGUILayout.PropertyField(gameplayContent.FindPropertyRelative("MonsterLevel2Counts"));
            EditorGUILayout.PropertyField(gameplayContent.FindPropertyRelative("MonsterLevel3Counts"));
            EditorGUILayout.PropertyField(gameplayContent.FindPropertyRelative("WildMonsterCount"));
        }

        private static void DrawInspectorPreview(OpenFieldMapTestDemo demo)
        {
            if (!demo.HasPreviewTexture || demo.PreviewTexture == null)
                return;

            Texture2D texture = demo.PreviewTexture;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            float aspect = texture.width / (float)Mathf.Max(1, texture.height);
            Rect previewRect = GUILayoutUtility.GetAspectRect(aspect, GUILayout.MaxHeight(InspectorPreviewMaxHeight));
            EditorGUI.DrawPreviewTexture(previewRect, texture, null, ScaleMode.StretchToFill);
            EditorGUILayout.LabelField($"Step: {demo.TerrainGenerationName}    Seed: {demo.PreviewSeed}    Size: {demo.PreviewWidth} x {demo.PreviewHeight}    Reachability: Valid");
            EditorGUILayout.LabelField(demo.PreviewContentSummary);
        }
    }
}
