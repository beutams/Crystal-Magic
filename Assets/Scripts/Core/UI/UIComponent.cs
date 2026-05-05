using System;
using System.Collections.Generic;
using CrystalMagic.UI;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.Core {
    public enum UILifetime
    {
        SceneScoped,
        Persistent,
        Manual,
    }

    /// <summary>
    /// UI 管理组件
    /// 全局单例，负责分组创建、注册、路由和每帧更新
    /// </summary>
    public class UIComponent : GameComponent<UIComponent>
    {
        private const string DefaultGroupName = "Default";
        private const string ConfigPath = "Assets/Res/Config/ui_config.json";

        private Dictionary<string, UIGroup> _groups = new();
        private Dictionary<string, string> _uiNameToGroupName = new();
        private Dictionary<UIBase, UIMvcContext> _mvcContexts = new();
        private Dictionary<string, Type> _typeCache = new();
        private UIGroupConfig _config;
        private bool _uiInputLocked;
        private int _currentSceneScopeId;
        private string _currentSceneName = string.Empty;

        public event Action EscapeUnhandled;

        public override int Priority => 15;

        public override void Initialize()
        {
            base.Initialize();

            // 从固定路径加载配置
            LoadConfigFromPath();

            // 确保存在默认分组
            EnsureDefaultGroupExists();

            // 通过 CameraComponent 获取相机，它比 UIComponent(15) 优先级更高(13)，确保已初始化
            RefreshUICamera(CameraComponent.Instance.Current);

            if (InputComponent.Instance != null)
            {
                InputComponent.Instance.OnEscape += HandleEscape;
            }

            if (EventComponent.Instance != null)
            {
                EventComponent.Instance.Subscribe<UISceneScopeChangedEvent>(HandleSceneScopeChanged);
                EventComponent.Instance.Subscribe<GameGateChangedEvent>(HandleGameGateChanged);
            }

            _uiInputLocked = GameGateComponent.Instance.IsUIInputLocked;
            ApplyUIInputState();
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

        private Vector2 GetGroupReferenceResolution()
        {
            if (_config == null)
                return new Vector2(2560, 1440);

            return new Vector2(_config.referenceResolutionWidth, _config.referenceResolutionHeight);
        }

        private CanvasScaler.ScreenMatchMode GetGroupScreenMatchMode()
        {
            return _config != null ? _config.screenMatchMode : CanvasScaler.ScreenMatchMode.Expand;
        }

        private float GetGroupPlaneDistance()
        {
            return _config != null ? Mathf.Max(0.01f, _config.planeDistance) : 1f;
        }

        public float GetHoverInfoDelaySeconds()
        {
            return _config != null ? Mathf.Max(0f, _config.hoverInfoDelaySeconds) : 2f;
        }

        public float GetUnitHealthBarShowSeconds()
        {
            return _config != null ? Mathf.Max(0f, _config.unitHealthBarShowSeconds) : 3f;
        }

        public Vector2 GetReferenceResolution()
        {
            return GetGroupReferenceResolution();
        }

        public CanvasScaler.ScreenMatchMode GetScreenMatchMode()
        {
            return GetGroupScreenMatchMode();
        }

        public float GetPlaneDistance()
        {
            return GetGroupPlaneDistance();
        }

        public void RefreshUICamera(Camera camera)
        {
            foreach (var group in _groups.Values)
            {
                Canvas[] canvases = group.GetComponentsInChildren<Canvas>(true);
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].worldCamera = camera;
                }
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
            GameObject groupObj = CreateGroupObject(entry.groupName);

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
                group.ConfigureGroup(entry.groupName, entry.order);
                group.ConfigureCanvasSettings(GetGroupReferenceResolution(), GetGroupScreenMatchMode(), GetGroupPlaneDistance());
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

            GameObject groupObj = CreateGroupObject(DefaultGroupName);

            StackUIGroup group = groupObj.AddComponent<StackUIGroup>();
            group.ConfigureGroup(DefaultGroupName, 0);
            group.ConfigureCanvasSettings(GetGroupReferenceResolution(), GetGroupScreenMatchMode(), GetGroupPlaneDistance());
            RegisterGroup(DefaultGroupName, group);
        }

        private GameObject CreateGroupObject(string groupName)
        {
            GameObject groupObj = new GameObject(groupName, typeof(RectTransform));
            RectTransform rectTransform = groupObj.GetComponent<RectTransform>();
            rectTransform.SetParent(transform, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            return groupObj;
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
                ApplyUIInputState();
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
            childContext.Lifetime = parentContext.Lifetime;
            childContext.SceneScopeId = parentContext.SceneScopeId;
            childContext.SceneName = parentContext.SceneName;
            childContext.AttachTo(parentContext);
            ApplyOpenData(childContext, data);
            OpenChildPanel(childContext, parent.gameObject.activeSelf);
            RefreshGroupSortingOrders(group);
            ApplyUIInputState();
            return panel;
        }

        public void SetLifetime(UIBase panel, UILifetime lifetime)
        {
            UIMvcContext context = GetOrCreateMvcContext(panel);
            if (context == null)
                return;

            context.Lifetime = lifetime;
            if (lifetime == UILifetime.SceneScoped)
            {
                AssignContextToCurrentSceneScope(context);
                return;
            }

            context.SceneScopeId = -1;
            context.SceneName = string.Empty;
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

        public bool IsManaged(UIBase panel)
        {
            return panel != null && _mvcContexts.ContainsKey(panel);
        }

        public string GetResourceOwnerKey(Component component)
        {
            if (component == null)
                return string.Empty;

            UIBase panel = component.GetComponentInParent<UIBase>(true);
            return GetResourceOwnerKey(panel);
        }

        public string GetResourceOwnerKey(UIBase panel)
        {
            return panel != null && _mvcContexts.TryGetValue(panel, out UIMvcContext context)
                ? context.ResourceOwnerKey
                : string.Empty;
        }

        public bool HasActiveSceneScopedPanel(string sceneName)
        {
            return HasActiveSceneScopedPanel(sceneName, null);
        }

        public bool HasActiveSceneScopedPanel(string sceneName, params string[] excludedUiNames)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            HashSet<string> excludedNames = null;
            if (excludedUiNames != null && excludedUiNames.Length > 0)
            {
                excludedNames = new HashSet<string>(excludedUiNames, StringComparer.Ordinal);
            }

            foreach (UIMvcContext context in _mvcContexts.Values)
            {
                if (context == null
                    || context.Lifetime != UILifetime.SceneScoped
                    || context.SceneName != sceneName
                    || !context.IsOpen
                    || context.Panel == null
                    || !context.Panel.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (excludedNames != null && excludedNames.Contains(context.Panel.GetType().Name))
                {
                    continue;
                }

                return true;
            }

            return false;
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

            bool removedFromGroup = context.Parent == null
                && RemoveRootPanelFromGroup(context.Panel, context.GroupName);

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
            ResourceComponent.Instance.ReleaseOwner(context.ResourceOwnerKey);
            EventComponent.Instance.ReleaseOwner(context.ResourceOwnerKey);
            PoolComponent.Instance.ReleaseOwner(context.ResourceOwnerKey);
            context.Dispose();
            PoolComponent.Instance.Release(context.Panel.gameObject);

            if (removedFromGroup)
            {
                RefreshGroupSortingOrders(context.GroupName);
            }
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
                AssignContextToCurrentSceneScope(context);
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
            ResourceComponent.Instance.ReleaseOwner(context.ResourceOwnerKey);
            EventComponent.Instance.ReleaseOwner(context.ResourceOwnerKey);
            PoolComponent.Instance.ReleaseOwner(context.ResourceOwnerKey);
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
            if (EventComponent.Instance != null)
            {
                EventComponent.Instance.Unsubscribe<UISceneScopeChangedEvent>(HandleSceneScopeChanged);
                EventComponent.Instance.Unsubscribe<GameGateChangedEvent>(HandleGameGateChanged);
            }

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

        private void HandleSceneScopeChanged(UISceneScopeChangedEvent gameEvent)
        {
            int previousSceneScopeId = _currentSceneScopeId;
            _currentSceneScopeId++;
            _currentSceneName = gameEvent.SceneName ?? string.Empty;
            ReleaseSceneScope(previousSceneScopeId);
        }

        private void AssignContextToCurrentSceneScope(UIMvcContext context)
        {
            if (context == null || context.Lifetime != UILifetime.SceneScoped)
                return;

            context.SceneScopeId = _currentSceneScopeId;
            context.SceneName = _currentSceneName;
        }

        private void ReleaseSceneScope(int sceneScopeId)
        {
            if (sceneScopeId < 0)
                return;

            List<UIMvcContext> rootsToRelease = new();
            HashSet<string> affectedGroupNames = new();
            foreach (UIMvcContext context in new List<UIMvcContext>(_mvcContexts.Values))
            {
                if (context.Parent != null
                    || context.Lifetime != UILifetime.SceneScoped
                    || context.SceneScopeId != sceneScopeId)
                {
                    continue;
                }

                rootsToRelease.Add(context);
                string groupName = string.IsNullOrEmpty(context.GroupName) ? DefaultGroupName : context.GroupName;
                affectedGroupNames.Add(groupName);
            }

            for (int i = 0; i < rootsToRelease.Count; i++)
            {
                ReleaseContextTree(rootsToRelease[i]);
            }

            foreach (string groupName in affectedGroupNames)
            {
                RefreshGroupSortingOrders(groupName);
            }

            ApplyUIInputState();
        }

        private bool RemoveRootPanelFromGroup(UIBase panel, string groupName)
        {
            if (panel == null)
                return false;

            string resolvedGroupName = string.IsNullOrEmpty(groupName) ? DefaultGroupName : groupName;
            if (!_groups.TryGetValue(resolvedGroupName, out UIGroup group))
                return false;

            return group.RemovePanelSilently(panel);
        }

        private void HandleEscape()
        {
            if (_uiInputLocked)
                return;

            UIBase panel = GetTopmostEscapeClosablePanel();
            if (panel != null)
            {
                panel.Close();
                return;
            }

            EscapeUnhandled?.Invoke();
        }

        private void ApplyUIInputState()
        {
            bool enableInput = !_uiInputLocked;
            foreach (UIGroup group in _groups.Values)
            {
                if (group == null)
                    continue;

                GraphicRaycaster[] raycasters = group.GetComponentsInChildren<GraphicRaycaster>(true);
                for (int i = 0; i < raycasters.Length; i++)
                    raycasters[i].enabled = enableInput;
            }
        }

        private void HandleGameGateChanged(GameGateChangedEvent gameEvent)
        {
            if (gameEvent.GateType != GameGateType.UIInput || _uiInputLocked == gameEvent.IsLocked)
                return;

            _uiInputLocked = gameEvent.IsLocked;
            ApplyUIInputState();
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
                ResourceOwnerKey = $"UI:{panel.GetType().Name}:{panel.GetInstanceID()}";
            }

            public UIBase Panel { get; }
            public UIModelBase Model => _model;
            public UIControllerBase Controller => _controller;
            public string GroupName { get; set; }
            public string ResourceOwnerKey { get; }
            public UIMvcContext Parent { get; private set; }
            public List<UIMvcContext> Children { get; } = new();
            public bool IsOpen { get; private set; }
            public UILifetime Lifetime { get; set; } = UILifetime.SceneScoped;
            public int SceneScopeId { get; set; } = -1;
            public string SceneName { get; set; } = string.Empty;

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

    public class UnitHealthBarComponent : GameComponent<UnitHealthBarComponent>
    {
        private const string GroupName = "Bottom";
        private const string UnitHealthBarUIName = "UnitHealthBarUI";
        private const float WorldYOffset = 1.4f;

        private readonly Dictionary<Entity, ActiveBar> _activeBars = new();
        private readonly List<Entity> _cleanupEntities = new();

        private RectTransform _rootRect;
        private Camera _currentCamera;
        private bool _battleActive;

        public override int Priority => 16;

        public override void Initialize()
        {
            base.Initialize();
            EventComponent.Instance.Subscribe<UnitDamagedEvent>(HandleUnitDamaged);
        }

        public override void Cleanup()
        {
            EventComponent.Instance.Unsubscribe<UnitDamagedEvent>(HandleUnitDamaged);
            ReleaseAllBars();
            _rootRect = null;
            _currentCamera = null;
            _battleActive = false;
            base.Cleanup();
        }

        public void SetBattleActive(bool active)
        {
            if (_battleActive == active)
                return;

            _battleActive = active;
            if (!_battleActive)
            {
                ReleaseAllBars();
                return;
            }

            ResolveFloatingRoot();
        }

        private void LateUpdate()
        {
            if (!_battleActive || _activeBars.Count == 0)
                return;

            if (!ResolveFloatingRoot())
                return;

            UpdateBars();
        }

        private void HandleUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!_battleActive)
                return;

            if (!IsEnemyUnit(gameEvent.TargetEntity))
                return;

            ActiveBar bar = GetOrCreateBar(gameEvent.TargetEntity);
            if (bar == null)
                return;

            bar.HideAtTime = Time.time + UIComponent.Instance.GetUnitHealthBarShowSeconds();
            bar.View.SetHealth(gameEvent.CurrentHealth, gameEvent.MaxHealth);
            bar.View.SetVisible(true);
        }

        private bool ResolveFloatingRoot()
        {
            UIGroup group = UIComponent.Instance != null
                ? UIComponent.Instance.GetGroup<UIGroup>(GroupName)
                : null;
            if (group == null)
                return false;

            _rootRect = group.transform as RectTransform;
            Canvas canvas = group.GetComponent<Canvas>();
            _currentCamera = canvas != null ? canvas.worldCamera : CameraComponent.Instance.Current;
            return _rootRect != null && _currentCamera != null;
        }

        private ActiveBar GetOrCreateBar(Entity entity)
        {
            if (_activeBars.TryGetValue(entity, out ActiveBar existingBar) && existingBar.View != null)
                return existingBar;

            if (!ResolveFloatingRoot())
                return null;

            UnitHealthBarUI view = UIComponent.Instance.Open<UnitHealthBarUI>();
            if (view == null)
                return null;

            UIComponent.Instance.SetLifetime(view, UILifetime.Manual);
            view.PrepareForFloatingRoot(_rootRect);
            view.SetVisible(true);

            ActiveBar bar = new ActiveBar
            {
                Entity = entity,
                View = view,
            };
            _activeBars[entity] = bar;
            return bar;
        }

        private void UpdateBars()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || _rootRect == null || _currentCamera == null)
                return;

            EntityManager entityManager = world.EntityManager;
            _cleanupEntities.Clear();

            foreach (KeyValuePair<Entity, ActiveBar> pair in _activeBars)
            {
                ActiveBar bar = pair.Value;
                if (bar == null || bar.View == null)
                {
                    _cleanupEntities.Add(pair.Key);
                    continue;
                }

                if (Time.time >= bar.HideAtTime)
                {
                    _cleanupEntities.Add(pair.Key);
                    continue;
                }

                Entity entity = pair.Key;
                if (!entityManager.Exists(entity)
                    || !entityManager.HasComponent<LocalToWorld>(entity)
                    || !entityManager.HasComponent<UnitVitalityComponent>(entity))
                {
                    _cleanupEntities.Add(entity);
                    continue;
                }

                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
                bar.View.SetHealth(vitality.CurrentHealth, vitality.RealMaxHealth);

                LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(entity);
                Vector3 worldPosition = (Vector3)localToWorld.Position + Vector3.up * WorldYOffset;
                Vector3 screenPosition = _currentCamera.WorldToScreenPoint(worldPosition);
                if (screenPosition.z <= 0f)
                {
                    bar.View.SetVisible(false);
                    continue;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPosition, _currentCamera, out Vector2 localPoint))
                {
                    bar.View.SetAnchoredPosition(localPoint);
                    bar.View.SetVisible(true);
                }
            }

            for (int i = 0; i < _cleanupEntities.Count; i++)
            {
                ReleaseBar(_cleanupEntities[i]);
            }
        }

        private bool IsEnemyUnit(Entity entity)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            EntityManager entityManager = world.EntityManager;
            if (!entityManager.Exists(entity)
                || !entityManager.HasComponent<UnitVitalityComponent>(entity)
                || !entityManager.HasComponent<UnitFactionComponent>(entity))
            {
                return false;
            }

            return entityManager.GetComponentData<UnitFactionComponent>(entity).Value == UnitFactionType.Enemy;
        }

        private void ReleaseBar(Entity entity)
        {
            if (!_activeBars.TryGetValue(entity, out ActiveBar bar))
                return;

            _activeBars.Remove(entity);
            if (bar?.View != null)
                UIComponent.Instance.ReleaseUI(bar.View);
        }

        private void ReleaseAllBars()
        {
            foreach (KeyValuePair<Entity, ActiveBar> pair in _activeBars)
            {
                if (pair.Value?.View != null)
                    UIComponent.Instance.ReleaseUI(pair.Value.View);
            }

            _activeBars.Clear();
            _cleanupEntities.Clear();
        }

        private sealed class ActiveBar
        {
            public Entity Entity;
            public UnitHealthBarUI View;
            public float HideAtTime;
        }
    }
}
