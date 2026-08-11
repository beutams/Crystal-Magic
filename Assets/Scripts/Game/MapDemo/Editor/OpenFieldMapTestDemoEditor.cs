using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo.Editor
{
    [CustomEditor(typeof(OpenFieldMapTestDemo))]
    public sealed class OpenFieldMapTestDemoEditor : UnityEditor.Editor
    {
        private const float InspectorPreviewMaxHeight = 320f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            OpenFieldMapTestDemo demo = (OpenFieldMapTestDemo)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Terrain Noise controls the low/middle and middle/high thresholds plus the frequency and amplitude of the third Perlin layer. Movement rules are not generated yet.", MessageType.Info);

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
            EditorGUILayout.LabelField($"Seed: {demo.PreviewSeed}    Size: {demo.PreviewWidth} x {demo.PreviewHeight}");
        }
    }
}
