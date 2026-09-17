using System;
using System.Collections;
using Server;
using CrystalMagic.Game.Unit;
using CrystalMagic.UI;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Core
{
    public class OnlineBattlePreparationState : GameState
    {
        public const string SceneName = DungeonState.SceneName;
        private ClientBattleManager battleManager;
        private bool restoreRequested;

        public static TransitionData CreateEnterTransitionData(ClientBattleManager battleManager, string ticket, ulong accountId, bool reload)
        {
            TransitionData transitionData = null;
            transitionData = new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(OnlineBattlePreparationState),
                TargetStateData = battleManager,
                TransitionUIName = "TransitionUI",
                KeepCurrentMainScene = true,
                ActiveSubSceneNames = new[] { DungeonState.RegistrySubSceneName },
                PreLoadCoroutineFactory = () => PrepareBattleWorld(battleManager),
                PostLoadCoroutineFactory = () => InitializeBattleScene(
                    transitionData,
                    battleManager,
                    ticket,
                    accountId,
                    reload),
            };
            return transitionData;
        }

        public static TransitionData CreateReturnToTownTransitionData(ClientBattleManager battleManager)
        {
            TransitionData transitionData = null;
            transitionData = new TransitionData
            {
                TargetSceneName = TownState.SceneName,
                TargetStateType = typeof(TownState),
                TransitionUIName = "TransitionUI",
                KeepCurrentMainScene = true,
                ActiveSubSceneNames = new[] { TownState.SubSceneName },
                PreLoadCoroutineFactory = () => RestoreTownWorld(battleManager, transitionData),
                OnComplete = () =>
                {
                    battleManager?.ClearPreBattleSnapshot();
                },
            };
            return transitionData;
        }

        private static IEnumerator PrepareBattleWorld(ClientBattleManager battleManager)
        {
            if (battleManager == null || !battleManager.HasPreBattleSnapshot)
            {
                battleManager?.FailPreparation("战前存档快照不可用。");
                yield break;
            }

            DungeonSceneRuntimeBuilder.DestroyCurrentDungeonScene();
            yield return null;

            SceneComponent.Instance.SetSubScenesActive(Array.Empty<string>());
            yield return SceneComponent.Instance.WaitForSubSceneUnloadedCoroutine(TownState.SubSceneName);
            if (SceneComponent.Instance.IsSubSceneLoaded(TownState.SubSceneName))
            {
                battleManager.FailPreparation("城镇场景未能完成卸载。");
                yield break;
            }

            GameWorldManager.ShutdownGameWorld();
            try
            {
                World world = GameWorldManager.CreateGameWorld(GameWorldRole.Client, false);
                FrameManagerUtility.Bind(world.EntityManager, battleManager.frame);
                GameWorldManager.SetSceneMode(GameSceneMode.Dungeon);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                battleManager.FailPreparation("初始化客户端战斗世界失败。");
            }
        }

        private static IEnumerator RestoreTownWorld(ClientBattleManager battleManager, TransitionData transitionData)
        {
            battleManager?.StopBattle();
            DungeonSceneRuntimeBuilder.DestroyCurrentDungeonScene();
            yield return null;

            SceneComponent.Instance.SetSubScenesActive(Array.Empty<string>());
            yield return SceneComponent.Instance.WaitForSubSceneUnloadedCoroutine(DungeonState.RegistrySubSceneName);

            GameWorldManager.ShutdownGameWorld();
            LoadGameContext context = null;
            bool restored = false;
            try
            {
                GameWorldManager.CreateGameWorld(GameWorldRole.Standalone);
                GameWorldManager.SetSceneMode(GameSceneMode.Town);
                restored = battleManager != null && battleManager.TryRestorePreBattleSave(out context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (!restored)
            {
                Debug.LogError("[OnlineBattlePreparationState] Failed to restore the pre-battle save.");
                yield break;
            }

            transitionData.TargetStateData = context;
        }

        private static IEnumerator InitializeBattleScene(
            TransitionData transitionData,
            ClientBattleManager battleManager,
            string ticket,
            ulong accountId,
            bool reload)
        {
            if (battleManager == null || string.IsNullOrEmpty(ticket) || accountId == 0UL)
            {
                Debug.LogError("[OnlineBattlePreparationState] Battle ticket is invalid.");
                yield break;
            }

            if (battleManager.PreparationFailed)
            {
                yield break;
            }

            battleManager.ConnectWithTicket(ticket, accountId, reload);
            while (battleManager != null &&
                !battleManager.PreparationFailed &&
                battleManager.battleConnect != null &&
                battleManager.BattleData == null)
            {
                GameWorldManager.UpdateGameWorld();
                yield return null;
            }

            if (battleManager == null ||
                battleManager.PreparationFailed ||
                battleManager.battleConnect == null ||
                battleManager.BattleData == null)
            {
                Debug.LogError("[OnlineBattlePreparationState] Battle scene data was not received.");
                yield break;
            }

            bool registryInitialized = false;
            while (!battleManager.PreparationFailed && battleManager.battleConnect != null)
            {
                GameWorldManager.UpdateGameWorld();
                World world = GameWorldManager.GameWorld;
                if (world != null && world.IsCreated)
                {
                    EntityManager entityManager = world.EntityManager;
                    EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitySpawnRegistrySingleton>());
                    if (!query.IsEmptyIgnoreFilter)
                    {
                        battleManager.OnBattleSceneInitialized();
                        registryInitialized = true;
                        break;
                    }
                }

                yield return null;
            }

            if (!registryInitialized)
            {
                yield break;
            }

            while (!battleManager.PreparationFailed &&
                battleManager.battleConnect != null
                && (!battleManager.SceneInitialized || !battleManager.EntitiesInitialized || !battleManager.BattleStarted))
            {
                GameWorldManager.UpdateGameWorld();
                yield return null;
            }

            if (!battleManager.SceneInitialized)
                Debug.LogError("[OnlineBattlePreparationState] Battle scene was not initialized.");
            else if (!battleManager.EntitiesInitialized)
                Debug.LogError("[OnlineBattlePreparationState] Network entities were not initialized.");
            else if (!battleManager.BattleStarted)
                Debug.LogError("[OnlineBattlePreparationState] Battle start frame was not received.");
            else
            {
                transitionData.TargetStateType = typeof(OnlineBattleState);
                transitionData.TargetStateData = battleManager;
            }
        }

        public override void OnEnter()
        {
            InputComponent.Instance.SetBattleInputEnabled(false);
            battleManager = StateData as ClientBattleManager;
            restoreRequested = false;
        }

        public override void OnUpdate()
        {
            if (restoreRequested || battleManager == null || battleManager.BattleStarted)
            {
                return;
            }

            restoreRequested = true;
            GameFlowComponent.Instance.BeginTransition(
                CreateReturnToTownTransitionData(battleManager));
        }

        public override void OnExit()
        {
            battleManager = null;
            restoreRequested = false;
        }
    }

    public sealed class OnlineBattleState : BattleStateBase
    {
        protected override string BattleSceneName => DungeonState.SceneName;

        private ClientBattleManager battleManager;
        private MinimapUI minimapUI;
        private bool restoreRequested;

        protected override void OnEnterBattle()
        {
            battleManager = StateData as ClientBattleManager;
            restoreRequested = false;
            if (battleManager == null || !battleManager.BattleStarted)
            {
                Debug.LogError("[OnlineBattleState] Client battle is not ready.");
                return;
            }

            minimapUI = UIComponent.Instance.Open<MinimapUI>();
            UIComponent.Instance.SetLifetime(minimapUI, UILifetime.SceneScoped);
            Debug.Log("[OnlineBattleState] Entered online battle.");
        }

        protected override void OnUpdateBattle()
        {
            if (restoreRequested || battleManager == null || !battleManager.RestoreStandaloneRequested)
            {
                return;
            }

            restoreRequested = true;
            GameFlowComponent.Instance.BeginTransition(
                OnlineBattlePreparationState.CreateReturnToTownTransitionData(battleManager));
        }

        protected override void OnExitBattle()
        {
            if (minimapUI != null && UIComponent.Instance.IsManaged(minimapUI))
                UIComponent.Instance.CloseUI(minimapUI);

            minimapUI = null;
            battleManager = null;
            restoreRequested = false;
            Debug.Log("[OnlineBattleState] Exited online battle.");
        }
    }
}
