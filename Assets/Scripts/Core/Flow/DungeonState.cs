using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 地牢状态
    /// </summary>
    public class DungeonState : GameState
    {
        public override void OnEnter()
        {
            Debug.Log("[DungeonState] Entered Dungeon");
            
            // 可以在这里访问 StateData（如果是从读档进入）
            if (StateData is LoadGameContext context)
            {
                Debug.Log($"[DungeonState] Resuming dungeon at floor: {context.DungeonFloor}");
            }
        }

        public override void OnExit()
        {
            Debug.Log("[DungeonState] Exited Dungeon");
        }

        /// <summary>
        /// 从地牢进入结算（带转场）
        /// </summary>
        public void GoToRunResult(object data = null)
        {
            TransitionData transData = new TransitionData
            {
                TargetSceneName = "RunResult",
                TargetStateType = typeof(RunResultState),
                TargetStateData = data
            };
            GameFlowComponent.Instance.SetState<TransitionState>(transData);
        }

        /// <summary>
        /// 从地牢返回城镇（带转场）
        /// </summary>
        public void ReturnToTown(object data = null)
        {
            TransitionData transData = new TransitionData
            {
                TargetSceneName = "Town",
                TargetStateType = typeof(TownState),
                TargetStateData = data
            };
            GameFlowComponent.Instance.SetState<TransitionState>(transData);
        }
    }
}
