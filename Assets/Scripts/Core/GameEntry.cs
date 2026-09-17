using System.Collections.Generic;
using Server;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 游戏总入口管理器。
    /// </summary>
    public class GameEntry : Singleton<GameEntry>
    {
        private readonly List<IGameComponent> _components = new();
        private bool _isInitialized = false;

        public EventComponent EventComponent { get; private set; }
        public LocalLogComponent LocalLogComponent { get; private set; }
        public ResourceComponent ResourceComponent { get; private set; }
        public PoolComponent PoolComponent { get; private set; }
        public SceneComponent SceneComponent { get; private set; }
        public TransitionComponent TransitionComponent { get; private set; }
        public UIComponent UIComponent { get; private set; }
        public DataComponent DataComponent { get; private set; }
        public ConfigComponent ConfigComponent { get; private set; }
        public DebugComponent DebugComponent { get; private set; }
        public CameraComponent CameraComponent { get; private set; }
        public SaveDataComponent SaveDataComponent { get; private set; }
        public GameSettingsComponent GameSettingsComponent { get; private set; }
        public LocalizationComponent LocalizationComponent { get; private set; }
        public AudioComponent AudioComponent { get; private set; }
        public InputComponent InputComponent { get; private set; }
        public GameGateComponent GameGateComponent { get; private set; }
        public GameFlowComponent GameFlowComponent { get; private set; }
        public TimerComponent TimerComponent { get; private set; }
        public NetworkComponent NetworkComponent { get; private set; }

        protected override void Awake()
        {
            if (InitializeSingletonInstance(this))
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                InitializeAllComponents();

                if (NetworkComponent.Role != NetworkRole.Client)
                {
                    return;
                }

                // 初始化完成后进入主菜单。
                GameFlowComponent.Instance.BeginTransition(new TransitionData
                {
                    TargetSceneName = "MainMenu",
                    TargetStateType = typeof(MainMenuState),
                    TransitionUIName = "TransitionUI",
                    KeepCurrentMainScene = true,
                    ActiveSubSceneNames = System.Array.Empty<string>(),
                });
            }
        }

        /// <summary>
        /// 初始化所有游戏组件。
        /// </summary>
        public void InitializeAllComponents()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[GameEntry] Components already initialized!");
                return;
            }

            Debug.Log("[GameEntry] Initializing game components...");

            LocalLogComponent = LocalLogComponent.Instance;
            _components.Add(LocalLogComponent);

            NetworkComponent = Server.NetworkComponent.Instance;
            _components.Add(NetworkComponent);

            // Lobby 不会创建游戏世界，只保留网络与日志即可。Battle Server 仍需初始化
            // Config、Data、Scene 等完整组件，才能和客户端走同一套地图/实体生成流程。
            if (NetworkComponent.Role == NetworkRole.LobbyServer)
            {
                InitializeRegisteredComponents();
                return;
            }

            EventComponent = EventComponent.Instance;
            _components.Add(EventComponent);

            GameGateComponent = GameGateComponent.Instance;
            _components.Add(GameGateComponent);

            ResourceComponent = ResourceComponent.Instance;
            _components.Add(ResourceComponent);

            PoolComponent = PoolComponent.Instance;
            _components.Add(PoolComponent);

            SceneComponent = SceneComponent.Instance;
            _components.Add(SceneComponent);

            TransitionComponent = TransitionComponent.Instance;
            _components.Add(TransitionComponent);

            UIComponent = UIComponent.Instance;
            _components.Add(UIComponent);

            DataComponent = DataComponent.Instance;
            _components.Add(DataComponent);

            ConfigComponent = ConfigComponent.Instance;
            _components.Add(ConfigComponent);

            DebugComponent = DebugComponent.Instance;
            _components.Add(DebugComponent);

            CameraComponent = CameraComponent.Instance;
            _components.Add(CameraComponent);

            SaveDataComponent = SaveDataComponent.Instance;
            _components.Add(SaveDataComponent);

            GameSettingsComponent = GameSettingsComponent.Instance;
            _components.Add(GameSettingsComponent);

            LocalizationComponent = LocalizationComponent.Instance;
            _components.Add(LocalizationComponent);

            AudioComponent = AudioComponent.Instance;
            _components.Add(AudioComponent);

            InputComponent = InputComponent.Instance;
            _components.Add(InputComponent);

            GameFlowComponent = GameFlowComponent.Instance;
            _components.Add(GameFlowComponent);

            TimerComponent = TimerComponent.Instance;
            _components.Add(TimerComponent);

            InitializeRegisteredComponents();
        }

        private void InitializeRegisteredComponents()
        {
            _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));

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
        /// 清理所有组件。
        /// </summary>
        public void CleanupAllComponents()
        {
            Debug.Log("[GameEntry] Cleaning up all components...");

            // 按初始化逆序清理。
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
        /// 当前是否已经完成初始化。
        /// </summary>
        public bool IsInitialized => _isInitialized;
    }
}
