using CrystalMagic.Game;
using CrystalMagic.UI;
using Unity.Entities;
using UnityEngine;
using Unity.Profiling;

namespace CrystalMagic.Core
{
    public abstract class BattleStateBase : GameState
    {
        private const string UIPlayerInputLockReason = "BattleStateBase.UIOpen";
        private static readonly ProfilerMarker BattleUpdateMarker = new("BattleState.Update");
        private static readonly ProfilerMarker HealthBarUpdateMarker = new("BattleState.UnitHealthBars");
        private static readonly ProfilerMarker DamageNumberUpdateMarker = new("BattleState.DamageNumbers");
        private static readonly ProfilerMarker InteractionPromptUpdateMarker = new("BattleState.InteractionPrompt");
        private static readonly ProfilerMarker UiInputLockUpdateMarker = new("BattleState.UIInputLock");
        private UIBase _battleUI;
        private CharacterUI _characterUI;
        private UnitHealthBarManager _unitHealthBarManager;
        private DamageNumberManager _damageNumberManager;
        private NotificationUI _notificationUI;
        private InteractionPromptManager _interactionPromptManager;
        private bool _inputBound;
        private bool _playerInputLockedByUI;

        protected virtual string BattleUIName => "BattleUI";
        protected abstract string BattleSceneName { get; }

        public sealed override void OnEnter()
        {
            OnEnterBattle();
            InputComponent.Instance?.SetBattleInputEnabled(true);
            _unitHealthBarManager ??= new UnitHealthBarManager();
            _unitHealthBarManager.Initialize();
            _damageNumberManager ??= new DamageNumberManager();
            _damageNumberManager.Initialize();
            _notificationUI = UIComponent.Instance.Open<NotificationUI>();
            _interactionPromptManager ??= new InteractionPromptManager();
            _interactionPromptManager.Initialize();
            OpenBattleUI();
            BindInput();
        }

        public sealed override void OnUpdate()
        {
            using (BattleUpdateMarker.Auto())
                OnUpdateBattle();
            if (_characterUI != null && _characterUI.gameObject.activeSelf && !_characterUI.IsSettingsPage &&
                GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager entityManager, out Entity player) &&
                PlayerCharacterUtility.IsEditLocked(entityManager, player))
                _characterUI.Close();
            using (InteractionPromptUpdateMarker.Auto())
                _interactionPromptManager?.Tick();
            using (UiInputLockUpdateMarker.Auto())
                RefreshUIInputLock();
        }

        public sealed override void OnLateUpdate()
        {
            // ECS simulation and NetworkComponent.LateUpdate have completed.
            using (HealthBarUpdateMarker.Auto())
                _unitHealthBarManager?.Tick();
            using (DamageNumberUpdateMarker.Auto())
                _damageNumberManager?.Tick();
        }

        public sealed override void OnExit()
        {
            _unitHealthBarManager?.Dispose();
            _unitHealthBarManager = null;
            _damageNumberManager?.Dispose();
            _damageNumberManager = null;
            ReleaseManagedUI(_notificationUI);
            _notificationUI = null;
            _interactionPromptManager?.Dispose();
            _interactionPromptManager = null;
            InputComponent.Instance?.SetBattleInputEnabled(false);
            ReleaseUIInputLock();
            UnbindInput();
            OnExitBattle();
            ReleaseManagedUI(_characterUI);
            ReleaseManagedUI(_battleUI);
            _characterUI = null;
            _battleUI = null;
        }

        protected virtual void OnEnterBattle()
        {
        }

        protected virtual void OnExitBattle()
        {
        }

        protected virtual void OnUpdateBattle()
        {
        }

        private void OpenBattleUI()
        {
            if (string.IsNullOrWhiteSpace(BattleUIName) || UIComponent.Instance == null)
            {
                return;
            }

            _battleUI = UIComponent.Instance.Open(BattleUIName);
        }

        private void BindInput()
        {
            if (_inputBound || InputComponent.Instance == null)
                return;

            InputComponent.Instance.OnInventory += HandleInventory;
            if (UIComponent.Instance != null)
                UIComponent.Instance.EscapeUnhandled += HandleUnhandledEscape;
            _inputBound = true;
        }

        private void UnbindInput()
        {
            if (!_inputBound)
                return;

            if (InputComponent.Instance != null)
            {
                InputComponent.Instance.OnInventory -= HandleInventory;
            }
            if (UIComponent.Instance != null)
                UIComponent.Instance.EscapeUnhandled -= HandleUnhandledEscape;
            _inputBound = false;
        }

        private void HandleInventory()
        {
            if (GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager entityManager, out Entity player) &&
                PlayerCharacterUtility.IsEditLocked(entityManager, player))
                return;
            if (_characterUI == null || !UIComponent.Instance.IsManaged(_characterUI))
            {
                _characterUI = UIComponent.Instance.Open<CharacterUI>();
                return;
            }

            if (_characterUI.gameObject.activeSelf)
            {
                _characterUI.Close();
                return;
            }

