using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    internal sealed class StateScriptAccessorDropdown : AdvancedDropdown
    {
        private const float MinimumMenuWidth = 420f;
        private const float MenuHorizontalPadding = 64f;

        private static StateScriptAccessorDropdown s_activeDropdown;

        private readonly List<string> _keys;
        private readonly Action<string> _onSelected;

        private StateScriptAccessorDropdown(
            IEnumerable<string> keys,
            Action<string> onSelected,
            float inspectorWidth)
            : base(new AdvancedDropdownState())
        {
            _keys = keys?
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList() ?? new List<string>();
            _onSelected = onSelected;
            minimumSize = new Vector2(
                Mathf.Max(MinimumMenuWidth, inspectorWidth, GetRequiredMenuWidth(_keys)),
                320f);
        }

        public static void Draw(
            string label,
            string currentKey,
            IEnumerable<string> keys,
            string emptyLabel,
            Action<string> onSelected)
        {
            Rect controlRect = EditorGUILayout.GetControlRect();
            Rect buttonRect = EditorGUI.PrefixLabel(controlRect, new GUIContent(label));
            string displayName = string.IsNullOrWhiteSpace(currentKey) ? emptyLabel : currentKey;
            if (!EditorGUI.DropdownButton(buttonRect, new GUIContent(displayName), FocusType.Keyboard, EditorStyles.popup))
                return;

            s_activeDropdown = new StateScriptAccessorDropdown(keys, selectedKey =>
            {
                s_activeDropdown = null;
                onSelected?.Invoke(selectedKey);
            }, EditorGUIUtility.currentViewWidth);
            s_activeDropdown.Show(buttonRect);
        }

        private static float GetRequiredMenuWidth(IEnumerable<string> keys)
        {
            float widestItem = EditorStyles.label.CalcSize(new GUIContent("Select Accessor")).x;
            foreach (string key in keys)
                widestItem = Mathf.Max(widestItem, EditorStyles.label.CalcSize(new GUIContent(key)).x);

            return widestItem + MenuHorizontalPadding;
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new("Select Accessor");
            root.AddChild(new AccessorItem("(Clear selection)", string.Empty));
            for (int i = 0; i < _keys.Count; i++)
                root.AddChild(new AccessorItem(_keys[i], _keys[i]));

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is AccessorItem accessor)
                _onSelected?.Invoke(accessor.Key);
        }

        private sealed class AccessorItem : AdvancedDropdownItem
        {
            public AccessorItem(string name, string key)
                : base(name)
            {
                Key = key;
            }

            public string Key { get; }
        }
    }
}
