using System.Collections;
using UnityEngine;

namespace CrystalMagic.Core {

    public class LoadGameState : GameState
    {
        private Coroutine _load;

        public override void OnEnter()
        {
            int saveIndex = StateData is int index ? index : 0;
            Debug.Log($"[LoadGameState] Loading slot index: {saveIndex}");
            _load = SceneComponent.Instance.StartCoroutine(LoadSlot(saveIndex));
        }

        private static IEnumerator LoadSlot(int saveIndex)
        {
            using (SceneLoadTiming.Measure("Park previous GameWorld"))
                GameWorldManager.ReleaseGameWorld();
            SceneComponent.Instance.PreloadStandaloneWorld();
            // Let the existing menu request finish, including an immediate Start click.
            yield return null;
            using (SceneLoadTiming.Measure("Await prepared Standalone World"))
            {
                while (!GameWorldPreload.IsReady(GameWorldRole.Standalone))
                {
                    string error = GameWorldPreload.GetError(GameWorldRole.Standalone);
                    if (error != null)
                    {
                        Debug.LogError($"[LoadGameState] World preload failed: {error}");
                        GameFlowComponent.Instance.SetState<MainMenuState>();
                        yield break;
                    }
                    yield return null;
                }
            }
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
                yield break;
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

        public override void OnExit()
        {
            if (_load != null)
                SceneComponent.Instance.StopCoroutine(_load);
            _load = null;
        }
        public override void OnUpdate() { }
    }
}
