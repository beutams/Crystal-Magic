using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor
{
    public static class EditorFocusUtility
    {
        public static void ClearTextFocus()
        {
            GUI.FocusControl(null);
            EditorGUI.FocusTextInControl(string.Empty);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }
    }
}
