using CrystalMagic.Core;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Localization
{
    [CustomEditor(typeof(LocalizedTextMeshProUGUI)), CanEditMultipleObjects]
    public sealed class LocalizedTextMeshProUGUIEditor : TMP_EditorPanelUI
    {
        private SerializedProperty _localizationKey;

        protected override void OnEnable()
        {
            base.OnEnable();
            _localizationKey = serializedObject.FindProperty("_localizationKey");
        }

        public override void OnInspectorGUI()
        {
            DrawLocalizationSettings();
            base.OnInspectorGUI();
        }

        private void DrawLocalizationSettings()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_localizationKey, new GUIContent("Localization Key"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                foreach (Object targetObject in targets)
                {
                    LocalizedTextMeshProUGUI localizedText = (LocalizedTextMeshProUGUI)targetObject;
                    localizedText.ApplyLocalizationKey();
                    EditorUtility.SetDirty(localizedText);
                }
            }

            EditorGUILayout.Space();
        }
    }
}
