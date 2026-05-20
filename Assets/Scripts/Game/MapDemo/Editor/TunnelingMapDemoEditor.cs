using CrystalMagic.Game.MapDemo;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo.Editor
{
    [CustomEditor(typeof(TunnelingMapDemo))]
    public sealed class TunnelingMapDemoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            TunnelingMapDemo demo = (TunnelingMapDemo)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"统计文件: {TunnelingMapDemo.GenerationReportFilePath}", MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成"))
                    demo.GenerateDemoMap();

                if (GUILayout.Button("新种子生成"))
                    demo.GenerateDemoMapWithNewSeed();

                if (GUILayout.Button("清空"))
                    demo.ClearDemoMap();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("清空统计文件"))
                demo.ClearGenerationReportFile();
        }
    }
}

