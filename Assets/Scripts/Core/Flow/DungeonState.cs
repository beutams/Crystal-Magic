using CrystalMagic.UI;
using UnityEngine;

namespace CrystalMagic.Core
{
    public abstract class BattleStateBase : GameState
    {
        private UIBase _battleUI;

        protected virtual string BattleUIName => "BattleUI";

        public sealed override void OnEnter()
        {
            OpenBattleUI();
            OnEnterBattle();
        }

        public sealed override void OnExit()
        {
            OnExitBattle();
            _battleUI = null;
        }

        protected virtual void OnEnterBattle()
        {
        }

        protected virtual void OnExitBattle()
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

    public class DungeonState : BattleStateBase
    {
        public const string SceneName = "DungeonScene";

        protected override void OnEnterBattle()
        {
            Debug.Log("[DungeonState] Entered Dungeon");
            int dungeonFloor = 1;

            if (StateData is LoadGameContext context)
            {
                dungeonFloor = context.DungeonFloor;
                Debug.Log($"[DungeonState] Resuming dungeon at floor: {context.DungeonFloor}");
            }

            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Dungeon, dungeonFloor);
        }

        protected override void OnExitBattle()
        {
            Debug.Log("[DungeonState] Exited Dungeon");
        }
    }
}
