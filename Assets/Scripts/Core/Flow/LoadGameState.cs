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

            LoadGameContext context = new LoadGameContext
            {
                SaveData = saveData,
                SaveIndex = saveIndex,
            };

            string targetSceneName;
            System.Type targetStateType;

            if (context.ShouldEnterDungeon())
            {
                Debug.Log($"[LoadGameState] 鈫?Dungeon (Floor: {context.DungeonFloor})");
                targetSceneName = "Game";
                targetStateType = typeof(DungeonState);
            }
            else
            {
                Debug.Log("[LoadGameState] 鈫?Town");
                targetSceneName = "Game";
                targetStateType = typeof(TownState);
            }

            GameFlowComponent.Instance.SetState<TransitionState>(new TransitionData
            {
                TargetSceneName = targetSceneName,
                TargetStateType = targetStateType,
                TargetStateData = context
            });
        }

        public override void OnExit() { }
        public override void OnUpdate() { }
    }
}
