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
                PreLoadCoroutineFactory = () => PrepareBattleWorld(battleManager, transitionData),
                OnLoadFailed = error => battleManager.FailPreparation(error),
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
                OnLoadFailed = error =>
                {
                    transitionData.TargetStateType = typeof(OnlineBattleRecoveryState);
                    transitionData.TargetStateData = new BattleRecoveryContext { Manager = battleManager, Error = error };
                },
                OnComplete = () =>
                {
                    if (battleManager.HasPendingSettlement && !SaveDataComponent.Instance.Save())
                    {
                        GameFlowComponent.Instance.SetState<OnlineBattleRecoveryState>(new BattleRecoveryContext
                        {
                            Manager = battleManager,
                            Error = "结算数据写入存档失败，已保留当前角色和结算结果，请重试。",
                            SaveContext = (LoadGameContext)transitionData.TargetStateData,
                        });
                        return;
                    }
                    battleManager.ClearPreBattleSnapshot();
                },
            };
            return transitionData;
        }

        public static TransitionData CreateNextThemeTransitionData(ClientBattleManager battleManager)
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
                PreLoadCoroutineFactory = () => PrepareBattleWorld(battleManager, transitionData),
                OnLoadFailed = error => battleManager.FailPreparation(error),
                PostLoadCoroutineFactory = () => InitializePreparedBattleScene(transitionData, battleManager),
            };
            return transitionData;
        }

        private static IEnumerator PrepareBattleWorld(ClientBattleManager battleManager, TransitionData transitionData)
        {
            if (battleManager == null || !battleManager.HasPreBattleSnapshot)
            {
                battleManager?.FailPreparation("战前存档快照不可用。");
                transitionData.LoadError = "战前存档快照不可用。";
                yield break;
            }

            if (GameWorldManager.HasGameWorld && GameWorldManager.Role == GameWorldRole.Client)
            {
                GameWorldManager.RemoveGameWorldFromPlayerLoop();
                BattleSceneResetUtility.Reset(GameWorldManager.GameWorld);
                DungeonSceneRuntimeBuilder.DestroyCurrentDungeonScene();
                yield return null; // 等旧场景 Root 完成实体/资源释放，再创建新地图。
                yield break;
            }

            DungeonSceneRuntimeBuilder.DestroyCurrentDungeonScene();
            yield return null;

            SceneComponent.Instance.SetSubScenesActive(Array.Empty<string>());
            yield return SceneComponent.Instance.WaitForSubSceneUnloadedCoroutine(TownState.SubSceneName);
            yield return SceneComponent.Instance.WaitForSubSceneUnloadedCoroutine(DungeonState.RegistrySubSceneName);
            if (SceneComponent.Instance.IsSubSceneLoaded(TownState.SubSceneName) ||
                SceneComponent.Instance.IsSubSceneLoaded(DungeonState.RegistrySubSceneName))
            {
                battleManager.FailPreparation("旧的游戏子场景未能完成卸载。");
                transitionData.LoadError = battleManager.PreparationError;
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
                transitionData.LoadError = battleManager.PreparationError;
            }
        }

        private static IEnumerator RestoreTownWorld(ClientBattleManager battleManager, TransitionData transitionData)
        {
            battleManager?.StopBattle();
            DungeonSceneRuntimeBuilder.DestroyCurrentDungeonScene();
            yield return null;

            SceneComponent.Instance.SetSubScenesActive(Array.Empty<string>());
            yield return SceneComponent.Instance.WaitForSubSceneUnloadedCoroutine(DungeonState.RegistrySubSceneName);
            yield return SceneComponent.Instance.WaitForSubSceneUnloadedCoroutine(TownState.SubSceneName);
            if (SceneComponent.Instance.IsSubSceneLoaded(DungeonState.RegistrySubSceneName) ||
                SceneComponent.Instance.IsSubSceneLoaded(TownState.SubSceneName))
            {
                transitionData.LoadError = "旧场景未能完成卸载，无法恢复单机存档。";
                yield break;
            }

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
                transitionData.LoadError = "无法读取战前存档，存档可能不可用或 GUID 不匹配。已保留恢复信息，请修复后重试。";
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
            if (battleManager == null || string.IsNullOrEmpty(ticket) || accountId == 0UL ||
                battleManager.PreparationFailed)
                yield break;

            battleManager.ConnectWithTicket(ticket, accountId, reload);
            yield return InitializePreparedBattleScene(transitionData, battleManager);
        }

        private static IEnumerator InitializePreparedBattleScene(
            TransitionData transitionData,
            ClientBattleManager battleManager)
        {
            while (battleManager != null && !battleManager.PreparationFailed && battleManager.battleConnect != null)
            {
                // 重连初始化期间也可能收到全队转层。仍在同一个黑屏里重建新主题。
                if (battleManager.ThemeTransitionRequested)
                {
                    battleManager.ConsumeThemeTransitionRequest();
                    yield return PrepareBattleWorld(battleManager, transitionData);
                    if (battleManager.PreparationFailed)
                        yield break;
                    SceneComponent.Instance.SetSubScenesActive(new[] { DungeonState.RegistrySubSceneName });
                }

                GameWorldManager.UpdateGameWorld();
                if (battleManager.BattleData != null && !battleManager.SceneInitialized)
                {
                    World world = GameWorldManager.GameWorld;
                    if (world != null && world.IsCreated)
                    {
                        using EntityQuery query = world.EntityManager.CreateEntityQuery(
                            ComponentType.ReadOnly<EntitySpawnRegistrySingleton>());
                        if (!query.IsEmptyIgnoreFilter)
                            battleManager.OnBattleSceneInitialized();
                    }
                }

                if (battleManager.SceneInitialized && battleManager.EntitiesInitialized && battleManager.BattleStarted)
                {
                    transitionData.TargetStateType = typeof(OnlineBattleState);
                    transitionData.TargetStateData = battleManager;
                    yield break;
                }

                yield return null;
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

    public sealed class BattleRecoveryContext
    {
        public ClientBattleManager Manager;
        public string Error;
        public LoadGameContext SaveContext;
    }

    // 读写存档失败不生成空角色，也不放行城镇操作；恢复凭据和结算数据仍由 BattleManager 持有。
    public sealed class OnlineBattleRecoveryState : GameState
    {
        private BattleRecoveryContext context;
        private ConfirmSingleUI prompt;

        public override void OnEnter()
        {
            context = (BattleRecoveryContext)StateData;
            prompt = null;
            InputComponent.Instance.SetBattleInputEnabled(false);
            GameWorldManager.RemoveGameWorldFromPlayerLoop();
        }

        public override void OnUpdate()
        {
            if (TransitionComponent.Instance.IsTransitioning ||
                (prompt != null && UIComponent.Instance.IsManaged(prompt) && prompt.gameObject.activeSelf))
                return;
            prompt = UIComponent.Instance.Open<ConfirmSingleUI>(new ConfirmUIOpenData("恢复失败", context.Error, () =>
            {
                if (context.SaveContext == null)
                {
                    GameFlowComponent.Instance.BeginTransition(OnlineBattlePreparationState.CreateReturnToTownTransitionData(context.Manager));
                    return;
                }

                // 保存失败时直接重试写入当前 Town，不重新读取可能尚未写完整的存档文件。
                if (!SaveDataComponent.Instance.Save())
                    return;
                context.Manager.ClearPreBattleSnapshot();
                GameWorldManager.AppendGameWorldToPlayerLoop();
                GameFlowComponent.Instance.SetState<TownState>(context.SaveContext);
            }, confirmLabel: "重试", showCancelButton: false));
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
            if (restoreRequested || battleManager == null)
            {
                return;
            }

            if (battleManager.ThemeTransitionRequested)
            {
                restoreRequested = true;
                battleManager.ConsumeThemeTransitionRequest();
                GameFlowComponent.Instance.BeginTransition(
                    OnlineBattlePreparationState.CreateNextThemeTransitionData(battleManager));
                return;
            }

            if (!battleManager.RestoreStandaloneRequested)
                return;

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
