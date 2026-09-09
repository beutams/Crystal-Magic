using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CrystalMagic.Game.Data.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.EffectGraph
{
    public sealed class EffectGraphWindow : EditorWindow
    {
        private const float InspectorWidth = 330f;
        private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod(
            "MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private EffectGraphBinding _binding;
        private EffectGraphBinding _workingBinding;
        private EffectData[] _workingRootEffects = Array.Empty<EffectData>();
        private EffectGraphModel _model;
        private EffectGraphLayoutStore _layoutStore;
        private EffectGraphLayoutData _layout;
        private EffectGraphView _graphView;
        private IMGUIContainer _inspector;
        private EffectData _selectedEffect;
        private bool _rebuildScheduled;
        private bool _hasUnsavedChanges;

        public static void Open(EffectGraphBinding binding)
        {
            if (binding == null)
                return;

            EffectGraphWindow window = CreateInstance<EffectGraphWindow>();
            window.titleContent = new GUIContent("Effect Graph");
            window.minSize = new Vector2(900f, 600f);
            window.Initialize(binding);
            window.Show();
        }

        private void Initialize(EffectGraphBinding binding)
        {
            _binding = binding;
            _workingRootEffects = CloneEffects(binding.GetRootEffects());
            _workingBinding = new EffectGraphBinding(
                binding.OwnerKey,
                binding.DisplayName,
                () => _workingRootEffects,
                effects => _workingRootEffects = effects ?? Array.Empty<EffectData>(),
                MarkDirty);
            _layoutStore = new EffectGraphLayoutStore();
            _model = new EffectGraphModel(_workingBinding);
            _layout = _layoutStore.Load(binding.OwnerKey);
            BuildRoot();
        }

        private void OnEnable()
        {
            if (_binding != null && _graphView == null)
                BuildRoot();
        }

        internal void RebuildGraph()
        {
            if (_binding == null)
                return;

            _model ??= new EffectGraphModel(_binding);
            _model.Rebuild();
            _layout ??= _layoutStore.Load(_binding.OwnerKey);
            _graphView?.Rebuild(_model, _layout);
            _inspector?.MarkDirtyRepaint();
        }

        internal void ScheduleGraphRebuild()
        {
            if (_rebuildScheduled)
                return;

            _rebuildScheduled = true;
            rootVisualElement.schedule.Execute(() =>
            {
                _rebuildScheduled = false;
                RebuildGraph();
            }).ExecuteLater(0);
        }

        internal void SetSelection(EffectData effect)
        {
            _selectedEffect = effect;
            _inspector?.MarkDirtyRepaint();
        }

        internal void CaptureLayout(EffectGraphLayoutData layout)
        {
            if (layout == null)
                return;

            _layout = layout;
        }

        internal void MarkDirty()
        {
            _hasUnsavedChanges = true;
            titleContent.text = "Effect Graph *";
        }

        private void SaveGraph()
        {
            if (_binding == null || _layoutStore == null)
                return;

            _graphView?.CaptureLayout();
            if (_hasUnsavedChanges)
            {
                _binding.SetRootEffects(CloneEffects(_workingRootEffects));
                _binding.NotifyChanged();
            }

            _layoutStore.Save(_binding.OwnerKey, _layout ?? new EffectGraphLayoutData());
            _hasUnsavedChanges = false;
            titleContent.text = "Effect Graph";
        }

        private void BuildRoot()
        {
            rootVisualElement.Clear();
            if (_binding == null)
                return;

            VisualElement toolbar = new() { style = { flexDirection = FlexDirection.Row, height = 24f, paddingLeft = 6f } };
            toolbar.Add(new Label(_binding.DisplayName) { style = { flexGrow = 1f } });
            Button save = new(SaveGraph) { text = "Save" };
            toolbar.Add(save);
            rootVisualElement.Add(toolbar);

            VisualElement content = new() { style = { flexGrow = 1f, flexDirection = FlexDirection.Row } };
            _graphView = new EffectGraphView(this) { style = { flexGrow = 1f } };
            content.Add(_graphView);

            _inspector = new IMGUIContainer(DrawInspector)
            {
                style =
                {
                    width = InspectorWidth,
                    minWidth = InspectorWidth,
                    borderLeftWidth = 1f,
                    borderLeftColor = new Color(0.2f, 0.2f, 0.2f),
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    paddingTop = 8f,
                },
            };
            content.Add(_inspector);
            rootVisualElement.Add(content);
            RebuildGraph();
        }

        private void DrawInspector()
        {
            if (_selectedEffect == null)
            {
                EditorGUILayout.HelpBox("Select an Effect node to edit its properties. Nested effect arrays are edited through their connected containers.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            bool changed = EffectGraphInspector.DrawEffect(_model, _selectedEffect);
            if (EditorGUI.EndChangeCheck() || changed)
            {
                _workingBinding.NotifyChanged();
                Repaint();
            }
        }

        private static EffectData[] CloneEffects(EffectData[] effects)
        {
            if (effects == null || effects.Length == 0)
                return Array.Empty<EffectData>();

            Dictionary<object, object> copies = new(ReferenceComparer.Instance);
            EffectData[] clone = new EffectData[effects.Length];
            for (int index = 0; index < effects.Length; index++)
                clone[index] = CloneValue(effects[index], copies) as EffectData;

            return clone;
        }

        private static object CloneValue(object value, Dictionary<object, object> copies)
        {
            if (value == null)
                return null;

            Type type = value.GetType();
            if (type.IsValueType || value is string || value is UnityEngine.Object || value is Type || value is Delegate)
                return value;

            if (copies.TryGetValue(value, out object existing))
                return existing;

            if (value is Array sourceArray)
            {
                Array cloneArray = Array.CreateInstance(type.GetElementType(), sourceArray.Length);
                copies.Add(value, cloneArray);
                for (int index = 0; index < sourceArray.Length; index++)
                    cloneArray.SetValue(CloneValue(sourceArray.GetValue(index), copies), index);
                return cloneArray;
            }

            if (value is IList sourceList && Activator.CreateInstance(type) is IList cloneList)
            {
                copies.Add(value, cloneList);
                foreach (object item in sourceList)
                    cloneList.Add(CloneValue(item, copies));
                return cloneList;
            }

            object clone = MemberwiseCloneMethod.Invoke(value, null);
            copies.Add(value, clone);
            for (Type current = type; current != null; current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsInitOnly)
                        continue;

                    field.SetValue(clone, CloneValue(field.GetValue(value), copies));
                }
            }

            return clone;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
