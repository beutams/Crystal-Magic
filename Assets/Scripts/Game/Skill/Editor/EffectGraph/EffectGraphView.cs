using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Editor;
using CrystalMagic.Game.Data.Effects;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.EffectGraph
{
    internal sealed class EffectGraphView : GraphView
    {
        private const float ContainerWidth = EffectGraphAutoLayout.ContainerWidth;
        private const float ContainerHeight = 86f;
        private const float EffectWidth = EffectGraphAutoLayout.EffectWidth;
        private const float EffectHeight = 92f;

        private readonly EffectGraphWindow _window;
        private readonly Dictionary<EffectGraphContainerModel, EffectArrayStackView> _containerViews = new();
        private readonly Dictionary<EffectData, EffectNodeView> _effectViews = new();
        private readonly HashSet<EffectArrayStackView> _stackViews = new();
        private EffectGraphModel _model;
        private EffectGraphLayoutData _layout;
        private bool _isBuilding;
        private EffectGraphEntryView _entry;

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
            _stackViews.Clear();

            bool needsLayout = _model.Containers.Any(container => ShouldShowContainer(container) &&
                !_layout.Containers.Any(saved => saved != null && saved.Path == container.Path));
            if (needsLayout)
                EffectGraphAutoLayout.Arrange(_model, _layout);

            EffectGraphEntryView entry = _entry = new();
            PositionEntry();
            AddElement(entry);

            foreach (EffectGraphContainerModel container in _model.Containers)
            {
                if (!ShouldShowContainer(container))
                    continue;

                Vector2 position = _layout.Containers.First(saved => saved.Path == container.Path).Position;
                EffectArrayStackView containerView = new(container);
                containerView.SetPosition(new Rect(position, new Vector2(
                    EffectGraphAutoLayout.GetWidth(container.Effects.Length), EffectGraphAutoLayout.GetHeight(container.Effects.Length))));
                AddElement(containerView);
                _containerViews.Add(container, containerView);
                _stackViews.Add(containerView);
            }

            foreach ((EffectGraphContainerModel container, EffectArrayStackView containerView) in _containerViews)
            {
                foreach (EffectData effect in container.Effects)
                {
                    if (effect == null || _effectViews.ContainsKey(effect))
                        continue;

                    EffectNodeView effectView = new(effect, container, _model, this);
                    effectView.SetPosition(new Rect(Vector2.zero, new Vector2(EffectWidth, EffectHeight)));
                    AddElement(effectView);
                    containerView.AddElement(effectView);
                    // StackNode clears explicit position/size when accepting a child.
                    effectView.style.width = EffectWidth;
                    effectView.style.minWidth = EffectWidth;
                    effectView.style.marginLeft = 0f;
                    effectView.style.marginTop = 0f;
                    effectView.style.marginBottom = 0f;
                    effectView.style.marginRight = ReferenceEquals(effect, container.Effects.LastOrDefault()) ? 0f : EffectGraphAutoLayout.EffectGap;
                    _effectViews.Add(effect, effectView);
                }
            }

            foreach ((EffectGraphContainerModel container, EffectArrayStackView stack) in _containerViews)
            {
                if (container.IsRoot)
                {
                    AddElement(entry.Output.ConnectTo(stack.Input));
                    continue;
                }

                if (_effectViews.TryGetValue(container.OwnerEffect, out EffectNodeView ownerView) &&
                    ownerView.TryGetOutput(container.OwnerField, out Port output))
                {
                    AddElement(output.ConnectTo(stack.Input));
                }
            }

            UpdateViewTransform(_layout.ViewPosition, Vector3.one * Mathf.Max(0.1f, _layout.ViewScale));
            _isBuilding = false;
            if (needsLayout)
                schedule.Execute(AutoArrange).ExecuteLater(0);
        }

        public void AutoArrange()
        {
            if (_model == null) return;
            _isBuilding = true;
            EffectGraphAutoLayout.Arrange(_model, _layout, container =>
                _containerViews.TryGetValue(container, out var view) ? view.GetPosition().size : Vector2.zero);
            foreach (var saved in _layout.Containers)
            {
                EffectGraphContainerModel container = _model.FindContainer(saved.Path);
                if (container != null && _containerViews.TryGetValue(container, out var view))
                    view.SetPosition(new Rect(saved.Position, view.GetPosition().size));
            }
            PositionEntry();
            UpdateViewTransform(_layout.ViewPosition, Vector3.one);
            _isBuilding = false;
            CaptureLayout();
            _window.MarkLayoutDirty();
        }

        private void PositionEntry()
        {
            Vector2 root = _layout.Containers.First(saved => saved.Path == "root").Position;
            float width = EffectGraphAutoLayout.GetWidth(_model.Root.Effects.Length);
            _entry.SetPosition(new Rect(root.x + (width - 130f) * 0.5f, 60f, 130f, 58f));
        }

        public void SelectEffect(EffectData effect)
        {
            _window.SetSelection(effect);
        }

        public void CaptureLayout()
        {
            if (_model == null || _layout == null)
                return;

            _layout.ViewPosition = viewTransform.position;
            _layout.ViewScale = viewTransform.scale.x;
            _layout.Containers.Clear();
            foreach ((EffectGraphContainerModel container, EffectArrayStackView view) in _containerViews)
            {
                _layout.Containers.Add(new EffectGraphContainerLayout
                {
                    Path = container.Path,
                    Position = view.GetPosition().position,
                    Expanded = view.expanded,
                });
            }

            _window.CaptureLayout(_layout);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            if (startPort == null)
                return new List<Port>();

            if (IsSourceOutput(startPort))
            {
                return _stackViews
                    .Where(stack => !stack.IsBound)
                    .Select(stack => stack.Input)
                    .ToList();
            }

            if (startPort.node is EffectArrayStackView { IsBound: false })
            {
                List<Port> outputs = new();
                foreach (Port port in ports)
                {
                    if (IsSourceOutput(port))
                        outputs.Add(port);
                }

                return outputs;
            }

            return new List<Port>();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            foreach (EffectGraphTypeInfo type in EffectGraphTypeRegistry.Types)
            {
                EffectGraphTypeInfo captured = type;
                evt.menu.AppendAction($"Create Effect/{captured.DisplayName}", action =>
                    CreateEffectDraft(captured.Type, action.eventInfo.localMousePosition));
            }

            evt.menu.AppendAction("Create Stack", action => CreateStackDraft(action.eventInfo.localMousePosition));
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_isBuilding || _model == null)
                return change;

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                foreach (Edge edge in change.edgesToCreate)
                    TryBindStack(edge);

                // A successful bind triggers a rebuild, which creates the persisted edge.
                change.edgesToCreate.Clear();
            }

            if (change.elementsToRemove != null)
            {
                change.elementsToRemove.RemoveAll(element =>
                    element is Edge ||
                    element is EffectGraphEntryView ||
                    element is EffectArrayStackView { IsBound: true });

                foreach (EffectNodeView removed in change.elementsToRemove.OfType<EffectNodeView>().ToArray())
                {
                    _effectViews.Remove(removed.Effect);
                    if (removed.Container == null)
                        continue;

                    int index = Array.IndexOf(removed.Container.Effects, removed.Effect);
                    if (index >= 0 && _model.RemoveEffect(removed.Container, index))
                        _window.ScheduleGraphRebuild();
                }

                foreach (EffectArrayStackView removed in change.elementsToRemove.OfType<EffectArrayStackView>())
                    _stackViews.Remove(removed);
            }

            if (change.movedElements != null)
            {
                HashSet<EffectArrayStackView> movedStacks = new(change.movedElements.OfType<EffectArrayStackView>().Where(stack => stack.IsBound));
                foreach (EffectArrayStackView stack in movedStacks)
                    UpdateContainerPosition(stack);

                foreach (EffectNodeView effect in change.movedElements.OfType<EffectNodeView>())
                {
                    if (!movedStacks.Any(stack => ReferenceEquals(stack.Container, effect.Container)))
                        TryInsertMovedEffect(effect);
                }

                if (movedStacks.Count > 0)
                {
                    CaptureLayout();
                    _window.MarkDirty();
                    _window.ScheduleGraphRebuild();
                }
            }

            return change;
        }

        private void CreateEffectDraft(Type effectType, Vector2 position)
        {
            if (!EffectGraphTypeRegistry.TryCreate(effectType, out EffectData effect))
                return;

            EffectNodeView effectView = new(effect, null, _model, this);
            effectView.SetPosition(new Rect(position, new Vector2(EffectWidth, EffectHeight)));
            AddElement(effectView);
            _effectViews.Add(effect, effectView);
        }

        private void CreateStackDraft(Vector2 position)
        {
            EffectArrayStackView stack = new(null);
            stack.SetPosition(new Rect(position, new Vector2(ContainerWidth, ContainerHeight)));
            AddElement(stack);
            _stackViews.Add(stack);
        }

        private bool TryBindStack(Edge edge)
        {
            if (edge?.input?.node is not EffectArrayStackView { IsBound: false } stack ||
                edge.output == null)
            {
                return false;
            }

            EffectGraphContainerModel container = GetOutputContainer(edge.output);
            if (container == null || _containerViews.ContainsKey(container))
                return false;

            stack.Bind(container);
            _layout.Containers.RemoveAll(item => item != null && string.Equals(item.Path, container.Path, StringComparison.Ordinal));
            _layout.Containers.Add(new EffectGraphContainerLayout
            {
                Path = container.Path,
                Position = stack.GetPosition().position,
                Expanded = stack.expanded,
            });
            _window.CaptureLayout(_layout);
            _window.MarkDirty();
            _window.ScheduleGraphRebuild();
            return true;
        }

        private EffectGraphContainerModel GetOutputContainer(Port output)
        {
            if (output.node is EffectGraphEntryView)
                return _model.Root;

            if (output.node is EffectNodeView effect && effect.IsBound &&
                effect.TryGetOutputField(output, out FieldInfo field))
            {
                return _model.GetNestedContainer(effect.Effect, field);
            }

            return null;
        }

        private void TryInsertMovedEffect(EffectNodeView effectView)
        {
            EffectArrayStackView targetStack = FindDropTarget(effectView);
            if (targetStack?.Container == null)
            {
                if (effectView.Container != null)
                    _window.ScheduleGraphRebuild();
                return;
            }

            EffectGraphContainerModel target = targetStack.Container;
            int targetIndex = GetDropIndex(target, effectView);
            if (effectView.Container == null)
            {
                if (_model.InsertEffect(target, effectView.Effect, targetIndex))
                    _window.ScheduleGraphRebuild();
                return;
            }

            int sourceIndex = Array.IndexOf(effectView.Container.Effects, effectView.Effect);
            if (sourceIndex >= 0 && _model.MoveEffect(effectView.Container, sourceIndex, target, targetIndex))
                _window.ScheduleGraphRebuild();
        }

        private EffectArrayStackView FindDropTarget(EffectNodeView effectView)
        {
            EffectArrayStackView target = null;
            float smallestArea = float.MaxValue;
            Vector2 center = effectView.worldBound.center;
            foreach ((EffectGraphContainerModel _, EffectArrayStackView stack) in _containerViews)
            {
                Rect bounds = stack.worldBound;
                if (!bounds.Contains(center))
                    continue;

                float area = bounds.width * bounds.height;
                if (area >= smallestArea)
                    continue;

                smallestArea = area;
                target = stack;
            }

            return target;
        }

        private int GetDropIndex(EffectGraphContainerModel target, EffectNodeView movingEffect)
        {
            List<EffectData> siblings = new(target.Effects.Where(effect => !ReferenceEquals(effect, movingEffect.Effect)));
            float x = movingEffect.worldBound.center.x;
            for (int index = 0; index < siblings.Count; index++)
            {
                if (_effectViews.TryGetValue(siblings[index], out EffectNodeView sibling) && x < sibling.worldBound.center.x)
                    return index;
            }

            return siblings.Count;
        }

        private bool ShouldShowContainer(EffectGraphContainerModel container)
        {
            return container.IsRoot ||
                   container.Effects.Length > 0 ||
                   _layout.Containers.Any(item => item != null && string.Equals(item.Path, container.Path, StringComparison.Ordinal));
        }

        private void UpdateContainerPosition(EffectArrayStackView view)
        {
            for (int index = 0; index < _layout.Containers.Count; index++)
            {
                if (!string.Equals(_layout.Containers[index].Path, view.Container.Path, StringComparison.Ordinal))
                    continue;

                _layout.Containers[index].Position = view.GetPosition().position;
                return;
            }
        }

        private static bool IsSourceOutput(Port port)
        {
            return port != null && port.direction == Direction.Output &&
                   (port.node is EffectGraphEntryView || port.node is EffectNodeView { IsBound: true });
        }
    }

    internal sealed class EffectGraphEntryView : TopBottomPortNode
    {
        public EffectGraphEntryView()
        {
            title = "Entry";
            capabilities = Capabilities.Movable | Capabilities.Selectable;
            Output = CreateBottomOutput("Effects", Port.Capacity.Single, typeof(bool));
        }

        public Port Output { get; }
    }

    internal sealed class EffectArrayStackView : StackNode
    {
        private const float PortStripHeight = 20f;

        public EffectArrayStackView(EffectGraphContainerModel container)
        {
            Container = container;
            title = container == null ? "Effects" : $"{container.DisplayName} ({container.Effects.Length})";
            style.width = EffectGraphAutoLayout.GetWidth(container?.Effects.Length ?? 0);
            // Effects in the same array are ordered siblings: read left-to-right, never wrap.
            contentContainer.style.flexDirection = FlexDirection.Row;
            contentContainer.style.flexWrap = Wrap.NoWrap;
            contentContainer.style.alignItems = Align.FlexStart;
            contentContainer.style.paddingLeft = EffectGraphAutoLayout.ContainerPadding * 0.5f;
            contentContainer.style.paddingRight = EffectGraphAutoLayout.ContainerPadding * 0.5f;
            // Built-in separators and insertion slots are vertical; use our horizontal slots.
            VisualElement separators = this.Q("stackSeparatorContainer");
            if (separators != null)
                separators.style.display = DisplayStyle.None;

            inputContainer.RemoveFromHierarchy();
            outputContainer.RemoveFromHierarchy();
            VisualElement topPortContainer = new()
            {
                style =
                {
                    minHeight = PortStripHeight,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                },
            };
            Input = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            Input.portName = "Input";
            topPortContainer.Add(Input);
            mainContainer.Insert(0, topPortContainer);

            capabilities = Capabilities.Movable | Capabilities.Selectable;
            RefreshPorts();
            RefreshExpandedState();
        }

        public EffectGraphContainerModel Container { get; private set; }

        public bool IsBound => Container != null;

        public Port Input { get; }

        public void Bind(EffectGraphContainerModel container)
        {
            Container = container;
            title = $"{container.DisplayName} ({container.Effects.Length})";
        }

        public override int GetInsertionIndex(Vector2 worldPosition)
        {
            if (!worldBound.Contains(worldPosition)) return -1;
            int index = 0;
            foreach (EffectNodeView effect in Children().OfType<EffectNodeView>())
            {
                if (worldPosition.x < effect.worldBound.center.x) return index;
                index++;
            }
            return index;
        }
    }

    internal sealed class EffectNodeView : TopBottomPortNode
    {
        private readonly Dictionary<FieldInfo, Port> _outputs = new();
        private readonly Dictionary<Port, FieldInfo> _outputFields = new();

        public EffectNodeView(EffectData effect, EffectGraphContainerModel container, EffectGraphModel model, EffectGraphView graphView)
        {
            Effect = effect;
            Container = container;
            int sequence = container == null ? -1 : Array.IndexOf(container.Effects, effect);
            title = (sequence >= 0 ? $"{sequence + 1}. " : string.Empty) + EffectGraphTypeRegistry.GetDisplayName(effect);
            style.width = EffectGraphAutoLayout.EffectWidth;
            style.flexShrink = 0f;
            style.minHeight = 100f;
            titleContainer.style.backgroundColor = EffectGraphTypeRegistry.GetColor(effect);

            foreach (FieldInfo field in model.GetNestedEffectArrayFields(effect))
            {
                Port output = CreateBottomOutput(EditorLabelUtility.GetLabel(field), Port.Capacity.Single, typeof(bool));
                _outputs.Add(field, output);
                _outputFields.Add(output, field);
            }

            capabilities = Capabilities.Movable | Capabilities.Selectable | Capabilities.Deletable;
            RegisterCallback<MouseDownEvent>(_ => graphView.SelectEffect(effect));
        }

        public EffectData Effect { get; }

        public EffectGraphContainerModel Container { get; }

        public bool IsBound => Container != null;

        public bool TryGetOutput(FieldInfo field, out Port output)
        {
            return _outputs.TryGetValue(field, out output);
        }

        public bool TryGetOutputField(Port output, out FieldInfo field)
        {
            return _outputFields.TryGetValue(output, out field);
        }
    }
}
