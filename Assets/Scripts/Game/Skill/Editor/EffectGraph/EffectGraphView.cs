using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Game.Data.Effects;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.EffectGraph
{
    internal sealed class EffectGraphView : GraphView
    {
        private const float ContainerWidth = 220f;
        private const float ContainerHeight = 86f;
        private const float EffectWidth = 180f;
        private const float EffectHeight = 92f;
        private const float EffectGap = 28f;
        private const float ChildRowOffset = 135f;

        private readonly EffectGraphWindow _window;
        private readonly Dictionary<EffectGraphContainerModel, EffectArrayContainerView> _containerViews = new();
        private readonly Dictionary<EffectData, EffectNodeView> _effectViews = new();
        private EffectGraphModel _model;
        private EffectGraphLayoutData _layout;
        private bool _isBuilding;

        public EffectGraphView(EffectGraphWindow window)
        {
            _window = window;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            GridBackground grid = new();
            Insert(0, grid);
            grid.StretchToParentSize();
            graphViewChanged = OnGraphViewChanged;
        }

        public void Rebuild(EffectGraphModel model, EffectGraphLayoutData layout)
        {
            _model = model;
            _layout = layout ?? new EffectGraphLayoutData();
            _isBuilding = true;
            foreach (GraphElement element in graphElements.ToList())
                RemoveElement(element);
            _containerViews.Clear();
            _effectViews.Clear();

            EffectGraphEntryView entry = new();
            entry.SetPosition(new Rect(80f, 64f, 130f, 58f));
            AddElement(entry);

            int fallbackIndex = 0;
            foreach (EffectGraphContainerModel container in _model.Containers)
            {
                Vector2 position = GetContainerPosition(container, fallbackIndex++);
                EffectArrayContainerView containerView = new(container, this);
                containerView.SetPosition(new Rect(position, new Vector2(ContainerWidth, ContainerHeight)));
                AddElement(containerView);
                _containerViews.Add(container, containerView);
            }

            foreach ((EffectGraphContainerModel container, EffectArrayContainerView containerView) in _containerViews)
            {
                EffectData[] effects = container.Effects;
                for (int index = 0; index < effects.Length; index++)
                {
                    EffectData effect = effects[index];
                    if (effect == null || _effectViews.ContainsKey(effect))
                        continue;

                    EffectNodeView effectView = new(effect, container, _model, this);
                    effectView.SetPosition(new Rect(GetEffectPosition(containerView, index), new Vector2(EffectWidth, EffectHeight)));
                    AddElement(effectView);
                    _effectViews.Add(effect, effectView);
                    AddElement(containerView.Output.ConnectTo(effectView.Input));
                }
            }

            AddElement(entry.Output.ConnectTo(_containerViews[_model.Root].Input));
            foreach ((EffectGraphContainerModel container, EffectArrayContainerView view) in _containerViews)
            {
                if (container.IsRoot || !_effectViews.TryGetValue(container.OwnerEffect, out EffectNodeView ownerView))
                    continue;

                if (ownerView.TryGetOutput(container.OwnerField, out Port output))
                    AddElement(output.ConnectTo(view.Input));
            }

            UpdateViewTransform(_layout.ViewPosition, Vector3.one * Mathf.Max(0.1f, _layout.ViewScale));
            _isBuilding = false;
        }

        public void AddEffect(EffectGraphContainerModel container)
        {
            GenericMenu menu = new();
            foreach (EffectGraphTypeInfo type in EffectGraphTypeRegistry.Types)
            {
                EffectGraphTypeInfo captured = type;
                menu.AddItem(new GUIContent($"Add Effect/{captured.DisplayName}"), false, () =>
                {
                    _model.AddEffect(container, captured.Type, container.Effects.Length);
                    _window.RebuildGraph();
                });
            }
            menu.ShowAsContext();
        }

        public void SelectEffect(EffectData effect)
        {
            _window.SetSelection(effect);
        }

        public void SaveLayout()
        {
            if (_model == null || _layout == null)
                return;

            _layout.ViewPosition = viewTransform.position;
            _layout.ViewScale = viewTransform.scale.x;
            _layout.Containers.Clear();
            foreach ((EffectGraphContainerModel container, EffectArrayContainerView view) in _containerViews)
            {
                _layout.Containers.Add(new EffectGraphContainerLayout
                {
                    Path = container.Path,
                    Position = view.GetPosition().position,
                    Expanded = view.expanded,
                });
            }
            _window.SaveLayout(_layout, _model.Containers.Select(container => container.Path));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            return new List<Port>();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_isBuilding || _model == null)
                return change;

            if (change.elementsToRemove != null)
            {
                change.elementsToRemove.RemoveAll(element => element is Edge || element is EffectArrayContainerView || element is EffectGraphEntryView);
                EffectNodeView removed = change.elementsToRemove.OfType<EffectNodeView>().FirstOrDefault();
                if (removed != null)
                {
                    int index = Array.IndexOf(removed.Container.Effects, removed.Effect);
                    if (index >= 0 && _model.RemoveEffect(removed.Container, index))
                        _window.ScheduleGraphRebuild();
                }
            }

            if (change.movedElements != null)
            {
                bool movedContainer = false;
                foreach (EffectArrayContainerView container in change.movedElements.OfType<EffectArrayContainerView>())
                {
                    UpdateContainerPosition(container);
                    movedContainer = true;
                }
                foreach (EffectNodeView effect in change.movedElements.OfType<EffectNodeView>())
                    TryInsertMovedEffect(effect);
                SaveLayout();
                if (movedContainer)
                    _window.ScheduleGraphRebuild();
            }

            return change;
        }

        private void TryInsertMovedEffect(EffectNodeView effectView)
        {
            EffectGraphContainerModel target = FindDropTarget(effectView.GetPosition().center);
            if (target == null)
            {
                _window.ScheduleGraphRebuild();
                return;
            }

            EffectGraphContainerModel source = effectView.Container;
            List<EffectData> siblings = new(target.Effects.Where(effect => !ReferenceEquals(effect, effectView.Effect)));
            int insertIndex = siblings.Count;
            float x = effectView.GetPosition().center.x;
            for (int index = 0; index < siblings.Count; index++)
            {
                if (_effectViews.TryGetValue(siblings[index], out EffectNodeView sibling) && x < sibling.GetPosition().center.x)
                {
                    insertIndex = index;
                    break;
                }
            }

            int sourceIndex = Array.IndexOf(source.Effects, effectView.Effect);
            if (sourceIndex >= 0 && _model.MoveEffect(source, sourceIndex, target, insertIndex))
                _window.ScheduleGraphRebuild();
        }

        private EffectGraphContainerModel FindDropTarget(Vector2 point)
        {
            foreach ((EffectGraphContainerModel container, EffectArrayContainerView view) in _containerViews)
            {
                Rect row = GetChildRowRect(view, Math.Max(1, container.Effects.Length));
                if (row.Contains(point))
                    return container;
            }
            return null;
        }

        private void UpdateContainerPosition(EffectArrayContainerView view)
        {
            for (int index = 0; index < _layout.Containers.Count; index++)
            {
                if (!string.Equals(_layout.Containers[index].Path, view.Container.Path, StringComparison.Ordinal))
                    continue;

                _layout.Containers[index].Position = view.GetPosition().position;
                return;
            }
        }

        private Vector2 GetContainerPosition(EffectGraphContainerModel container, int fallbackIndex)
        {
            EffectGraphContainerLayout saved = _layout.Containers.FirstOrDefault(item => item != null && string.Equals(item.Path, container.Path, StringComparison.Ordinal));
            if (saved != null)
                return saved.Position;

            if (!container.IsRoot && container.Parent != null)
            {
                int ownerIndex = Array.IndexOf(container.Parent.Effects, container.OwnerEffect);
                Vector2 parentPosition = _containerViews.TryGetValue(container.Parent, out EffectArrayContainerView parentView)
                    ? parentView.GetPosition().position
                    : new Vector2(280f, 60f);
                return parentPosition + new Vector2(
                    Mathf.Max(0, ownerIndex) * (EffectWidth + EffectGap),
                    ChildRowOffset + EffectHeight + 120f + GetFieldOffset(container));
            }

            return container.IsRoot
                ? new Vector2(280f, 60f)
                : new Vector2(280f + (fallbackIndex % 3) * 350f, 300f + fallbackIndex * 220f);
        }

        private static float GetFieldOffset(EffectGraphContainerModel container)
        {
            if (container.OwnerEffect == null || container.OwnerField == null)
                return 0f;

            IReadOnlyList<FieldInfo> fields = container.OwnerEffect.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType.IsArray && typeof(EffectData).IsAssignableFrom(field.FieldType.GetElementType())).ToArray();
            int index = Array.IndexOf(fields.ToArray(), container.OwnerField);
            return Math.Max(0, index) * 180f;
        }

        private static Vector2 GetEffectPosition(EffectArrayContainerView container, int index)
        {
            Vector2 origin = container.GetPosition().position;
            return origin + new Vector2(index * (EffectWidth + EffectGap), ChildRowOffset);
        }

        private static Rect GetChildRowRect(EffectArrayContainerView container, int count)
        {
            Vector2 origin = container.GetPosition().position;
            return new Rect(origin.x - 12f, origin.y + ChildRowOffset - 18f, Math.Max(ContainerWidth, count * (EffectWidth + EffectGap)), EffectHeight + 40f);
        }

    }

    internal sealed class EffectGraphEntryView : Node
    {
        public EffectGraphEntryView()
        {
            title = "Entry";
            capabilities = Capabilities.Selectable;
            Output = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
            Output.portName = "Effects";
            outputContainer.Add(Output);
            RefreshPorts();
            RefreshExpandedState();
        }

        public Port Output { get; }
    }

    internal sealed class EffectArrayContainerView : Node
    {
        public EffectArrayContainerView(EffectGraphContainerModel container, EffectGraphView graphView)
        {
            Container = container;
            title = $"{container.DisplayName} ({container.Effects.Length})";
            Input = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            Input.portName = "Input";
            inputContainer.Add(Input);
            Output = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Output.portName = "Effects";
            outputContainer.Add(Output);
            Button addButton = new(() => graphView.AddEffect(container)) { text = "+ Add Effect" };
            extensionContainer.Add(addButton);
            capabilities = Capabilities.Movable | Capabilities.Selectable;
            RefreshPorts();
            RefreshExpandedState();
        }

        public EffectGraphContainerModel Container { get; }

        public Port Input { get; }

        public Port Output { get; }
    }

    internal sealed class EffectNodeView : Node
    {
        private readonly Dictionary<FieldInfo, Port> _outputs = new();

        public EffectNodeView(EffectData effect, EffectGraphContainerModel container, EffectGraphModel model, EffectGraphView graphView)
        {
            Effect = effect;
            Container = container;
            title = EffectGraphTypeRegistry.GetDisplayName(effect);
            titleContainer.style.backgroundColor = EffectGraphTypeRegistry.GetColor(effect);
            Input = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            Input.portName = "Input";
            inputContainer.Add(Input);
            foreach (FieldInfo field in model.GetNestedEffectArrayFields(effect))
            {
                Port output = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                output.portName = EditorLabelUtility.GetLabel(field);
                outputContainer.Add(output);
                _outputs.Add(field, output);
            }
            capabilities = Capabilities.Movable | Capabilities.Selectable | Capabilities.Deletable;
            RegisterCallback<MouseDownEvent>(_ => graphView.SelectEffect(effect));
            RefreshPorts();
            RefreshExpandedState();
        }

        public EffectData Effect { get; }

        public EffectGraphContainerModel Container { get; }

        public Port Input { get; }

        public bool TryGetOutput(FieldInfo field, out Port output)
        {
            return _outputs.TryGetValue(field, out output);
        }
    }
}
