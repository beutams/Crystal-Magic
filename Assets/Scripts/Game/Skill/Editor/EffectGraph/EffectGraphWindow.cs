using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Game.Data.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.EffectGraph
{
    public sealed class EffectGraphWindow : EditorWindow
    {
        private const float InspectorWidth = 330f;

        private EffectGraphBinding _binding;
        private EffectGraphModel _model;
        private EffectGraphLayoutStore _layoutStore;
        private EffectGraphLayoutData _layout;
        private EffectGraphView _graphView;
        private IMGUIContainer _inspector;
        private EffectData _selectedEffect;
        private bool _rebuildScheduled;

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
            _layoutStore = new EffectGraphLayoutStore();
            _model = new EffectGraphModel(binding);
            _layout = _layoutStore.Load(binding.OwnerKey);
            BuildRoot();
        }

        private void OnEnable()
        {
            if (_binding != null && _graphView == null)
                BuildRoot();
        }

        private void OnDisable()
        {
            _graphView?.SaveLayout();
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

        internal void SaveLayout(EffectGraphLayoutData layout, IEnumerable<string> validPaths)
        {
            if (_binding == null || _layoutStore == null || layout == null)
                return;

            _layout = layout;
            _layoutStore.Save(_binding.OwnerKey, layout);
            _layoutStore.Prune(_binding.OwnerKey, new HashSet<string>(validPaths ?? Enumerable.Empty<string>(), StringComparer.Ordinal));
        }

        private void BuildRoot()
        {
            rootVisualElement.Clear();
            if (_binding == null)
                return;

            VisualElement toolbar = new() { style = { flexDirection = FlexDirection.Row, height = 24f, paddingLeft = 6f } };
            toolbar.Add(new Label(_binding.DisplayName) { style = { flexGrow = 1f } });
            Button saveLayout = new(() => _graphView?.SaveLayout()) { text = "Save Layout" };
            toolbar.Add(saveLayout);
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
                _binding.NotifyChanged();
                _graphView?.SaveLayout();
                Repaint();
            }
        }
    }
}
