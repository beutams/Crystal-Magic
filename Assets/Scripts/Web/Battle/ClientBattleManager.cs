using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Server
{
    public class ClientBattleManager
    {
        private enum BattlePreparationStage
        {
            None,
            Connecting,
            WaitingForEnterResult,
            WaitingForReloadResult,
            WaitingForScene,
            WaitingForEntities,
            WaitingForReloadSnapshot,
            WaitingForStartFrame,
        }

        public ClientService battleServic => NetworkComponent.Instance.clientServic;
        public readonly ClientFrameManager frame = new ClientFrameManager();
        public Connect battleConnect;
        public string ticket;
        public ulong localAccountId;
        public Dictionary<Guid, Entity> networkEntities = new Dictionary<Guid, Entity>();
        public BattleEnterData BattleData => battleData;
        public bool SceneInitialized => sceneInitialized;
        public bool EntitiesInitialized => entitiesInitialized;
        public bool BattleStarted => battleStarted;
        public bool PreparationFailed => preparationFailed;
        public string PreparationError => preparationError;
        public bool RestoreStandaloneRequested => restoreStandaloneRequested;
        public bool HasPreBattleSnapshot => preBattleSaveIndex >= 0 &&
            Guid.TryParse(preBattleSaveGuid, out _) &&
            preBattleCharacterData != null;
        public event Action<string> onPreparationFailed;

        private BattleEnterData battleData;
        private NetworkEntitySpawnInfo[] pendingEntityInfos;
        private B2C_ReloadBattleSnapshot pendingReloadSnapshot;
        private long preparationTimerId;
        private uint connectVersion;
        private BattlePreparationStage preparationStage;
        private bool reload;
        private bool runningReload;
        private bool sceneInitialized;
        private bool entitiesInitialized;
        private bool battleStarted;
        private bool preparationFailed;
        private bool cleaningUp;
        private bool restoreStandaloneRequested;
        private string preparationError;
        private CharacterData preBattleCharacterData;
        private int preBattleSaveIndex = -1;
        private string preBattleSaveGuid;

        public void Cleanup()
        {
            StopBattle();
            ClearPreBattleSnapshot();
            preparationFailed = false;
            preparationError = null;
            onPreparationFailed = null;
        }

        public bool PrepareForOnlineBattle(out string error)
        {
            error = null;
            if (battleConnect != null)
            {
                error = "当前已有进行中的战斗准备。";
                return false;
            }

            if (HasPreBattleSnapshot)
            {
                return true;
            }

            SaveDataComponent saveData = SaveDataComponent.Instance;
            if (saveData == null || !saveData.Save())
            {
                error = "无法保存当前角色数据。";
                return false;
            }

            CharacterData characterData = saveData.GetCharacterData();
            if (characterData == null)
            {
                error = "当前角色数据不可用。";
                return false;
            }

            CharacterData snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(characterData));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = "无法创建角色数据快照。";
                return false;
            }

            if (snapshot == null || saveData.CurrentSaveIndex < 0 ||
                !Guid.TryParse(saveData.CurrentSaveGuid, out Guid saveGuid))
            {
                error = "战前存档快照无效。";
                return false;
            }

            preBattleCharacterData = snapshot;
            preBattleSaveIndex = saveData.CurrentSaveIndex;
            preBattleSaveGuid = saveGuid.ToString("N");
            return true;
        }

        public bool TryRestorePreBattleSave(out LoadGameContext context)
        {
            context = null;
            if (!HasPreBattleSnapshot || SaveDataComponent.Instance == null ||
                !SaveDataComponent.Instance.LoadFromSlot(preBattleSaveIndex, preBattleSaveGuid, out context))
            {
                return false;
            }
            return true;
        }

        public void ClearPreBattleSnapshot()
        {
            preBattleCharacterData = null;
            preBattleSaveIndex = -1;
            preBattleSaveGuid = null;
            restoreStandaloneRequested = false;
        }

        public void StopBattle()
        {
            bool wasCleaningUp = cleaningUp;
            cleaningUp = true;
            StopPreparationTimeout();
            frame?.Stop();
            if (battleConnect != null)
            {
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleResult>(), OnEnterBattleResult);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_ReloadBattleResult>(), OnReloadBattleResult);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleScene>(), OnEnterBattleScene);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_CreateNetworkEntities>(), OnCreateNetworkEntities);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_ReloadBattleSnapshot>(), OnReloadBattleSnapshot);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_StartFrame>(), OnStartFrame);
                battleConnect.OnConnected -= OnBattleConnected;
                battleConnect.OnDisconnected -= OnBattleDisconnected;
                battleServic.Disconnect(battleConnect);
                battleConnect = null;
            }

            ClearBattleData();
            cleaningUp = wasCleaningUp;
        }

        public void ConnectWithTicket(string ticket, ulong accountId, bool reload)
        {
            if (battleConnect != null || string.IsNullOrEmpty(ticket) || accountId == 0UL)
            {
                return;
            }

            if (!HasPreBattleSnapshot)
            {
                FailPreparation("战前角色数据快照不可用。");
                return;
            }

            this.ticket = ticket;
            localAccountId = accountId;
            this.reload = reload;
            runningReload = false;
            pendingReloadSnapshot = null;
            battleStarted = false;
            preparationFailed = false;
            preparationError = null;
            restoreStandaloneRequested = false;
            battleServic.Connect(ServerUtility.GetBattleIPEndPoint(), out battleConnect);
            battleConnect.OnConnected += OnBattleConnected;
            battleConnect.OnDisconnected += OnBattleDisconnected;
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleResult>(), OnEnterBattleResult);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_ReloadBattleResult>(), OnReloadBattleResult);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleScene>(), OnEnterBattleScene);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_CreateNetworkEntities>(), OnCreateNetworkEntities);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_ReloadBattleSnapshot>(), OnReloadBattleSnapshot);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_StartFrame>(), OnStartFrame);
            StartPreparationTimeout(BattlePreparationStage.Connecting);
        }

        private void OnBattleConnected(Connect connect)
        {
            if (connect != battleConnect)
            {
                return;
            }

            CharacterData characterData = preBattleCharacterData;
            if (characterData == null)
            {
                FailPreparation("角色数据不可用。");
                battleServic.Disconnect(connect);
                return;
            }

            if (reload)
            {
                StartPreparationTimeout(BattlePreparationStage.WaitingForReloadResult);
                connect.Send(new C2B_ReloadBattle
                {
                    ticket = ticket,
                    saveGuid = preBattleSaveGuid,
                    data = characterData,
                });
                return;
            }

            StartPreparationTimeout(BattlePreparationStage.WaitingForEnterResult);
            connect.Send(new C2B_EnterBattle
            {
                ticket = ticket,
                saveGuid = preBattleSaveGuid,
                data = characterData,
            });
        }

        private void OnEnterBattleResult(IMessage message, Connect connect)
        {
            B2C_EnterBattleResult realMessage = message as B2C_EnterBattleResult;
            if (realMessage == null || connect != battleConnect)
            {
                return;
            }

            if (realMessage.type != BattleRequestType.EnterBattleSuccess || realMessage.connectVersion == 0U)
            {
                FailPreparation("进入战斗服务器失败。");
                battleServic.Disconnect(connect);
                return;
            }

            ticket = null;
            connectVersion = realMessage.connectVersion;
            runningReload = false;
            frame.ClearOrders();
            frame.AddConnect(connect);
            StartPreparationTimeout(BattlePreparationStage.WaitingForScene);
        }

        private void OnReloadBattleResult(IMessage message, Connect connect)
        {
            B2C_ReloadBattleResult realMessage = message as B2C_ReloadBattleResult;
            if (realMessage == null || connect != battleConnect)
            {
                return;
            }

            if (realMessage.type != BattleRequestType.ReloadBattleSuccess || realMessage.connectVersion == 0U)
            {
                FailPreparation("重连战斗服务器失败。");
                battleServic.Disconnect(connect);
                return;
            }

            ticket = null;
            connectVersion = realMessage.connectVersion;
            runningReload = realMessage.isRunningReload;
            frame.ClearOrders();
            frame.AddConnect(connect);
            StartPreparationTimeout(BattlePreparationStage.WaitingForScene);
        }

        private void OnBattleDisconnected(Connect connect)
        {
            if (battleConnect != connect)
            {
                return;
            }

            bool shouldRestore = !cleaningUp && HasPreBattleSnapshot;
            bool transitionInProgress = shouldRestore &&
                TransitionComponent.Instance != null &&
                TransitionComponent.Instance.IsTransitioning;
            StopPreparationTimeout();
            frame?.Stop();
            battleConnect = null;
            ClearBattleData();
            if (!shouldRestore)
            {
                return;
            }

            Debug.LogWarning("[Battle] Connection lost. Restoring the pre-battle standalone save.");
            restoreStandaloneRequested = true;
            if (transitionInProgress)
            {
                FailPreparation("与战斗服务器的连接已断开。");
                return;
            }

            GameFlowComponent.Instance.BeginTransition(
                OnlineBattlePreparationState.CreateReturnToTownTransitionData(this));
        }

        private void OnStartFrame(IMessage message, Connect connect)
        {
            B2C_StartFrame realMessage = message as B2C_StartFrame;
            if (realMessage == null ||
                connect != battleConnect ||
                battleData == null ||
                realMessage.battleId != battleData.battleId ||
                realMessage.connectVersion != connectVersion ||
                !entitiesInitialized)
            {
                return;
            }

            if (!GameWorldManager.AppendGameWorldToPlayerLoop())
            {
                FailPreparation("客户端战斗世界未能加入 PlayerLoop。");
                battleServic.Disconnect(connect);
                return;
            }

            StopPreparationTimeout();
            battleStarted = true;
            frame.Start(realMessage.startFrame);
        }

        private void OnEnterBattleScene(IMessage message, Connect connect)
        {
            B2C_EnterBattleScene realMessage = message as B2C_EnterBattleScene;
            if (realMessage == null ||
                connect != battleConnect ||
                realMessage.battleData == null ||
                realMessage.battleData.battleId == 0UL ||
                realMessage.connectVersion != connectVersion)
            {
                return;
            }

            if (battleData != null)
            {
                if (battleData.battleId != realMessage.battleData.battleId)
                {
                    Debug.LogError("[Battle] Received a scene message for another battle.");
                }

                return;
            }

            battleData = realMessage.battleData;
            sceneInitialized = false;
            entitiesInitialized = false;
            battleStarted = false;
            pendingEntityInfos = null;
            pendingReloadSnapshot = null;
            StartPreparationTimeout(BattlePreparationStage.WaitingForScene);
            if (!GameWorldManager.HasGameWorld || GameWorldManager.Role != GameWorldRole.Client)
            {
                FailPreparation("客户端战斗世界尚未准备完成。");
                battleServic.Disconnect(connect);
                return;
            }

            GameWorldManager.SetSceneMode(GameSceneMode.Dungeon);
        }

        public void OnBattleSceneInitialized()
        {
            if (battleData == null || sceneInitialized)
            {
                return;
            }

            string error = null;
            if (!GameWorldManager.TryGetEntityManager(out EntityManager entityManager) ||
                !DungeonMapPlanBuilder.TryBuildBattle(
                    battleData.themeKey,
                    battleData.seed,
                    out DungeonMapPlan mapPlan,
                    out error) ||
                !DungeonSceneRuntimeBuilder.TryBuildBattleClient(entityManager, mapPlan))
            {
                FailPreparation(string.IsNullOrWhiteSpace(error) ? "初始化客户端战斗地图失败。" : error);
                if (battleConnect != null)
                {
                    battleServic.Disconnect(battleConnect);
                }
                return;
            }

            GameWorldManager.UpdateGameWorld();

            sceneInitialized = true;
            if (battleConnect == null)
            {
                return;
            }

            StartPreparationTimeout(runningReload
                ? BattlePreparationStage.WaitingForReloadSnapshot
                : BattlePreparationStage.WaitingForEntities);
            battleConnect.Send(new C2B_BattleSceneReady
            {
                battleId = battleData.battleId,
                connectVersion = connectVersion,
            });
            TryInitializeNetworkEntities();
            TryInitializeReloadSnapshot();
        }

        private void OnCreateNetworkEntities(IMessage message, Connect connect)
        {
            B2C_CreateNetworkEntities realMessage = message as B2C_CreateNetworkEntities;
            if (realMessage == null ||
                connect != battleConnect ||
                battleData == null ||
                realMessage.battleId != battleData.battleId ||
                realMessage.connectVersion != connectVersion ||
                runningReload ||
                entitiesInitialized)
            {
                return;
            }

            pendingEntityInfos = realMessage.entityInfos ?? Array.Empty<NetworkEntitySpawnInfo>();
            TryInitializeNetworkEntities();
        }

        private void TryInitializeNetworkEntities()
        {
            if (!sceneInitialized ||
                pendingEntityInfos == null ||
                entitiesInitialized ||
                !GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
            {
                return;
            }

            if (!TryCreateNetworkEntities(entityManager, pendingEntityInfos))
            {
                return;
            }

            GameWorldManager.UpdateGameWorld();

            entitiesInitialized = true;
            if (battleConnect != null)
            {
                StartPreparationTimeout(BattlePreparationStage.WaitingForStartFrame);
                battleConnect.Send(new C2B_BattleReady
                {
                    battleId = battleData.battleId,
                    connectVersion = connectVersion,
                });
            }
        }

        private void OnReloadBattleSnapshot(IMessage message, Connect connect)
        {
            B2C_ReloadBattleSnapshot realMessage = message as B2C_ReloadBattleSnapshot;
            if (realMessage == null ||
                connect != battleConnect ||
                battleData == null ||
                realMessage.battleId != battleData.battleId ||
                realMessage.connectVersion != connectVersion ||
                !runningReload ||
                entitiesInitialized)
            {
                return;
            }

            pendingReloadSnapshot = realMessage;
            TryInitializeReloadSnapshot();
        }

        private void TryInitializeReloadSnapshot()
        {
            if (!sceneInitialized ||
                pendingReloadSnapshot == null ||
                entitiesInitialized ||
                !GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
            {
                return;
            }

            NetworkEntitySpawnInfo[] entityInfos = pendingReloadSnapshot.entityInfos ?? Array.Empty<NetworkEntitySpawnInfo>();
            if (!TryCreateNetworkEntities(entityManager, entityInfos))
            {
                return;
            }

            NetworkStateApplyContext context = new(
                entityManager,
                pendingReloadSnapshot.snapshotFrame,
                frame.frameInterval);
            List<NetworkStateData> states = pendingReloadSnapshot.states;
            if (states != null)
            {
                for (int index = 0; index < states.Count; index++)
                {
                    states[index]?.Apply(context);
                }
            }

            GameWorldManager.UpdateGameWorld();

            entitiesInitialized = true;
            if (battleConnect != null)
            {
                uint snapshotFrame = pendingReloadSnapshot.snapshotFrame;
                pendingReloadSnapshot = null;
                StartPreparationTimeout(BattlePreparationStage.WaitingForStartFrame);
                battleConnect.Send(new C2B_ReloadBattleReady
                {
                    battleId = battleData.battleId,
                    connectVersion = connectVersion,
                    snapshotFrame = snapshotFrame,
                });
            }
        }

        private bool TryCreateNetworkEntities(EntityManager entityManager, NetworkEntitySpawnInfo[] entityInfos)
        {
            for (int index = 0; index < entityInfos.Length; index++)
            {
                NetworkEntitySpawnInfo entityInfo = entityInfos[index];
                if (entityInfo == null ||
                    entityInfo.unitId == Guid.Empty ||
                    string.IsNullOrEmpty(entityInfo.prefabName))
                {
                    Debug.LogError("[Battle] Received invalid network entity spawn data.");
                    return false;
                }

                if (networkEntities.ContainsKey(entityInfo.unitId))
                {
                    continue;
                }

                if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity entity))
                {
                    Debug.LogError($"[Battle] Failed to create network entity '{entityInfo.prefabName}'.");
                    return false;
                }

                if (entityInfo.ownerAccountId == localAccountId)
                {
                    if (!entityManager.HasComponent<PlayerInputComponent>(entity))
                        entityManager.AddComponentData(entity, default(PlayerInputComponent));

                    if (entityManager.HasComponent<NetworkPlayerComponent>(entity))
                    {
                        entityManager.SetComponentData(entity, new NetworkPlayerComponent { id = entityInfo.unitId });
                    }
                    else
                    {
                        entityManager.AddComponentData(entity, new NetworkPlayerComponent { id = entityInfo.unitId });
                    }
                }

                networkEntities.Add(entityInfo.unitId, entity);
            }

            return true;
        }

        private void StartPreparationTimeout(BattlePreparationStage stage)
        {
            StopPreparationTimeout();
            preparationStage = stage;
            Connect expectedConnect = battleConnect;
            long timerId = 0;
            timerId = NetworkTimer.Instance.AddOnce(GetPreparationTimeout(stage), () =>
            {
                if (preparationTimerId != timerId ||
                    preparationStage != stage ||
                    battleConnect != expectedConnect ||
                    battleStarted)
                {
                    return;
                }

                preparationTimerId = 0;
                preparationStage = BattlePreparationStage.None;
                FailPreparation($"战斗准备阶段超时：{stage}。");
                if (expectedConnect != null)
                {
                    battleServic.Disconnect(expectedConnect);
                }
            });
            preparationTimerId = timerId;
        }

        private void StopPreparationTimeout()
        {
            if (preparationTimerId != 0)
            {
                NetworkTimer.Instance.Remove(preparationTimerId);
                preparationTimerId = 0;
            }

            preparationStage = BattlePreparationStage.None;
        }

        private long GetPreparationTimeout(BattlePreparationStage stage)
        {
            return stage switch
            {
                BattlePreparationStage.Connecting => ServerUtility.BattleEnterTimeout,
                BattlePreparationStage.WaitingForEnterResult => ServerUtility.BattleEnterTimeout,
                BattlePreparationStage.WaitingForReloadResult => ServerUtility.BattleEnterTimeout,
                BattlePreparationStage.WaitingForScene => ServerUtility.BattleInitializeTimeout,
                BattlePreparationStage.WaitingForEntities => ServerUtility.BattleInitializeTimeout,
                BattlePreparationStage.WaitingForReloadSnapshot => ServerUtility.BattleInitializeTimeout,
                BattlePreparationStage.WaitingForStartFrame => ServerUtility.BattleReadyTimeout,
                _ => ServerUtility.BattleReadyTimeout,
            };
        }

        public void FailPreparation(string error)
        {
            if (preparationFailed)
            {
                return;
            }

            preparationFailed = true;
            preparationError = error;
            onPreparationFailed?.Invoke(error);
        }

        private void ClearBattleData()
        {
            ticket = null;
            localAccountId = 0UL;
            reload = false;
            runningReload = false;
            connectVersion = 0U;
            networkEntities.Clear();
            battleData = null;
            pendingEntityInfos = null;
            pendingReloadSnapshot = null;
            sceneInitialized = false;
            entitiesInitialized = false;
            battleStarted = false;
        }
    }
}
