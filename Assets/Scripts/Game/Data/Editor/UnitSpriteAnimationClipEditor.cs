using CrystalMagic.Game.Data;
using UnityEditor;

namespace CrystalMagic.Editor.Data
{
    [CustomEditor(typeof(UnitSpriteAnimationClip))]
    public sealed class UnitSpriteAnimationClipEditor : UnityEditor.Editor
    {
        private SerializedProperty _framesPerSecond;
        private SerializedProperty _loop;
        private SerializedProperty _referenceFrameSizePixels;
        private SerializedProperty _referenceFrameWorldSize;
        private SerializedProperty _frontFrames;
        private SerializedProperty _backFrames;
        private SerializedProperty _rightFrames;

        private void OnEnable()
        {
            _framesPerSecond = serializedObject.FindProperty("_framesPerSecond");
            _loop = serializedObject.FindProperty("_loop");
            _referenceFrameSizePixels = serializedObject.FindProperty("_referenceFrameSizePixels");
            _referenceFrameWorldSize = serializedObject.FindProperty("_referenceFrameWorldSize");
            _frontFrames = serializedObject.FindProperty("_frontFrames");
            _backFrames = serializedObject.FindProperty("_backFrames");
            _rightFrames = serializedObject.FindProperty("_rightFrames");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_framesPerSecond, new UnityEngine.GUIContent("Frames Per Second"));
            EditorGUILayout.PropertyField(_loop, new UnityEngine.GUIContent("Loop"));
            EditorGUILayout.PropertyField(_referenceFrameSizePixels, new UnityEngine.GUIContent("Reference Frame Pixels"));
            EditorGUILayout.PropertyField(_referenceFrameWorldSize, new UnityEngine.GUIContent("Reference Frame World Size"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_frontFrames, new UnityEngine.GUIContent("Front Frames"), true);
            EditorGUILayout.PropertyField(_backFrames, new UnityEngine.GUIContent("Back Frames"), true);
            EditorGUILayout.PropertyField(_rightFrames, new UnityEngine.GUIContent("Right Frames"), true);
            EditorGUILayout.HelpBox(
                "Drag sliced Sprites into each list in playback order. Set every Sprite pivot to the same ground point. Left direction mirrors Right automatically.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