            UIComponent.Instance.ShowUI(_characterUI);
        }

        private void HandleUnhandledEscape()
        {
            if (_characterUI == null || !UIComponent.Instance.IsManaged(_characterUI))
            {
                _characterUI = UIComponent.Instance.Open<CharacterUI>(CharacterPage.Setting);
                return;
            }

            UIComponent.Instance.ShowUI(_characterUI);
            _characterUI.ShowSettings();
        }

        private void RefreshUIInputLock()
        {
            bool shouldLock = UIComponent.Instance != null
                && UIComponent.Instance.HasActiveSceneScopedPanel(
                    BattleSceneName,
                    BattleUIName,
                    "MinimapUI", nameof(NotificationUI));
            if (shouldLock == _playerInputLockedByUI)
                return;

            if (shouldLock)
            {
                GameGateComponent.Instance.Lock(GameGateType.PlayerInput, UIPlayerInputLockReason);
                _playerInputLockedByUI = true;
                return;
            }

            ReleaseUIInputLock();
        }

        private void ReleaseUIInputLock()
        {
            if (!_playerInputLockedByUI)
                return;

            GameGateComponent.Instance.Unlock(GameGateType.PlayerInput, UIPlayerInputLockReason);
            _playerInputLockedByUI = false;
        }

        private static void ReleaseManagedUI(UIBase panel)
        {
            if (panel != null && UIComponent.Instance.IsManaged(panel))
                UIComponent.Instance.CloseUI(panel);
        }
    }

    public class DungeonState : BattleStateBase
    {
        public const string SceneName = "DungeonScene";
        public const string RegistrySubSceneName = "DungeonRegistrySubScene";
        protected override string BattleSceneName => SceneName;
        private bool _isProcessingDefeat;
        private MinimapUI _minimapUI;
        private float _nextIntroSaveAttempt;

        public static TransitionData CreateEnterTransitionData(LoadGameContext context)
        {
            DungeonFlowTiming.EnsureStarted(context);
            DungeonFlowTiming.BeginStage(1, "创建地牢转场数据");
            TransitionData transitionData = new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(DungeonState),
                TargetStateData = context,
                TransitionUIName = "TransitionUI",
                KeepCurrentMainScene = true,
                ActiveSubSceneNames = new[] { RegistrySubSceneName },
                PostLoadCoroutineFactory = () => DungeonGenerationService.GenerateForTransition(context, SceneName),
            };
            DungeonFlowTiming.EndStage(1, "TransitionData 已创建");
            return transitionData;
        }

        public static int PrepareDungeonRun(LoadGameContext context)
        {
            int dungeonFloor = context?.DungeonFloor ?? 1;
            int dungeonThemeId = context?.DungeonThemeId ?? SaveDataComponent.Instance.GetInitialDungeonThemeId();

            if (SaveDataComponent.Instance.GetDungeonRunData() != null)
                SaveDataComponent.Instance.EnsureDungeonRunExists(dungeonThemeId, dungeonFloor);
            else if (context?.DungeonRun != null)
                GameRuntimeStateUtility.CreateDungeonRun(context.DungeonRun);
            else
                SaveDataComponent.Instance.BeginDungeonRunFromPersistent(dungeonThemeId, dungeonFloor);
            return dungeonFloor;
        }

        protected override void OnEnterBattle()
        {
            _isProcessingDefeat = false;
            _nextIntroSaveAttempt = 0f;
            EventComponent.Instance?.Subscribe<UnitDiedEvent>(HandleUnitDied);
            Debug.Log("[DungeonState] Entered Dungeon");
            LoadGameContext context = StateData as LoadGameContext;
            int dungeonFloor = PrepareDungeonRun(context);
            GameRuntimeStateUtility.BindPlayerCharacterData(context?.Character ?? new CharacterData());
            GameRuntimeStateUtility.RestoreDungeonRuntimeState(context?.Player);
            Debug.Log($"[DungeonState] Resuming dungeon theme {SaveDataComponent.Instance.GetDungeonRunData()?.ThemeId} at level {dungeonFloor}");
            _minimapUI = UIComponent.Instance.Open<MinimapUI>();
            UIComponent.Instance.SetLifetime(_minimapUI, UILifetime.SceneScoped);
        }

        protected override void OnUpdateBattle()
        {
            if (!TownIntroTriggerUtility.IsPending(SaveDataComponent.Instance) || Time.unscaledTime < _nextIntroSaveAttempt) return;
            _nextIntroSaveAttempt = Time.unscaledTime + 2f;
            NPCSequenceUtility.CompleteIntroOnDungeonArrival();
        }

        private void HandleUnitDied(UnitDiedEvent gameEvent)
        {
            if (_isProcessingDefeat ||
                TransitionComponent.Instance != null && TransitionComponent.Instance.IsTransitioning)
            return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            if (gameEvent.Entity == Entity.Null ||
                !entityManager.Exists(gameEvent.Entity) ||
                !entityManager.HasComponent<UnitFactionComponent>(gameEvent.Entity) ||
                !UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(gameEvent.Entity).Value))
            {
                return;
            }

            _isProcessingDefeat = true;
            GameFlowComponent.Instance.SetState<DungeonSettlementState>(
                DungeonSettlementStateData.Create(DungeonSettlementOutcome.Defeated));
        }

        protected override void OnExitBattle()
        {
            DungeonSceneRuntimeBuilder.DestroyCurrentDungeonScene();
            if (_minimapUI != null && UIComponent.Instance.IsManaged(_minimapUI))
                UIComponent.Instance.CloseUI(_minimapUI);
            _minimapUI = null;
            EventComponent.Instance?.Unsubscribe<UnitDiedEvent>(HandleUnitDied);
            Debug.Log("[DungeonState] Exited Dungeon");
        }
    }
}
