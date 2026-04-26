using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 城镇状态
    /// </summary>
    public class TownState : GameState
    {
        public const string SceneName = "TownScene";
        private CharacterUI _characterUI;
        private bool _inputBound;

        public override void OnEnter()
        {
            Debug.Log("[TownState] Entered Town");
            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Town);
            BindInput();
            
            // 可以在这里访问 StateData（如果是从读档进入）
            if (StateData is LoadGameContext context)
            {
                Debug.Log($"[TownState] Loaded from slot index: {context.SaveIndex}");
            }
        }

        public override void OnExit()
        {
            Debug.Log("[TownState] Exited Town");
            UnbindInput();
            ReleaseCharacterUI();
        }

        public override void OnUpdate()
        {
        }

        private void BindInput()
        {
            if (_inputBound || InputComponent.Instance == null)
                return;

            InputComponent.Instance.OnInventory += HandleInventory;
            _inputBound = true;
        }

        private void UnbindInput()
        {
            if (!_inputBound || InputComponent.Instance == null)
                return;

            InputComponent.Instance.OnInventory -= HandleInventory;
            _inputBound = false;
        }

        private void HandleInventory()
        {
            if (_characterUI == null)
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

        private void ReleaseCharacterUI()
        {
            if (_characterUI == null)
                return;

            UIComponent.Instance.ReleaseUI(_characterUI);
            _characterUI = null;
        }

        /// <summary>
        /// 从城镇进入地牢（带转场）
        /// </summary>
        public void GoToDungeon(object data = null)
        {
            TransitionData transData = new TransitionData
            {
                TargetSceneName = DungeonState.SceneName,
                TargetStateType = typeof(DungeonState),
                TargetStateData = data ?? SaveDataComponent.Instance?.CreateLoadGameContext(SaveAreaType.Dungeon),
                ForceReloadTargetScene = true,
            };
            GameFlowComponent.Instance.SetState<TransitionState>(transData);
        }

        public void GoToTrainingGround(object data = null)
        {
            object targetData = data ?? SaveDataComponent.Instance?.CreateLoadGameContext(SaveAreaType.Training);
            GameFlowComponent.Instance.SetState<TransitionState>(TrainingState.CreateEnterTransitionData(targetData));
        }

        /// <summary>
        /// 返回主菜单（带转场）
        /// </summary>
        public void GoToMainMenu()
        {
            TransitionData transData = new TransitionData
            {
                TargetSceneName = "MainMenu",
                TargetStateType = typeof(MainMenuState)
            };
            GameFlowComponent.Instance.SetState<TransitionState>(transData);
        }
    }
}
