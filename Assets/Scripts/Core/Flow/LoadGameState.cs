using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 读档流程状态
    /// 职责：读取存档数据，根据内容通过 TransitionState 进入城镇或地牢
    /// data 传入 string slotName
    /// </summary>
    public class LoadGameState : GameState
    {
        public override void OnEnter()
        {
            string slotName = StateData as string ?? "autosave";
            Debug.Log($"[LoadGameState] Loading slot: {slotName}");

            bool success = SaveDataComponent.Instance.LoadFromSlot(slotName);
            SaveData saveData = SaveDataComponent.Instance.GetCurrentSaveData();

            if (!success || saveData == null)
            {
                Debug.LogError("[LoadGameState] Failed to load game!");
                return;
            }

            LoadGameContext context = new LoadGameContext
            {
                SaveData = saveData,
                SlotName = slotName,
                HasDungeonRun = saveData.DungeonRun != null,
                DungeonFloor = saveData.DungeonRun?.CurrentFloor ?? 0
            };

            string targetSceneName;
            System.Type targetStateType;

            if (context.ShouldEnterDungeon())
            {
                Debug.Log($"[LoadGameState] → Dungeon (Floor: {context.DungeonFloor})");
                targetSceneName = "Dungeon";
                targetStateType = typeof(DungeonState);
            }
            else
            {
                Debug.Log("[LoadGameState] → Town");
                targetSceneName = "Town";
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
