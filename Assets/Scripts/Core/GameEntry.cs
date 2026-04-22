using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 游戏总入口管理器
    /// 职责：
    /// - 手动管理所有 IGameComponent
    /// - 按优先级初始化所有组件
    /// - 管理组件的生命周期
    /// </summary>
    public class GameEntry : Singleton<GameEntry>
    {
        private List<IGameComponent> _components = new();
        private bool _isInitialized = false;

        // 手动声明各管理模块属性
        public EventComponent EventComponent { get; private set; }
        public ResourceComponent ResourceComponent { get; private set; }
        public PoolComponent PoolComponent { get; private set; }
        public SceneComponent SceneComponent { get; private set; }
        public TransitionComponent TransitionComponent { get; private set; }
        public UIComponent UIComponent { get; private set; }
        public DataComponent DataComponent { get; private set; }
        public ConfigComponent ConfigComponent { get; private set; }
        public CameraComponent CameraComponent { get; private set; }
        public SaveDataComponent SaveDataComponent { get; private set; }
        public AudioComponent AudioComponent { get; private set; }
        public InputComponent InputComponent { get; private set; }
        public GameGateComponent GameGateComponent { get; private set; }
        public GameFlowComponent GameFlowComponent { get; private set; }
        private void Start()
        {
            if (!_isInitialized)
            {
                InitializeAllComponents();
                // 初始化完成后，转场进入主菜单场景
                GameFlowComponent.Instance.SetState<TransitionState>(new TransitionData
                {
                    TargetSceneName = "MainMenu",
                    TargetStateType = typeof(MainMenuState)
                });
            }
        }

        /// <summary>
        /// 初始化所有游戏组件
        /// </summary>
        public void InitializeAllComponents()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[GameEntry] Components already initialized!");
                return;
            }

            Debug.Log("[GameEntry] Initializing game components...");

            // 手动注册各组件
            RegisterComponent(EventComponent.Instance);
            RegisterComponent(GameGateComponent.Instance);
            RegisterComponent(ResourceComponent.Instance);
            RegisterComponent(PoolComponent.Instance);
            RegisterComponent(SceneComponent.Instance);
            RegisterComponent(TransitionComponent.Instance);
            RegisterComponent(UIComponent.Instance);
            RegisterComponent(DataComponent.Instance);
            RegisterComponent(ConfigComponent.Instance);
            RegisterComponent(CameraComponent.Instance);
            RegisterComponent(SaveDataComponent.Instance);
            RegisterComponent(AudioComponent.Instance);
            RegisterComponent(InputComponent.Instance);
            RegisterComponent(GameFlowComponent.Instance);

            // 按优先级排序
            _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            // 初始化每个组件
            foreach (var component in _components)
            {
                try
                {
                    Debug.Log($"[GameEntry] Initializing {component.GetType().Name} (Priority: {component.Priority})");
                    component.Initialize();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameEntry] Failed to initialize {component.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            _isInitialized = true;
            Debug.Log($"[GameEntry] All {_components.Count} components initialized successfully!");
        }

        /// <summary>
        /// 注册组件
        /// </summary>
        private void RegisterComponent(IGameComponent component)
        {
            if (component == null)
                return;

            _components.Add(component);

            // 存储到对应的属性
            if (component is EventComponent eventComponent)
                EventComponent = eventComponent;
            else if (component is ResourceComponent resourceComponent)
                ResourceComponent = resourceComponent;
            else if (component is PoolComponent poolComponent)
                PoolComponent = poolComponent;
            else if (component is SceneComponent sceneComponent)
                SceneComponent = sceneComponent;
            else if (component is TransitionComponent transitionComponent)
                TransitionComponent = transitionComponent;
            else if (component is UIComponent uiComponent)
                UIComponent = uiComponent;
            else if (component is DataComponent dataComponent)
                DataComponent = dataComponent;
            else if (component is ConfigComponent configComponent)
                ConfigComponent = configComponent;
            else if (component is CameraComponent cameraComponent)
                CameraComponent = cameraComponent;
            else if (component is SaveDataComponent saveDataComponent)
                SaveDataComponent = saveDataComponent;
            else if (component is AudioComponent audioComponent)
                AudioComponent = audioComponent;
            else if (component is InputComponent inputComponent)
                InputComponent = inputComponent;
            else if (component is GameGateComponent gameGateComponent)
                GameGateComponent = gameGateComponent;
            else if (component is GameFlowComponent gameFlowComponent)
                GameFlowComponent = gameFlowComponent;
        }

        /// <summary>
        /// 清理所有组件
        /// </summary>
        public void CleanupAllComponents()
        {
            Debug.Log("[GameEntry] Cleaning up all components...");

            // 反向顺序清理
            for (int i = _components.Count - 1; i >= 0; i--)
            {
                try
                {
                    _components[i].Cleanup();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameEntry] Error cleaning up {_components[i].GetType().Name}: {ex.Message}");
                }
            }

            _components.Clear();
            _isInitialized = false;
        }

        private void OnApplicationQuit()
        {
            CleanupAllComponents();
        }

        /// <summary>
        /// 获取初始化状态
        /// </summary>
        public bool IsInitialized => _isInitialized;
    }
}
