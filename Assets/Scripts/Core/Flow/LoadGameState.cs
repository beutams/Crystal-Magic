using UnityEngine;

namespace CrystalMagic.Core {

    public class LoadGameState : GameState
    {
        public override void OnEnter()
        {
            int saveIndex = StateData is int index ? index : 0;
            Debug.Log($"[LoadGameState] Loading slot index: {saveIndex}");

            GameWorldManager.ShutdownGameWorld();
            GameWorldManager.CreateGameWorld();
            bool success = SaveDataComponent.Instance.LoadFromSlot(saveIndex, out LoadGameContext context);

            if (!success)
            {
                Debug.LogError("[LoadGameState] Failed to load game!");
                GameWorldManager.ShutdownGameWorld();
                return;
            }

            if (context.ShouldEnterDungeon())
            {
                DungeonFlowTiming.Begin(context);
                Debug.Log($"[LoadGameState] 进入 Dungeon，主题 {context.DungeonThemeId}，关卡 {context.DungeonFloor}");
            }
            else if (context.ShouldEnterTraining())
            {
                Debug.Log("[LoadGameState] Enter Training");
            }
            else
            {
                Debug.Log("[LoadGameState] 进入 Town");
            }

            TransitionData transitionData = context.ShouldEnterDungeon()
                ? DungeonState.CreateEnterTransitionData(context)
                : context.ShouldEnterTraining()
                    ? TrainingState.CreateEnterTransitionData(context)
                    : TownState.CreateEnterTransitionData(context);

            GameFlowComponent.Instance.BeginTransition(transitionData);
        }

        public override void OnExit() { }
        public override void OnUpdate() { }
    }
}
