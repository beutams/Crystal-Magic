using CrystalMagic.UI;
using UnityEngine;

namespace CrystalMagic.Core {
    public abstract class BattleStateBase : GameState
    {
        private UIBase _battleUI;

        protected virtual string BattleUIName => null;

        public sealed override void OnEnter()
        {
            OpenBattleUI();
            OnEnterBattle();
        }

        public sealed override void OnExit()
        {
            OnExitBattle();
            CloseBattleUI();
        }

        protected virtual void OnEnterBattle()
        {
        }

        protected virtual void OnExitBattle()
        {
        }

        protected void ReturnToTownInternal(object data = null)
        {
            GameFlowComponent.Instance.SetState<TransitionState>(new TransitionData
            {
                TargetSceneName = TownState.SceneName,
                TargetStateType = typeof(TownState),
                TargetStateData = data ?? SaveDataComponent.Instance?.CreateLoadGameContext(SaveAreaType.Town),
                ForceReloadTargetScene = true,
            });
        }

        private void OpenBattleUI()
        {
            if (string.IsNullOrWhiteSpace(BattleUIName) || UIComponent.Instance == null)
            {
                return;
            }

            _battleUI = UIComponent.Instance.Open(BattleUIName);
        }

        private void CloseBattleUI()
        {
            if (_battleUI == null || UIComponent.Instance == null)
            {
                return;
            }

            UIComponent.Instance.ReleaseUI(_battleUI);
            _battleUI = null;
        }
    }

    public sealed class TrainingState : BattleStateBase
    {
        public const string SceneName = "TrainingScene";

        protected override void OnEnterBattle()
        {
            Debug.Log("[TrainingState] Entered Training Ground");

            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Training);

            if (StateData is LoadGameContext context)
            {
                Debug.Log($"[TrainingState] Loaded from save slot: {context.SaveIndex}");
            }
        }

        protected override void OnExitBattle()
        {
            Debug.Log("[TrainingState] Exited Training Ground");
        }

        public void ReturnToTownFromTraining(object data = null)
        {
            ReturnToTownInternal(data);
        }

        public static TransitionData CreateEnterTransitionData(object data = null)
        {
            return new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(TrainingState),
                TargetStateData = data,
                ForceReloadTargetScene = true,
            };
        }
    }

    /// <summary>
    /// 地牢状态
    /// </summary>
    public class DungeonState : GameState
    {
        public const string SceneName = "DungeonScene";

        public override void OnEnter()
        {
            Debug.Log("[DungeonState] Entered Dungeon");
            int dungeonFloor = 1;
            
            // 可以在这里访问 StateData（如果是从读档进入）
            if (StateData is LoadGameContext context)
            {
                dungeonFloor = context.DungeonFloor;
                Debug.Log($"[DungeonState] Resuming dungeon at floor: {context.DungeonFloor}");
            }

            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Dungeon, dungeonFloor);
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
                TargetSceneName = TownState.SceneName,
                TargetStateType = typeof(TownState),
                TargetStateData = data ?? SaveDataComponent.Instance?.CreateLoadGameContext(SaveAreaType.Town),
                ForceReloadTargetScene = true,
            };
            GameFlowComponent.Instance.SetState<TransitionState>(transData);
        }
    }
}
