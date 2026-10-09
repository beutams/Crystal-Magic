using UnityEngine;

namespace CrystalMagic.Core {

    public class LoadGameState : GameState
    {
        public override void OnEnter()
        {
            int saveIndex = StateData is int index ? index : 0;
            Debug.Log($"[LoadGameState] Loading slot index: {saveIndex}");

            using (SceneLoadTiming.Measure("Park previous GameWorld"))
                GameWorldManager.ReleaseGameWorld();
            SceneComponent.Instance.PreloadStandaloneWorld();
            // Reuse the World even while it is loading; the transition waits for SubScenes.
            using (SceneLoadTiming.Measure("Reuse prepared Standalone World"))
                GameWorldManager.CreateGameWorld();
            bool success;
            LoadGameContext context;
            using (SceneLoadTiming.Measure($"Read save slot {saveIndex}"))
                success = SaveDataComponent.Instance.LoadFromSlot(saveIndex, out context);

            if (!success)
            {
                Debug.LogError("[LoadGameState] Failed to load game!");
                GameWorldManager.ReleaseGameWorld();
                SceneLoadTiming.Finish("Failed to read save slot.");
                GameFlowComponent.Instance.SetState<MainMenuState>();
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

            TransitionData transitionData;
            using (SceneLoadTiming.Measure("Build target transition data"))
                transitionData = context.ShouldEnterDungeon()
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
