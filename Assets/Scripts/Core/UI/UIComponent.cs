using System;
using System.Collections.Generic;
using CrystalMagic.UI;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// UI 管理组件
    /// 全局单例，负责分组创建、注册、路由和每帧更新
    /// </summary>
    public class UIComponent : GameComponent<UIComponent>
    {
        private const string DefaultGroupName = "Default";
        private const string ConfigPath = "Assets/Config/ui_config.json";

        private Dictionary<string, UIGroup> _groups = new();
        private Dictionary<string, string> _uiNameToGroupName = new();
        private Dictionary<UIBase, UIMvcContext> _mvcContexts = new();
        private Dictionary<string, Type> _typeCache = new();
        private UIGroupConfig _config;

        public override int Priority => 15;

        public override void Initialize()
        {
            base.Initialize();

            // 从固定路径加载配置
            LoadConfigFromPath();

            // 确保存在默认分组
            EnsureDefaultGroupExists();

            // 通过 CameraComponent 获取相机，它比 UIComponent(15) 优先级更高(13)，确保已初始化
            Camera uiCamera = CameraComponent.Instance.Current;
            if (uiCamera != null)
            {
                ApplyCameraToGroups(uiCamera);
            }

            if (InputComponent.Instance != null)
            {
                InputComponent.Instance.OnEscape += HandleEscape;
            }
        }

        private void LoadConfigFromPath()
        {
            #if UNITY_EDITOR
            TextAsset configAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (configAsset != null)
            {
                _config = UIConfigLoader.LoadFromJson(configAsset.text);
                if (_config != null)
                {
                    CreateGroupsFromConfig();
                }
            }
            #endif
        }

        private void ApplyCameraToGroups(Camera camera)
        {
            foreach (var group in _groups.Values)
            {
                group.GetComponent<Canvas>().worldCamera = camera;
            }
        }

        private void Update()
        {
            // 每帧更新所有分组
            foreach (var group in _groups.Values)
            {
                group.Tick();
            }
        }

        /// <summary>
        /// 从配置创建分组
        /// </summary>
        private void CreateGroupsFromConfig()
        {
            foreach (var entry in _config.groups)
            {
                CreateGroup(entry);
            }
        }

        /// <summary>
        /// 创建单个分组
        /// </summary>
        private void CreateGroup(UIGroupEntry entry)
        {
            GameObject groupObj = new GameObject(entry.groupName);
            groupObj.transform.SetParent(transform);

            UIGroup group = null;
            switch (entry.groupType)
            {
                case UIGroupType.Stack:
                    group = groupObj.AddComponent<StackUIGroup>();
                    break;
                case UIGroupType.Queue:
                    group = groupObj.AddComponent<QueueUIGroup>();
                    break;
                case UIGroupType.List:
                    group = groupObj.AddComponent<ListUIGroup>();
                    break;
            }

            if (group != null)
            {
                RegisterGroup(entry.groupName, group, entry.uiNames);
            }
        }

        /// <summary>
        /// 确保存在默认分组
        /// </summary>
        private void EnsureDefaultGroupExists()
        {
            if (_groups.ContainsKey(DefaultGroupName))
                return;

            GameObject groupObj = new GameObject(DefaultGroupName);
            groupObj.transform.SetParent(transform);

            StackUIGroup group = groupObj.AddComponent<StackUIGroup>();
            RegisterGroup(DefaultGroupName, group);
        }

        /// <summary>
        /// 注册分组
        /// </summary>
        public void RegisterGroup(string groupName, UIGroup group, List<string> uiNames = null)
        {
            if (string.IsNullOrEmpty(groupName))
                return;

            _groups[groupName] = group;

            if (uiNames != null)
            {
                foreach (var uiName in uiNames)
                {
                    _uiNameToGroupName[uiName] = groupName;
                }
            }
        }

        /// <summary>
        /// 显示 UI
        /// </summary>
        public void ShowUI(UIBase panel)
        {
            if (panel == null)
                return;

            string groupName = GetGroupNameByUIName(panel.GetType().Name);
            if (string.IsNullOrEmpty(groupName))
                groupName = DefaultGroupName;

            ShowUI(groupName, panel);
        }

        /// <summary>
        /// 显示 UI（按组名）
        /// </summary>
        public void ShowUI(string groupName, UIBase panel)
        {
            if (panel == null)
                return;

            if (_groups.TryGetValue(groupName, out UIGroup group))
            {
                UIMvcContext context = GetOrCreateMvcContext(panel);
                if (context != null)
                {
                    context.Detach();
                    context.GroupName = groupName;
                }

                group.ShowUI(panel);
            }
        }

        public T Open<T>() where T : UIBase
        {
            return Open(typeof(T).Name) as T;
        }

        public T Open<T>(object data) where T : UIBase
        {
            return Open(typeof(T).Name, data) as T;
        }

        public UIBase Open(string uiName)
        {
            return Open(uiName, null);
        }

        public UIBase Open(string uiName, object data)
        {
            if (string.IsNullOrEmpty(uiName))
                return null;

            GameObject uiInstance = PoolComponent.Instance.Get(AssetPathHelper.GetUIAsset(uiName));
            if (uiInstance == null)
            {
                Debug.LogError($"[UIComponent] Failed to open UI: {uiName}");
                return null;
            }

            UIBase panel = uiInstance.GetComponent<UIBase>();
            if (panel == null)
            {
                Debug.LogError($"[UIComponent] UI prefab '{uiName}' missing UIBase component");
                PoolComponent.Instance.Release(uiInstance);
                return null;
            }

            ApplyOpenData(GetOrCreateMvcContext(panel), data);
            ShowUI(panel);
            return panel;
        }

        public T OpenChild<T>(UIBase parent) where T : UIBase
        {
            return OpenChild(typeof(T).Name, parent) as T;
        }

        public T OpenChild<T>(UIBase parent, object data) where T : UIBase
        {
            return OpenChild(typeof(T).Name, parent, data) as T;
        }

        public UIBase OpenChild(string uiName, UIBase parent)
        {
            return OpenChild(uiName, parent, null);
        }

        public UIBase OpenChild(string uiName, UIBase parent, object data)
        {
            if (parent == null)
                return Open(uiName, data);

            if (string.IsNullOrEmpty(uiName))
                return null;

            UIMvcContext parentContext = GetOrCreateMvcContext(parent);
            if (parentContext == null)
            {
                Debug.LogError($"[UIComponent] Failed to open child UI '{uiName}', parent context missing");
                return null;
            }

            string groupName = string.IsNullOrEmpty(parentContext.GroupName) ? DefaultGroupName : parentContext.GroupName;
            if (!_groups.TryGetValue(groupName, out UIGroup group))
            {
                Debug.LogError($"[UIComponent] Failed to open child UI '{uiName}', group '{groupName}' missing");
                return null;
            }

            GameObject uiInstance = PoolComponent.Instance.Get(AssetPathHelper.GetUIAsset(uiName));
            if (uiInstance == null)
            {
                Debug.LogError($"[UIComponent] Failed to open child UI: {uiName}");
                return null;
            }

            UIBase panel = uiInstance.GetComponent<UIBase>();
            if (panel == null)
            {
                Debug.LogError($"[UIComponent] UI prefab '{uiName}' missing UIBase component");
                PoolComponent.Instance.Release(uiInstance);
                return null;
            }

            group.AttachPanel(panel);
            UIMvcContext childContext = GetOrCreateMvcContext(panel);
            if (childContext == null)
            {
                PoolComponent.Instance.Release(uiInstance);
                return null;
            }

            childContext.GroupName = groupName;
            childContext.AttachTo(parentContext);
            ApplyOpenData(childContext, data);
            OpenChildPanel(childContext, parent.gameObject.activeSelf);
            RefreshGroupSortingOrders(group);
            return panel;
        }

        /// <summary>
        /// 关闭 UI
        /// </summary>
        public void CloseUI(UIBase panel)
        {
            if (panel == null)
                return;

            if (_mvcContexts.TryGetValue(panel, out UIMvcContext context) && context.Parent != null)
            {
                string childGroupName = string.IsNullOrEmpty(context.GroupName) ? DefaultGroupName : context.GroupName;
                ReleaseContextTree(context);
                RefreshGroupSortingOrders(childGroupName);
                return;
            }

            string groupName = GetGroupNameByUIName(panel.GetType().Name);
            if (string.IsNullOrEmpty(groupName))
                groupName = DefaultGroupName;

            CloseUI(groupName, panel);
        }

        /// <summary>
        /// 关闭 UI（按组名）
        /// </summary>
        public void CloseUI(string groupName, UIBase panel)
        {
            if (panel == null)
                return;

            if (_mvcContexts.TryGetValue(panel, out UIMvcContext context) && context.Parent != null)
            {
                string childGroupName = string.IsNullOrEmpty(context.GroupName) ? DefaultGroupName : context.GroupName;
                ReleaseContextTree(context);
                RefreshGroupSortingOrders(childGroupName);
                return;
            }

            if (_groups.TryGetValue(groupName, out UIGroup group))
            {
                group.CloseUI(panel);
            }
        }

        public void ReleaseUI(UIBase panel)
        {
            if (panel == null)
                return;

            if (_mvcContexts.TryGetValue(panel, out UIMvcContext context) && context.Parent != null)
            {
                string childGroupName = string.IsNullOrEmpty(context.GroupName) ? DefaultGroupName : context.GroupName;
                ReleaseContextTree(context);
                RefreshGroupSortingOrders(childGroupName);
                return;
            }

            CloseUI(panel);
            DisconnectMvc(panel);
            PoolComponent.Instance.Release(panel.gameObject);
        }

        /// <summary>
        /// 获取 UI 所属的组名
        /// </summary>
        public string GetGroupNameByUIName(string uiName)
        {
            _uiNameToGroupName.TryGetValue(uiName, out string groupName);
            return groupName;
        }

        /// <summary>
        /// 获取分组
        /// </summary>
        public T GetGroup<T>(string groupName) where T : UIGroup
        {
            if (_groups.TryGetValue(groupName, out UIGroup group))
            {
                return group as T;
            }
            return null;
        }

        public UIBase GetParent(UIBase child)
        {
            if (child == null)
                return null;

            return _mvcContexts.TryGetValue(child, out UIMvcContext context) ? context.Parent?.Panel : null;
        }

        public IReadOnlyList<UIBase> GetChildren(UIBase parent)
        {
            if (parent == null)
                return Array.Empty<UIBase>();

            if (!_mvcContexts.TryGetValue(parent, out UIMvcContext context) || context.Children.Count == 0)
                return Array.Empty<UIBase>();

            List<UIBase> children = new(context.Children.Count);
            foreach (UIMvcContext childContext in context.Children)
            {
                children.Add(childContext.Panel);
            }

            return children;
        }

        internal void OpenRootPanel(UIBase panel)
        {
            if (panel == null)
                return;

            UIMvcContext context = GetOrCreateMvcContext(panel);
            if (context == null)
            {
                panel.gameObject.SetActive(true);
                panel.OnOpen();
                return;
            }

            OpenPanel(context);
        }

        internal void CloseRootPanel(UIBase panel)
        {
            if (panel == null)
                return;

            if (!_mvcContexts.TryGetValue(panel, out UIMvcContext context))
            {
                panel.OnClose();
                panel.gameObject.SetActive(false);
                return;
            }

            ReleaseChildContexts(context);
            ClosePanel(context);
        }

        internal void CoverPanelTree(UIBase panel)
        {
            if (panel == null)
                return;

            if (!_mvcContexts.TryGetValue(panel, out UIMvcContext context))
            {
                panel.OnCovered();
                panel.gameObject.SetActive(false);
                return;
            }

            panel.OnCovered();
            SetTreeActive(context, false);
        }

        internal void UncoverPanelTree(UIBase panel)
        {
            if (panel == null)
                return;

            if (!_mvcContexts.TryGetValue(panel, out UIMvcContext context))
            {
                panel.gameObject.SetActive(true);
                panel.OnUncovered();
                return;
            }

            SetTreeActive(context, true);
            panel.OnUncovered();
        }

        internal void RefreshGroupSortingOrders(UIGroup group)
        {
            if (group == null)
                return;

            int order = group.BaseSortingOrder;
            foreach (UIBase rootPanel in group.Panels)
            {
                if (_mvcContexts.TryGetValue(rootPanel, out UIMvcContext context))
                {
                    order = ApplyTreeOrder(context, order);
                }
                else if (rootPanel != null)
                {
                    rootPanel.Canvas.sortingOrder = order;
                    order++;
                }

                order += 100;
            }
        }

        private void RefreshGroupSortingOrders(string groupName)
        {
            if (_groups.TryGetValue(groupName, out UIGroup group))
            {
                RefreshGroupSortingOrders(group);
            }
        }

        private void OpenChildPanel(UIMvcContext context, bool active)
        {
            if (context == null)
                return;

            OpenPanel(context);
            if (!active)
            {
                SetTreeActive(context, false);
            }
        }

        private void OpenPanel(UIMvcContext context)
        {
            if (context == null)
                return;

            context.Panel.gameObject.SetActive(true);
            if (!context.IsOpen)
            {
                context.Panel.OnOpen();
                context.Open();
                return;
            }

            SetTreeActive(context, true);
        }

        private void ApplyOpenData(UIMvcContext context, object data)
        {
            if (context == null || data == null)
                return;

            if (TryApplyOpenData(context.Model, data))
                return;

            if (TryApplyOpenData(context.Controller, data))
                return;

            TryApplyOpenData(context.Panel, data);
        }

        private bool TryApplyOpenData(object target, object data)
        {
            if (target == null || data == null)
                return false;

            Type dataType = data.GetType();
            Type receiverType = typeof(IUIOpenDataReceiver<>).MakeGenericType(dataType);
            if (!receiverType.IsInstanceOfType(target))
                return false;

            receiverType.GetMethod(nameof(IUIOpenDataReceiver<object>.SetOpenData))?.Invoke(target, new[] { data });
            return true;
        }

        private void ClosePanel(UIMvcContext context)
        {
            if (context == null)
                return;

            if (context.IsOpen)
            {
                context.Panel.OnClose();
                context.Close();
            }

            context.Panel.gameObject.SetActive(false);
        }

        private void ReleaseChildContexts(UIMvcContext context)
        {
            if (context == null || context.Children.Count == 0)
                return;

            List<UIMvcContext> children = new(context.Children);
            foreach (UIMvcContext child in children)
            {
                ReleaseContextTree(child);
            }
        }

        private void ReleaseContextTree(UIMvcContext context)
        {
            if (context == null)
                return;

            List<UIMvcContext> children = new(context.Children);
            foreach (UIMvcContext child in children)
            {
                ReleaseContextTree(child);
            }

            context.Children.Clear();

            if (context.IsOpen)
            {
                context.Panel.OnClose();
                context.Close();
            }

            context.Panel.gameObject.SetActive(false);
            context.Detach();
            _mvcContexts.Remove(context.Panel);
            context.Dispose();
            PoolComponent.Instance.Release(context.Panel.gameObject);
        }

        private void SetTreeActive(UIMvcContext context, bool active)
        {
            if (context == null)
                return;

            context.Panel.gameObject.SetActive(active);
            foreach (UIMvcContext child in context.Children)
            {
                SetTreeActive(child, active);
            }
        }

        private int ApplyTreeOrder(UIMvcContext context, int order)
        {
            if (context == null)
                return order;

            context.Panel.Canvas.sortingOrder = order;
            int nextOrder = order + 1;

            foreach (UIMvcContext child in context.Children)
            {
                nextOrder = ApplyTreeOrder(child, nextOrder);
            }

            return nextOrder;
        }

        private UIMvcContext GetOrCreateMvcContext(UIBase panel)
        {
            if (panel == null)
                return null;

            if (_mvcContexts.TryGetValue(panel, out UIMvcContext existingContext))
                return existingContext;

            panel.EnsureInitialized();

            Type viewType = panel.GetType();
            Type modelType = ResolveType($"CrystalMagic.UI.{viewType.Name}Model", typeof(UIModelBase))
                ?? ResolveType($"{viewType.Name}Model", typeof(UIModelBase));
            Type controllerType = ResolveType($"CrystalMagic.UI.{viewType.Name}Controller", typeof(UIControllerBase))
                ?? ResolveType($"{viewType.Name}Controller", typeof(UIControllerBase));

            try
            {
                UIModelBase model = null;
                UIControllerBase controller = null;

                if (controllerType != null)
                {
                    modelType ??= typeof(EmptyUIModel);
                    model = Activator.CreateInstance(modelType) as UIModelBase;
                    controller = Activator.CreateInstance(controllerType, panel, model) as UIControllerBase;

                    if (model == null || controller == null)
                    {
                        Debug.LogError($"[UIComponent] Failed to create MVC context for {viewType.Name}");
                        return null;
                    }
                }

                UIMvcContext context = new UIMvcContext(panel, model, controller);
                _mvcContexts[panel] = context;
                return context;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIComponent] Failed to bind MVC for {viewType.Name}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private void DisconnectMvc(UIBase panel)
        {
            if (!_mvcContexts.TryGetValue(panel, out UIMvcContext context))
                return;

            context.Detach();
            context.Dispose();
            _mvcContexts.Remove(panel);
        }

        private Type ResolveType(string typeName, Type requiredBaseType)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            if (_typeCache.TryGetValue(typeName, out Type cachedType))
                return cachedType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null && requiredBaseType.IsAssignableFrom(type))
                {
                    _typeCache[typeName] = type;
                    return type;
                }

                try
                {
                    foreach (Type candidate in assembly.GetTypes())
                    {
                        if (candidate.Name == typeName && requiredBaseType.IsAssignableFrom(candidate))
                        {
                            _typeCache[typeName] = candidate;
                            return candidate;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public override void Cleanup()
        {
            if (InputComponent.Instance != null)
            {
                InputComponent.Instance.OnEscape -= HandleEscape;
            }

            foreach (UIMvcContext context in _mvcContexts.Values)
            {
                context.Dispose();
            }

            _mvcContexts.Clear();
            _groups.Clear();
            _uiNameToGroupName.Clear();
            _typeCache.Clear();
            base.Cleanup();
        }

        private void HandleEscape()
        {
            UIBase panel = GetTopmostEscapeClosablePanel();
            panel?.Close();
        }

        private UIBase GetTopmostEscapeClosablePanel()
        {
            UIBase selected = null;
            int maxSortingOrder = int.MinValue;

            foreach (UIMvcContext context in _mvcContexts.Values)
            {
                UIBase panel = context.Panel;
                if (panel == null || !panel.gameObject.activeInHierarchy || !panel.CanCloseByEscape)
                {
                    continue;
                }

                int sortingOrder = panel.Canvas != null ? panel.Canvas.sortingOrder : int.MinValue;
                if (selected == null || sortingOrder > maxSortingOrder)
                {
                    selected = panel;
                    maxSortingOrder = sortingOrder;
                }
            }

            return selected;
        }

        private sealed class UIMvcContext : IDisposable
        {
            private readonly UIModelBase _model;
            private readonly UIControllerBase _controller;

            public UIMvcContext(UIBase panel, UIModelBase model, UIControllerBase controller)
            {
                Panel = panel;
                _model = model;
                _controller = controller;
            }

            public UIBase Panel { get; }
            public UIModelBase Model => _model;
            public UIControllerBase Controller => _controller;
            public string GroupName { get; set; }
            public UIMvcContext Parent { get; private set; }
            public List<UIMvcContext> Children { get; } = new();
            public bool IsOpen { get; private set; }

            public void AttachTo(UIMvcContext parent)
            {
                if (Parent == parent)
                    return;

                Detach();
                Parent = parent;
                Parent?.Children.Add(this);
            }

            public void Detach()
            {
                if (Parent == null)
                    return;

                Parent.Children.Remove(this);
                Parent = null;
            }

            public void Open()
            {
                IsOpen = true;
                _controller?.Open();
            }

            public void Close()
            {
                IsOpen = false;
                _controller?.Close();
            }

            public void Dispose()
            {
                Detach();
                _controller?.Dispose();
                _model?.Dispose();
            }
        }
    }
}
