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
            string[] requiredSubSceneNames;

            if (context.ShouldEnterDungeon())
            {
                Debug.Log($"[LoadGameState] éˆ?Dungeon (Floor: {context.DungeonFloor})");
                targetSceneName = "Game";
                targetStateType = typeof(DungeonState);
                requiredSubSceneNames = null;
            }
            else
            {
                Debug.Log("[LoadGameState] éˆ?Town");
                targetSceneName = "Game";
                targetStateType = typeof(TownState);
                requiredSubSceneNames = new[] { "TownSubScene" };
            }

            GameFlowComponent.Instance.SetState<TransitionState>(new TransitionData
            {
                TargetSceneName = targetSceneName,
                TargetStateType = targetStateType,
                TargetStateData = context,
                RequiredSubSceneNames = requiredSubSceneNames,
            });
        }

        public override void OnExit() { }
        public override void OnUpdate() { }
    }
}
