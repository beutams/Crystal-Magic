using UnityEngine;

namespace CrystalMagic.Core {

    public class LoadGameState : GameState
    {
        public override void OnEnter()
        {
            int saveIndex = StateData is int index ? index : 0;
            Debug.Log($"[LoadGameState] Loading slot index: {saveIndex}");

            bool success = SaveDataComponent.Instance.LoadFromSlot(saveIndex);
            SaveData saveData = SaveDataComponent.Instance.GetCurrentSaveData();

            if (!success || saveData == null)
            {
                Debug.LogError("[LoadGameState] Failed to load game!");
                return;
            }

            RuntimeDataComponent.Instance.InitializeForGameRun();

            LoadGameContext context = new LoadGameContext
            {
                SaveData = saveData,
                SaveIndex = saveIndex,
                Location = saveData.Location,
            };

            string targetSceneName;
            System.Type targetStateType;

            if (context.ShouldEnterDungeon())
            {
                Debug.Log($"[LoadGameState] 进入 Dungeon，楼层 {context.DungeonFloor}");
                targetSceneName = DungeonState.SceneName;
                targetStateType = typeof(DungeonState);
            }
            else if (context.ShouldEnterTraining())
            {
                Debug.Log("[LoadGameState] Enter Training");
                targetSceneName = TrainingState.SceneName;
                targetStateType = typeof(TrainingState);
            }
            else
            {
                Debug.Log("[LoadGameState] 进入 Town");
                targetSceneName = TownState.SceneName;
                targetStateType = typeof(TownState);
            }

            TransitionData transitionData = context.ShouldEnterDungeon()
                ? DungeonState.CreateEnterTransitionData(context)
                : new TransitionData
                {
                    TargetSceneName = targetSceneName,
                    TargetStateType = targetStateType,
                    TargetStateData = context,
                    TransitionUIName = "TransitionUI",
                    ForceReloadTargetScene = true,
                };

            GameFlowComponent.Instance.BeginTransition(transitionData);
        }

        public override void OnExit() { }
        public override void OnUpdate() { }
    }
}
