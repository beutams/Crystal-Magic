using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 结算状态
    /// </summary>
    public class RunResultState : GameState
    {
        public override void OnEnter()
        {
            Debug.Log("[RunResultState] Entered RunResult");
        }

        public override void OnExit()
        {
            Debug.Log("[RunResultState] Exited RunResult");
        }

        public override void OnUpdate()
        {
        }

        /// <summary>
        /// 从结算返回城镇（带转场）
        /// </summary>
        public void ReturnToTown()
        {
            TransitionData transData = new TransitionData
            {
                TargetSceneName = TownState.SceneName,
                TargetStateType = typeof(TownState),
                TargetStateData = SaveDataComponent.Instance?.CreateLoadGameContext(SaveAreaType.Town),
                ForceReloadTargetScene = true,
            };
            GameFlowComponent.Instance.SetState<TransitionState>(transData);
        }

        /// <summary>
        /// 从结算返回主菜单（带转场）
        /// </summary>
        public void ReturnToMainMenu()
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
