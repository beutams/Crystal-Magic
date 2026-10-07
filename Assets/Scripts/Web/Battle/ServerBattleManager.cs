using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Server
{
    public class ServerBattleManager
    {
        public IServerTransport battleService;
        public IServerTransport lobbyService;
        public Dictionary<ulong, BattleRoom> battleRooms = new Dictionary<ulong, BattleRoom>();
        public Dictionary<Connect, BattlePlayer> connectDic = new Dictionary<Connect, BattlePlayer>();

        public Connect lobbyConnect;
        private readonly Dictionary<Connect, B2C_BattleSettlement> pendingSettlements = new();
        private readonly Dictionary<Connect, long> settlementDeadlines = new();
        private readonly Dictionary<Connect, long> enterDeadlines = new();

        public event Action<string> SessionEnded;
        public void Initialize(IServerTransport battleTransport, IServerTransport lobbyTransport = null)
        {
            MessageCodec.Init();
            battleService = battleTransport ?? throw new ArgumentNullException(nameof(battleTransport));
            lobbyService = lobbyTransport;
            battleService.OnAccept += OnAccept;
            battleService.OnDisconnected += OnDisconnected;
            battleService.Init();

            if (lobbyService != null)
            {
                lobbyService.OnAccept += OnLobbyAccept;
                lobbyService.OnDisconnected += OnDisconnected;
                lobbyService.Init();
            }
        }

        private void OnDisconnected(Connect connect)
        {
            pendingSettlements.Remove(connect);
            settlementDeadlines.Remove(connect);
            enterDeadlines.Remove(connect);
            if (connect == lobbyConnect)
            {
                lobbyConnect = null;
                return;
            }

            if (!connectDic.TryGetValue(connect, out BattlePlayer player))
            {
                return;
            }

            Debug.LogWarning(
                $"[BattleTrace][Server] Socket disconnected: connect={connect?.RemoteEndpoint}, " +
                $"reason={connect?.LastDisconnectInfo?.Reason}, phase={connect?.LastDisconnectInfo?.Phase}, " +
                $"detail={connect?.LastDisconnectInfo?.Detail}, roomPhase={player.room?.phase}, " +
                $"player={DescribePlayer(player)}");

            connectDic.Remove(connect);
            player.room.frame.RemoveConnect(connect);
            if (player.connect != connect)
            {
                return;
            }

            player.exitReady = false;
            SetBattlePlayerConnectionState(player, BattlePlayerConnectionState.Offline);
            StopReloadTimeout(player);
            player.connect = null;
            player.offline = true;
            player.reconnectUntil = NetworkTimer.Instance.TimeNow + ServerUtility.ReconnectGrace;
            player.active = false;
            player.sceneReady = false;
            player.entitiesSent = false;
            player.ready = false;
            player.runningReload = false;
            player.reloadSnapshotRequested = false;
            player.reloadSnapshotSent = false;
            TryAdvancePhase(player.room);
            TryCompleteBattleExitSelection(player.room);
        }

        private void OnAccept(Connect connect)
        {
            enterDeadlines[connect] = NetworkTimer.Instance.TimeNow + ServerUtility.BattleEnterTimeout;
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_BattleSettlementAck>(), OnSettlementAck);
            Debug.Log($"[BattleTrace][Server] Battle socket accepted: connect={connect?.RemoteEndpoint}");
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_EnterBattle>(), OnEnterBattle);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_ReloadBattle>(), OnReloadBattle);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_BattleSceneReady>(), OnBattleSceneReady);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_BattleReady>(), OnBattleReady);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_ReloadBattleReady>(), OnReloadBattleReady);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_FramePing>(), OnFramePing);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2B_BattleExitRequest>(), OnBattleExitRequest);
        }

        private void OnBattleExitRequest(IMessage message, Connect connect)
        {
            if (message is not C2B_BattleExitRequest request ||
                !connectDic.TryGetValue(connect, out BattlePlayer player) || player.connect != connect)
                return;

            BattleRoom room = player.room;
            string error = null;
            int targetThemeKey = -1;
            if (player.offline || !player.active || room.phase != BattlePhase.Running ||
                room.battleId != request.battleId || player.connectVersion != request.connectVersion ||
                room.sceneVersion != request.sceneVersion || room.world == null ||
                !room.world.TryGetEntityManager(out EntityManager entityManager))
            {
                error = "当前无法使用出口，请稍后重试。";
            }
            else
            {
                NetworkStateApplyContext context = new(entityManager, room.frame.currentFrame, room.frame.frameInterval);
                if (!entityManager.Exists(player.entity) ||
                    BattlePlayerStatusUtility.IsSpectator(entityManager, player.entity) ||
                    !context.TryGetEntity(request.exitUnitId, out Entity exit) ||
                    GameInteractionUtility.ValidateTarget(entityManager, player.entity, exit) != InteractionResultCode.Success ||
                    !DungeonExitRuntimeUtility.TryGetDestination(entityManager, exit, out targetThemeKey, out _))
                    error = "出口尚不可用，或者角色已经进入观战。";
                else if (request.type == BattleExitRequestType.NextTheme && targetThemeKey < 0)
                    error = "这个出口没有配置下一个主题。";
                else if (request.type != BattleExitRequestType.NextTheme && request.type != BattleExitRequestType.Retreat)
                    error = "未知的出口操作。";
                else if (room.exitSelectionActive &&
                         (room.exitRequestType != request.type ||
                          (request.type == BattleExitRequestType.NextTheme &&
                           room.exitTargetThemeKey != targetThemeKey)))
                    error = "其他玩家已经选择了另一个出口。";
            }

            if (error == null)
            {
                if (!room.exitSelectionActive)
                {
                    room.exitSelectionActive = true;
                    room.exitRequestType = request.type;
                    room.exitTargetThemeKey = request.type == BattleExitRequestType.NextTheme
                        ? targetThemeKey
                        : -1;
                }

                SetBattlePlayerExitReady(player, true);
            }

            connect.Send(new B2C_BattleExitResult
            {
                battleId = request.battleId,
                sceneVersion = request.sceneVersion,
                accepted = error == null,
                error = error,
            });
            if (error != null)
                return;

            TryCompleteBattleExitSelection(room);
        }

        private void OnFramePing(IMessage message, Connect connect)
        {
            if (message is not C2B_FramePing ping ||
                !connectDic.TryGetValue(connect, out BattlePlayer player) || player.connect != connect)
                return;

            ServerFrameManager frame = player.room.frame;
            if (ping.sceneVersion != player.room.sceneVersion)
                return;
            connect.Send(new B2C_FramePong
            {
                sceneVersion = player.room.sceneVersion,
                clientSendTime = ping.clientSendTime,
                running = frame.running,
                serverFrame = frame.currentFrame,
                frameElapsedMs = frame.clock.GetFrameElapsedMilliseconds(NetworkTimer.Instance.TimeNow, frame.frameInterval),
            });
        }

        private void OnLobbyAccept(Connect connect)
        {
            if (lobbyConnect != null && lobbyConnect.State != ConnectState.Close)
            {
                lobbyService.Disconnect(connect);
                return;
            }
            lobbyConnect = connect;
            connect.RegisterCallback(MessageCodec.GetOpcode<L2B_StartRoom>(), OnStartRoom);
            connect.RegisterCallback(MessageCodec.GetOpcode<L2B_TryReloadRoom>(), OnReloadRoom);
        }

        // Lobby 只在这里换发 ticket。客户端拿到 ticket 后，会用 C2B_ReloadBattle 重新接入 Battle。
        private void OnReloadRoom(IMessage message, Connect connect)
        {
            if (connect != lobbyConnect || message is not L2B_TryReloadRoom request) return;
            B2L_ReloadRoomResult result = QueryReload(request);
            if (result != null) connect.Send(result);
        }

        public B2L_ReloadRoomResult QueryReload(L2B_TryReloadRoom realMessage)
        {
            if (realMessage == null || realMessage.accountId == 0UL ||
                !Guid.TryParse(realMessage.saveGuid, out Guid requestedSaveGuid))
            {
                return null;
            }

            string normalizedSaveGuid = requestedSaveGuid.ToString("N");

            foreach (BattleRoom room in battleRooms.Values)
            {
                if (room.phase == BattlePhase.Finished ||
                    !room.players.TryGetValue(realMessage.accountId, out BattlePlayer player) ||
                    (player.offline && player.reconnectUntil != 0 && NetworkTimer.Instance.TimeNow >= player.reconnectUntil))
                {
                    continue;
                }

                if (!string.Equals(player.saveGuid, normalizedSaveGuid, StringComparison.Ordinal))
                {
                    return new B2L_ReloadRoomResult
                    {
                        accountId = realMessage.accountId,
                        saveGuid = normalizedSaveGuid,
                        type = BattleReloadRoomResultType.SaveMismatch,
                    };
                }

                if (player.connect != null)
                {
                    SetPlayerOffline(player, "ReloadRoomReplacedConnection");
                }

                return new B2L_ReloadRoomResult
                {
                    accountId = realMessage.accountId,
                    saveGuid = normalizedSaveGuid,
                    type = BattleReloadRoomResultType.ReloadAvailable,
                    ticket = CreateTicket(room, player),
                    connection = room.connection,
                };
            }

            return new B2L_ReloadRoomResult
            {
                accountId = realMessage.accountId,
                saveGuid = normalizedSaveGuid,
                type = BattleReloadRoomResultType.NoBattle,
            };
        }

        private void OnStartRoom(IMessage message, Connect connect)
        {
            if (connect != lobbyConnect || message is not L2B_StartRoom request) return;
            connect.Send(CreateSession(request) ?? new B2L_StartRoomResult { roomId = request.roomId, error = "Invalid battle roster." });
        }

        public B2L_StartRoomResult CreateSession(L2B_StartRoom realMessage, BattleConnectionInfo connection = null)
        {
            if (realMessage == null || realMessage.players == null || realMessage.players.Length == 0 ||
                realMessage.saveGuids == null)
            {
                return null;
            }

            BattleRoom room = BattleRoom.CreateRoom(
                realMessage.roomId,
                realMessage.ownerAccountId,
                realMessage.themeKey,
                realMessage.players,
                realMessage.saveGuids);
            if (room == null)
            {
                return null;
            }
            if (connection != null && !connection.IsValid) return null;
            room.connection = connection ?? BattleConnectionInfo.Dedicated(
                Guid.TryParse(realMessage.sessionId, out _) ? realMessage.sessionId : room.sessionId);
            room.sessionId = room.connection.sessionId;
            battleRooms.Add(room.battleId, room);

            Dictionary<ulong, string> keys = new Dictionary<ulong, string>();
            foreach (BattlePlayer player in room.players.Values)
            {
                keys[player.accountId] = CreateTicket(room, player);
            }

            SetPhase(room, BattlePhase.WaitingForEnter);
            return new B2L_StartRoomResult { secretKeys = keys, roomId = realMessage.roomId,
                connection = room.connection };
        }

        public void EndSession(string sessionId)
        {
            foreach (BattleRoom room in new List<BattleRoom>(battleRooms.Values))
                if (room.sessionId == sessionId) FinishRoom(room);
        }

        // 首次入场只允许发生在 WaitingForEnter。重连必须经由 OnReloadBattle 进入。
        private void OnEnterBattle(IMessage message, Connect connect)
        {
            C2B_EnterBattle realMessage = message as C2B_EnterBattle;
            BattleRoom room = null;
            BattlePlayer player = null;
            Guid enterSaveGuid = Guid.Empty;
            bool ticketValid = realMessage != null &&
                !string.IsNullOrEmpty(realMessage.ticket) &&
                TryGetTicketPlayer(realMessage.ticket, out room, out player);
            ticketValid = ticketValid && MatchesSessionPeer(room, player, connect, realMessage.sessionId);
            bool saveGuidValid = realMessage != null &&
                Guid.TryParse(realMessage.saveGuid, out enterSaveGuid);
            bool saveMatches = ticketValid && saveGuidValid &&
                string.Equals(player.saveGuid, enterSaveGuid.ToString("N"), StringComparison.Ordinal);
            bool phaseValid = ticketValid && room.phase == BattlePhase.WaitingForEnter;
            bool playerStateValid = ticketValid &&
                !player.entered && !player.offline && player.connect == null;
            bool characterDataValid = realMessage?.data != null;
            if (!ticketValid || !saveMatches || !phaseValid || !playerStateValid || !characterDataValid)
            {
                Debug.LogWarning(
                    $"[BattleTrace][Server] C2B_EnterBattle rejected: connect={connect?.RemoteEndpoint}, " +
                    $"message={realMessage != null}, ticketValid={ticketValid}, saveGuidValid={saveGuidValid}, " +
                    $"saveMatches={saveMatches}, phase={room?.phase.ToString() ?? "<none>"}, " +
                    $"playerStateValid={playerStateValid}, characterDataValid={characterDataValid}");
                connect.Send(new B2C_EnterBattleResult { type = BattleRequestType.EnterBattleFail });
                battleService.DisconnectAfterSend(connect);
                return;
            }

            room.secretKeys.Remove(realMessage.ticket);
            enterDeadlines.Remove(connect);
            player.connect = connect;
            player.connectVersion++;
            player.offline = false;
            player.active = false;
            player.sceneReady = false;
            player.entitiesSent = false;
            player.ready = false;
            player.exitReady = false;
            player.runningReload = false;
            player.reloadSnapshotRequested = false;
            player.reloadSnapshotSent = false;
            player.characterData = realMessage.data;
            player.entered = true;
            connectDic[connect] = player;
            Debug.Log(
                $"[BattleTrace][Server] C2B_EnterBattle accepted: room={room.battleId}, " +
                $"account={player.accountId}, connect={connect.RemoteEndpoint}, player={DescribePlayer(player)}");
            connect.UnRegisterCallback(MessageCodec.GetOpcode<C2B_EnterBattle>(), OnEnterBattle);
            connect.Send(new B2C_EnterBattleResult
            {
                type = BattleRequestType.EnterBattleSuccess,
                connectVersion = player.connectVersion,
            });

            TryAdvancePhase(room);
        }

        // 重连只在新的 Battle TCP 连接上发生。它会按当前阶段补齐，而不是重新参与首次入场判断。
        private void OnReloadBattle(IMessage message, Connect connect)
        {
            C2B_ReloadBattle realMessage = message as C2B_ReloadBattle;
            if (realMessage == null ||
                string.IsNullOrEmpty(realMessage.ticket) ||
                !TryGetTicketPlayer(realMessage.ticket, out BattleRoom room, out BattlePlayer player) ||
                !MatchesSessionPeer(room, player, connect, realMessage.sessionId) ||
                !Guid.TryParse(realMessage.saveGuid, out Guid reloadSaveGuid) ||
                !string.Equals(player.saveGuid, reloadSaveGuid.ToString("N"), StringComparison.Ordinal) ||
                room.phase == BattlePhase.Finished ||
                player.connect != null ||
                !player.entered && realMessage.data == null)
            {
                connect.Send(new B2C_ReloadBattleResult { type = BattleRequestType.ReloadBattleFail });
                battleService.DisconnectAfterSend(connect);
                return;
            }

            bool runningReload = room.phase == BattlePhase.Running;
            enterDeadlines.Remove(connect);
            room.secretKeys.Remove(realMessage.ticket);
            StopReloadTimeout(player);
            player.connect = connect;
            player.connectVersion++;
            player.offline = false;
            player.active = false;
            player.sceneReady = false;
            player.entitiesSent = false;
            player.ready = false;
            player.exitReady = false;
            player.runningReload = runningReload;
            player.reloadSnapshotRequested = false;
            player.reloadSnapshotSent = false;
            player.snapshotFrame = 0U;
            if (!player.entered)
            {
                player.characterData = realMessage.data;
                player.entered = true;
            }

            connectDic[connect] = player;
            if (room.battleData != null && !TryEnsureBattlePlayerEntity(room, player))
            {
                connect.Send(new B2C_ReloadBattleResult { type = BattleRequestType.ReloadBattleFail });
                battleService.DisconnectAfterSend(connect);
                return;
            }

            if (room.world != null && room.world.TryGetEntityManager(out EntityManager reloadManager) &&
                reloadManager.Exists(player.entity) && reloadManager.HasComponent<PlayerInputComponent>(player.entity))
            {
                PlayerInputComponent input = reloadManager.GetComponentData<PlayerInputComponent>(player.entity);
                input.NetworkDirty = 1;
                reloadManager.SetComponentData(player.entity, input);
            }

            connect.UnRegisterCallback(MessageCodec.GetOpcode<C2B_ReloadBattle>(), OnReloadBattle);
            connect.Send(new B2C_ReloadBattleResult
            {
                type = BattleRequestType.ReloadBattleSuccess,
                connectVersion = player.connectVersion,
                isRunningReload = runningReload,
            });

            if (room.phase == BattlePhase.WaitingForClientReady || player.runningReload)
            {
                SendBattleScene(room, player);
            }

            TryAdvancePhase(room);
        }

        private void OnBattleReady(IMessage message, Connect connect)
        {
            C2B_BattleReady realMessage = message as C2B_BattleReady;
            bool playerKnown = connectDic.TryGetValue(connect, out BattlePlayer player);
            bool valid = realMessage != null &&
                playerKnown &&
                player.connect == connect &&
                !player.offline &&
                player.room != null &&
                player.room.phase == BattlePhase.WaitingForClientReady &&
                player.room.battleId == realMessage.battleId &&
                player.connectVersion == realMessage.connectVersion &&
                player.room.sceneVersion == realMessage.sceneVersion &&
                player.sceneReady &&
                player.entitiesSent &&
                !player.runningReload &&
                !player.ready;
            if (!valid)
            {
                Debug.LogWarning(
                    $"[BattleTrace][Server] C2B_BattleReady rejected: connect={connect?.RemoteEndpoint}, " +
                    $"message={realMessage != null}, playerKnown={playerKnown}, " +
                    $"messageBattleId={realMessage?.battleId}, messageConnectVersion={realMessage?.connectVersion}, " +
                    $"messageSceneVersion={realMessage?.sceneVersion}, room={player?.room?.battleId}, " +
                    $"roomPhase={player?.room?.phase.ToString() ?? "<none>"}, player={DescribePlayer(player)}");
                return;
            }

            player.ready = true;
            Debug.Log(
                $"[BattleTrace][Server] C2B_BattleReady accepted: room={player.room.battleId}, " +
                $"account={player.accountId}, connect={connect.RemoteEndpoint}, player={DescribePlayer(player)}");
            TryAdvancePhase(player.room);
        }

        private void OnBattleSceneReady(IMessage message, Connect connect)
        {
            C2B_BattleSceneReady realMessage = message as C2B_BattleSceneReady;
            bool playerKnown = connectDic.TryGetValue(connect, out BattlePlayer player);
            bool valid = realMessage != null &&
                playerKnown &&
                player.connect == connect &&
                !player.offline &&
                player.room != null &&
                player.room.battleId == realMessage.battleId &&
                player.connectVersion == realMessage.connectVersion &&
                player.room.sceneVersion == realMessage.sceneVersion &&
                !player.sceneReady;
            if (!valid)
            {
                Debug.LogWarning(
                    $"[BattleTrace][Server] C2B_BattleSceneReady rejected: connect={connect?.RemoteEndpoint}, " +
                    $"message={realMessage != null}, playerKnown={playerKnown}, " +
                    $"messageBattleId={realMessage?.battleId}, messageConnectVersion={realMessage?.connectVersion}, " +
                    $"messageSceneVersion={realMessage?.sceneVersion}, room={player?.room?.battleId}, " +
                    $"roomPhase={player?.room?.phase.ToString() ?? "<none>"}, player={DescribePlayer(player)}");
                return;
            }

            player.sceneReady = true;
            Debug.Log(
                $"[BattleTrace][Server] C2B_BattleSceneReady accepted: room={player.room.battleId}, " +
                $"account={player.accountId}, connect={connect.RemoteEndpoint}, player={DescribePlayer(player)}");
            if (player.runningReload)
            {
                SendReloadSnapshot(player.room, player);
                return;
            }

            if (player.room.phase == BattlePhase.WaitingForClientReady)
            {
                SendNetworkEntities(player.room, player);
            }
        }

        private void OnReloadBattleReady(IMessage message, Connect connect)
        {
            C2B_ReloadBattleReady realMessage = message as C2B_ReloadBattleReady;
            if (realMessage == null ||
                !connectDic.TryGetValue(connect, out BattlePlayer player) ||
                player.connect != connect ||
                player.offline ||
                !player.runningReload ||
                !player.reloadSnapshotSent ||
                player.room.phase != BattlePhase.Running ||
                player.room.battleId != realMessage.battleId ||
                player.connectVersion != realMessage.connectVersion ||
                player.room.sceneVersion != realMessage.sceneVersion ||
                player.snapshotFrame != realMessage.snapshotFrame)
            {
                return;
            }

            StopReloadTimeout(player);
            player.runningReload = false;
            player.reloadSnapshotRequested = false;
            player.reloadSnapshotSent = false;
            player.active = true;
            SetBattlePlayerConnectionState(player, BattlePlayerConnectionState.Online);
            player.room.frame.PromoteSyncingConnect(connect);
            connect.Send(new B2C_StartFrame
            {
                battleId = player.room.battleId,
                connectVersion = player.connectVersion,
                sceneVersion = player.room.sceneVersion,
                startFrame = player.room.frame.currentFrame,
                frameInterval = player.room.frame.frameInterval,
                frameElapsedMs = player.room.frame.clock.GetFrameElapsedMilliseconds(NetworkTimer.Instance.TimeNow, player.room.frame.frameInterval),
            });
        }

        private void TryAdvancePhase(BattleRoom room)
        {
            if (room == null || room.phase == BattlePhase.Finished || !battleRooms.ContainsKey(room.battleId))
            {
                return;
            }

            switch (room.phase)
            {
                case BattlePhase.WaitingForEnter:
                {
                    bool hasPlayer = false;
                    foreach (BattlePlayer player in room.players.Values)
                    {
                        if (player.offline)
                        {
                            continue;
                        }

                        if (!player.entered || player.connect == null)
                        {
                            return;
                        }

                        hasPlayer = true;
                    }

                    if (!hasPlayer)
                    {
                        if (room.phaseTimerId == 0)
                        {
                            FinishRoom(room);
                        }
                        return;
                    }

                    BeginInitialize(room);
                    return;
                }
                case BattlePhase.WaitingForClientReady:
                {
                    bool hasPlayer = false;
                    foreach (BattlePlayer player in room.players.Values)
                    {
                        if (player.offline || !player.entered)
                        {
                            continue;
                        }

                        if (player.connect == null || !player.ready)
                        {
                            return;
                        }

                        hasPlayer = true;
                    }

                    if (!hasPlayer)
                    {
                        if (room.phaseTimerId == 0)
                        {
                            FinishRoom(room);
                        }
                        return;
                    }

                    StartFrame(room);
                    return;
                }
            }
        }

        private void BeginInitialize(BattleRoom room)
        {
            room.frame.sceneVersion = room.sceneVersion;
            SetPhase(room, BattlePhase.Initializing);

            if (room.world != null && room.world.IsCreated)
                return;

            try
            {
                if (!SceneComponent.Instance.TryGetSubSceneGuid(
                        DungeonState.RegistrySubSceneName,
                        out Unity.Entities.Hash128 registrySceneGuid))
                {
                    throw new InvalidOperationException(
                        $"Battle registry SubScene '{DungeonState.RegistrySubSceneName}' is unavailable.");
                }

                room.world = new BattleWorldContext(room.battleId, room.frame, registrySceneGuid);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishRoom(room);
            }
        }

        private void FinishInitialize(BattleRoom room)
        {
            if (room.phase != BattlePhase.Initializing)
            {
                return;
            }

            SetPhase(room, BattlePhase.WaitingForClientReady);
            foreach (BattlePlayer player in room.players.Values)
            {
                if (!player.offline && player.entered && player.connect != null)
                {
                    SendBattleScene(room, player);
                }
            }

            TryAdvancePhase(room);
        }

        private void SendBattleScene(BattleRoom room, BattlePlayer player)
        {
            if (room == null ||
                player == null ||
                player.offline ||
                player.connect == null ||
                room.battleData == null)
            {
                return;
            }

            player.sceneReady = false;
            player.entitiesSent = false;
            player.ready = false;
            player.active = false;
            player.connect.Send(new B2C_EnterBattleScene
            {
                battleData = room.battleData,
                connectVersion = player.connectVersion,
            });
            if (player.runningReload)
            {
                StartReloadTimeout(player, ServerUtility.BattleInitializeTimeout);
            }
        }

        private void SendNetworkEntities(BattleRoom room, BattlePlayer player)
        {
            if (room == null ||
                player == null ||
                player.offline ||
                player.connect == null ||
                !player.sceneReady ||
                player.entitiesSent ||
                room.battleData == null)
            {
                return;
            }

            player.entitiesSent = true;
            player.connect.Send(new B2C_CreateNetworkEntities
            {
                battleId = room.battleId,
                connectVersion = player.connectVersion,
                sceneVersion = room.sceneVersion,
                entityInfos = room.entityInfos ?? Array.Empty<NetworkEntitySpawnInfo>(),
            });
        }

        private void SendReloadSnapshot(BattleRoom room, BattlePlayer player)
        {
            if (room == null ||
                player == null ||
                player.offline ||
                player.connect == null ||
                !player.runningReload ||
                !player.sceneReady ||
                player.reloadSnapshotRequested ||
                player.reloadSnapshotSent ||
                room.phase != BattlePhase.Running)
            {
                return;
            }

            World world = room.world?.World;
            ServerNetworkStateCollectSystem stateCollectSystem =
                world?.GetExistingSystemManaged<ServerNetworkStateCollectSystem>();
            if (stateCollectSystem == null)
            {
                Debug.LogError("[Battle] Server network state system is unavailable for reload snapshot.");
                SetPlayerOffline(player, "ReloadSnapshotSystemUnavailable");
                return;
            }

            player.reloadSnapshotRequested = true;
            Connect snapshotConnect = player.connect;
            uint connectVersion = player.connectVersion;
            stateCollectSystem.RequestSnapshot((snapshotFrame, entityInfos, states) =>
            {
                if (player.connect != snapshotConnect ||
                    player.connectVersion != connectVersion ||
                    player.offline ||
                    !player.runningReload ||
                    !player.reloadSnapshotRequested ||
                    room.phase != BattlePhase.Running)
                {
                    return;
                }

                player.reloadSnapshotRequested = false;
                player.snapshotFrame = snapshotFrame;
                player.reloadSnapshotSent = true;
                snapshotConnect.Send(new B2C_ReloadBattleSnapshot
                {
                    battleId = room.battleId,
                    connectVersion = connectVersion,
                    sceneVersion = room.sceneVersion,
                    snapshotFrame = snapshotFrame,
                    entityInfos = entityInfos,
                    states = states,
                });
                room.frame.AddSyncingConnect(snapshotConnect, player.unitId);
            });
            StartReloadTimeout(player, ServerUtility.BattleReadyTimeout);
        }

        private void StartFrame(BattleRoom room)
        {
            ServerFrameManager frame = room.frame;
            uint startFrame = frame.running ? frame.currentFrame : 0U;
            foreach (BattlePlayer player in room.players.Values)
            {
                if (player.offline || !player.entered || !player.ready || player.connect == null)
                {
                    continue;
                }

                player.active = true;
                SetBattlePlayerConnectionState(player, BattlePlayerConnectionState.Online);
                frame.AddConnect(player.connect, player.unitId);
            }

            SetPhase(room, BattlePhase.Running);
            frame.Start(startFrame);
            foreach (BattlePlayer player in room.players.Values)
            {
                if (!player.active || player.connect == null)
                {
                    continue;
                }

                player.connect.Send(new B2C_StartFrame
                {
                    battleId = room.battleId,
                    connectVersion = player.connectVersion,
                    sceneVersion = room.sceneVersion,
                    startFrame = startFrame,
                    frameInterval = frame.frameInterval,
                    frameElapsedMs = frame.clock.GetFrameElapsedMilliseconds(NetworkTimer.Instance.TimeNow, frame.frameInterval),
                });
            }
        }

        private void SetPhase(BattleRoom room, BattlePhase phase)
        {
            BattlePhase previousPhase = room.phase;
            if (room.phaseTimerId != 0)
            {
                NetworkTimer.Instance.Remove(room.phaseTimerId);
                room.phaseTimerId = 0;
            }

            room.phase = phase;
            room.phaseVersion++;
            long timeout = phase switch
            {
                BattlePhase.WaitingForEnter => ServerUtility.BattleEnterTimeout,
                BattlePhase.Initializing => ServerUtility.BattleInitializeTimeout,
                BattlePhase.WaitingForClientReady => ServerUtility.BattleReadyTimeout,
                _ => 0,
            };
            if (timeout <= 0)
            {
                Debug.Log(
                    $"[BattleTrace][Server] Phase changed: room={room.battleId}, " +
                    $"from={previousPhase}, to={phase}, version={room.phaseVersion}, timeout=none, " +
                    $"players={DescribePlayers(room)}");
                return;
            }

            Debug.Log(
                $"[BattleTrace][Server] Phase changed: room={room.battleId}, " +
                $"from={previousPhase}, to={phase}, version={room.phaseVersion}, timeout={timeout}ms, " +
                $"players={DescribePlayers(room)}");

            uint phaseVersion = room.phaseVersion;
            room.phaseTimerId = NetworkTimer.Instance.AddOnce(timeout, () =>
            {
                OnPhaseTimeout(room, phase, phaseVersion);
            });
        }

        private void OnPhaseTimeout(BattleRoom room, BattlePhase expectedPhase, uint expectedPhaseVersion)
        {
            if (room == null ||
                room.phase != expectedPhase ||
                room.phaseVersion != expectedPhaseVersion ||
                !battleRooms.ContainsKey(room.battleId))
            {
                return;
            }

            room.phaseTimerId = 0;
            Debug.LogWarning(
                $"[BattleTrace][Server] Phase timeout: room={room.battleId}, phase={expectedPhase}, " +
                $"version={expectedPhaseVersion}, players={DescribePlayers(room)}");
            switch (expectedPhase)
            {
                case BattlePhase.WaitingForEnter:
                    foreach (BattlePlayer player in room.players.Values)
                    {
                        if (!player.offline && (!player.entered || player.connect == null))
                        {
                            SetPlayerOffline(player, $"PhaseTimeout:{expectedPhase}");
                        }
                    }
                    TryAdvancePhase(room);
                    break;
                case BattlePhase.Initializing:
                    Debug.LogError($"[Battle] Initialize battle {room.battleId} timed out.");
                    FinishRoom(room);
                    break;
                case BattlePhase.WaitingForClientReady:
                    foreach (BattlePlayer player in room.players.Values)
                    {
                        if (!player.offline && (!player.entered || player.connect == null || !player.ready))
                        {
                            SetPlayerOffline(player, $"PhaseTimeout:{expectedPhase}");
                        }
                    }
                    TryAdvancePhase(room);
                    break;
            }
        }

        private void StartReloadTimeout(BattlePlayer player, long timeout)
        {
            StopReloadTimeout(player);
            if (player == null || timeout <= 0 || player.connect == null)
            {
                return;
            }

            Debug.Log(
                $"[BattleTrace][Server] Reload timeout started: room={player.room?.battleId}, " +
                $"account={player.accountId}, timeout={timeout}ms, player={DescribePlayer(player)}");

            uint reloadVersion = player.reloadVersion;
            Connect expectedConnect = player.connect;
            long timerId = 0;
            timerId = NetworkTimer.Instance.AddOnce(timeout, () =>
            {
                if (player.reloadTimerId != timerId ||
                    player.reloadVersion != reloadVersion ||
                    player.connect != expectedConnect ||
                    !player.runningReload)
                {
                    return;
                }

                player.reloadTimerId = 0;
                Debug.LogWarning(
                    $"[BattleTrace][Server] Reload timeout fired: room={player.room?.battleId}, " +
                    $"account={player.accountId}, player={DescribePlayer(player)}");
                SetPlayerOffline(player, "ReloadTimeout");
            });
            player.reloadTimerId = timerId;
        }

        private void StopReloadTimeout(BattlePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (player.reloadTimerId != 0)
            {
                NetworkTimer.Instance.Remove(player.reloadTimerId);
                player.reloadTimerId = 0;
            }

            player.reloadVersion++;
        }

        private void SetPlayerOffline(BattlePlayer player, string reason = null)
        {
            if (player == null)
            {
                return;
            }

            Debug.LogWarning(
                $"[BattleTrace][Server] SetPlayerOffline: reason={reason ?? "Unspecified"}, " +
                $"room={player.room?.battleId}, account={player.accountId}, player={DescribePlayer(player)}");

            player.exitReady = false;
            SetBattlePlayerConnectionState(player, BattlePlayerConnectionState.Offline);
            StopReloadTimeout(player);
            player.offline = true;
            player.reconnectUntil = NetworkTimer.Instance.TimeNow + ServerUtility.ReconnectGrace;
            player.active = false;
            player.sceneReady = false;
            player.entitiesSent = false;
            player.ready = false;
            player.runningReload = false;
            player.reloadSnapshotRequested = false;
            player.reloadSnapshotSent = false;
            if (player.connect == null)
            {
                TryCompleteBattleExitSelection(player.room);
                return;
            }

            Connect connect = player.connect;
            player.connect = null;
            connectDic.Remove(connect);
            player.room.frame.RemoveConnect(connect);
            battleService.Disconnect(connect);
            TryCompleteBattleExitSelection(player.room);
        }

        private static string DescribePlayer(BattlePlayer player)
        {
            if (player == null)
            {
                return "<null>";
            }

            return
                $"entered={player.entered}, offline={player.offline}, connected={player.connect != null}, " +
                $"sceneReady={player.sceneReady}, entitiesSent={player.entitiesSent}, ready={player.ready}, " +
                $"active={player.active}, reload={player.runningReload}, " +
                $"connectVersion={player.connectVersion}";
        }

        private static string DescribePlayers(BattleRoom room)
        {
            if (room?.players == null || room.players.Count == 0)
            {
                return "<none>";
            }

            List<string> descriptions = new List<string>();
            foreach (BattlePlayer player in room.players.Values)
            {
                descriptions.Add($"account={player.accountId}[{DescribePlayer(player)}]");
            }

            return string.Join("; ", descriptions);
        }

        private void SetBattlePlayerConnectionState(
            BattlePlayer player,
            BattlePlayerConnectionState connectionState)
        {
            if (player?.room?.world == null ||
                !player.room.world.TryGetEntityManager(out EntityManager entityManager) ||
                player.entity == Entity.Null ||
                !entityManager.Exists(player.entity) ||
                !entityManager.HasComponent<BattlePlayerStatusComponent>(player.entity))
            {
                return;
            }

            BattlePlayerStatusComponent status =
                entityManager.GetComponentData<BattlePlayerStatusComponent>(player.entity);
            byte transitionReady = player.exitReady ? (byte)1 : (byte)0;
            if (status.ConnectionState != connectionState || status.TransitionReady != transitionReady)
            {
                status.ConnectionState = connectionState;
                status.TransitionReady = transitionReady;
                status.NetworkDirty = 1;
                BattlePlayerStatusUtility.Apply(entityManager, player.entity, status);
            }

            if (connectionState != BattlePlayerConnectionState.Offline ||
                !entityManager.HasComponent<PlayerInputComponent>(player.entity))
            {
                return;
            }

            PlayerInputComponent input = entityManager.GetComponentData<PlayerInputComponent>(player.entity);
            input.Move = Unity.Mathematics.float2.zero;
            input.IsPrimaryHeld = 0;
            input.ContinuousPrimaryHeld = 0;
            input.IsInteractHeld = 0;
            input.IsInventoryHeld = 0;
            input.IsPropertyHeld = 0;
            input.IsEscapeHeld = 0;
            input.IsSkillHeld = 0;
            input.IsUsePropHeld = 0;
            entityManager.SetComponentData(player.entity, input);
            if (entityManager.HasBuffer<PlayerInputEventElement>(player.entity))
                entityManager.GetBuffer<PlayerInputEventElement>(player.entity).Clear();
        }

        private void SetBattlePlayerExitReady(BattlePlayer player, bool ready)
        {
            if (player == null)
                return;

            player.exitReady = ready;
            if (player.room?.world == null ||
                !player.room.world.TryGetEntityManager(out EntityManager entityManager) ||
                player.entity == Entity.Null ||
                !entityManager.Exists(player.entity) ||
                !entityManager.HasComponent<BattlePlayerStatusComponent>(player.entity))
            {
                return;
            }

            BattlePlayerStatusComponent status =
                entityManager.GetComponentData<BattlePlayerStatusComponent>(player.entity);
            byte value = ready ? (byte)1 : (byte)0;
            if (status.TransitionReady == value)
                return;

            status.TransitionReady = value;
            status.NetworkDirty = 1;
            BattlePlayerStatusUtility.Apply(entityManager, player.entity, status);
        }

        private bool TryCompleteBattleExitSelection(BattleRoom room)
        {
            if (room == null || !room.exitSelectionActive || room.phase != BattlePhase.Running ||
                room.world == null || !room.world.TryGetEntityManager(out EntityManager entityManager))
            {
                return false;
            }

            bool hasLivingPlayer = false;
            foreach (BattlePlayer player in room.players.Values)
            {
                if (player.offline || !player.active || player.connect == null || !player.entered)
                    continue;
                if (player.entity == Entity.Null || !entityManager.Exists(player.entity) ||
                    !entityManager.HasComponent<BattlePlayerStatusComponent>(player.entity))
                    return false;

                BattlePlayerStatusComponent status =
                    entityManager.GetComponentData<BattlePlayerStatusComponent>(player.entity);
                player.lifeState = status.LifeState;
                if (status.LifeState == BattlePlayerLifeState.Dead)
                    continue;

                hasLivingPlayer = true;
                if (!player.exitReady)
                    return false;
            }

            if (!hasLivingPlayer)
                return false;

            BattleExitRequestType requestType = room.exitRequestType;
            int targetThemeKey = room.exitTargetThemeKey;
            room.exitSelectionActive = false;
            room.exitTargetThemeKey = -1;
            if (requestType == BattleExitRequestType.Retreat)
                SettleRoom(room, BattleSettlementOutcome.Escaped);
            else
                BeginNextTheme(room, targetThemeKey);
            return true;
        }

        private void FinishRoom(BattleRoom room)
        {
            if (room == null || room.phase == BattlePhase.Finished)
            {
                return;
            }

            if (room.phaseTimerId != 0)
            {
                NetworkTimer.Instance.Remove(room.phaseTimerId);
                room.phaseTimerId = 0;
            }

            BattlePhase previousPhase = room.phase;
            room.phase = BattlePhase.Finished;
            Debug.LogWarning(
                $"[BattleTrace][Server] FinishRoom: room={room.battleId}, previousPhase={previousPhase}, " +
                $"players={DescribePlayers(room)}");
            room.frame.Stop();
            foreach (BattlePlayer player in room.players.Values)
            {
                SetPlayerOffline(player, "FinishRoom");
            }

            room.secretKeys.Clear();
            battleRooms.Remove(room.battleId);
            try { room.world?.Dispose(); }
            finally { room.world = null; SessionEnded?.Invoke(room.sessionId); }
        }

        private void BeginNextTheme(BattleRoom room, int targetThemeKey)
        {
            if (room == null || room.phase != BattlePhase.Running || targetThemeKey < 0)
                return;

            if (room.world != null && room.world.TryGetEntityManager(out EntityManager entityManager))
            {
                foreach (BattlePlayer player in room.players.Values)
                {
                    if (player.entity != Entity.Null && entityManager.Exists(player.entity) &&
                        entityManager.HasComponent<PlayerCharacterComponent>(player.entity))
                    {
                        PlayerCharacterComponent character =
                            entityManager.GetComponentObject<PlayerCharacterComponent>(player.entity);
                        player.characterData = PlayerCharacterUtility.Clone(character.Data);
                        player.hasTransferVitals = true;
                        player.reviveOnTransfer = entityManager.GetComponentData<BattlePlayerStatusComponent>(player.entity).LifeState == BattlePlayerLifeState.Dead;
                        player.transferHealth = entityManager.GetComponentData<UnitVitalityComponent>(player.entity).CurrentHealth;
                        player.transferMana = entityManager.GetComponentData<UnitManaComponent>(player.entity).CurrentMana;
                    }
                }
            }

            room.frame.Stop();
            BattleSceneResetUtility.Reset(room.world.World);
            room.themeKey = targetThemeKey;
            room.seed = ServerUtility.CreateBattleSeed();
            room.sceneVersion++;
            room.frame.sceneVersion = room.sceneVersion;
            room.battleData = null;
            room.entityInfos = Array.Empty<NetworkEntitySpawnInfo>();
            room.exitSelectionActive = false;
            room.exitTargetThemeKey = -1;

            foreach (BattlePlayer player in room.players.Values)
            {
                StopReloadTimeout(player);
                player.lifeState = BattlePlayerLifeState.Alive;
                if (!player.offline && player.connect != null)
                    player.connect.Send(new B2C_BeginBattleTheme
                    {
                        battleId = room.battleId,
                        sceneVersion = room.sceneVersion,
                        connectVersion = player.connectVersion,
                    });
                player.unitId = Guid.Empty;
                player.entity = Entity.Null;
                player.sceneReady = false;
                player.entitiesSent = false;
                player.ready = false;
                player.exitReady = false;
                player.active = false;
                player.runningReload = false;
                player.reloadSnapshotRequested = false;
                player.reloadSnapshotSent = false;
            }

            BeginInitialize(room);
        }

        private void TrySettleDefeatedRoom(BattleRoom room)
        {
            if (room == null || room.phase != BattlePhase.Running || room.world == null ||
                !room.world.TryGetEntityManager(out EntityManager entityManager))
            {
                return;
            }

            bool hasBattlePlayer = false;
            bool hasLivingPlayer = false;
            foreach (BattlePlayer player in room.players.Values)
            {
                if (!player.entered || player.entity == Entity.Null || !entityManager.Exists(player.entity) ||
                    !entityManager.HasComponent<BattlePlayerStatusComponent>(player.entity))
                {
                    continue;
                }

                hasBattlePlayer = true;
                BattlePlayerStatusComponent status =
                    entityManager.GetComponentData<BattlePlayerStatusComponent>(player.entity);
                player.lifeState = status.LifeState;
                if (status.LifeState == BattlePlayerLifeState.Alive)
                    hasLivingPlayer = true;
            }

            if (hasBattlePlayer && !hasLivingPlayer)
                SettleRoom(room, BattleSettlementOutcome.Defeated);
        }

        private void SettleRoom(BattleRoom room, BattleSettlementOutcome outcome)
        {
            if (room == null || room.phase == BattlePhase.Finished)
                return;

            if (room.world != null && room.world.TryGetEntityManager(out EntityManager entityManager))
            {
                foreach (BattlePlayer player in room.players.Values)
                {
                    if (player.entity != Entity.Null && entityManager.Exists(player.entity) &&
                        entityManager.HasComponent<PlayerCharacterComponent>(player.entity))
                    {
                        PlayerCharacterComponent character =
                            entityManager.GetComponentObject<PlayerCharacterComponent>(player.entity);
                        player.characterData = PlayerCharacterUtility.Clone(character.Data);
                    }
                }
            }

            if (room.phaseTimerId != 0)
            {
                NetworkTimer.Instance.Remove(room.phaseTimerId);
                room.phaseTimerId = 0;
            }

            room.phase = BattlePhase.Finished;
            room.frame.Stop();
            foreach (BattlePlayer player in room.players.Values)
            {
                StopReloadTimeout(player);
                player.offline = true;
                player.active = false;
                if (player.connect == null)
                    continue;

                Connect connect = player.connect;
                player.connect = null;
                connectDic.Remove(connect);
                B2C_BattleSettlement settlement = new()
                {
                    battleId = room.battleId,
                    sessionId = room.sessionId,
                    connectVersion = player.connectVersion,
                    sceneVersion = room.sceneVersion,
                    outcome = outcome,
                    characterData = outcome == BattleSettlementOutcome.Escaped
                        ? PlayerCharacterUtility.Clone(player.characterData)
                        : null,
                };
                pendingSettlements[connect] = settlement;
                settlementDeadlines[connect] = NetworkTimer.Instance.TimeNow + ServerUtility.SettlementTimeout;
                connect.Send(settlement);
            }

            room.secretKeys.Clear();
            battleRooms.Remove(room.battleId);
            try { room.world?.Dispose(); }
            finally { room.world = null; SessionEnded?.Invoke(room.sessionId); }
        }

        private bool TryGetTicketPlayer(string ticket, out BattleRoom room, out BattlePlayer player)
        {
            foreach (BattleRoom item in battleRooms.Values)
            {
                if (item.secretKeys.TryGetValue(ticket, out player))
                {
                    room = item;
                    return true;
                }
            }

            room = null;
            player = null;
            return false;
        }

        private string CreateTicket(BattleRoom room, BattlePlayer player)
        {
            List<string> oldTickets = new List<string>();
            foreach (KeyValuePair<string, BattlePlayer> pair in room.secretKeys)
            {
                if (pair.Value == player)
                {
                    oldTickets.Add(pair.Key);
                }
            }

            foreach (string oldTicket in oldTickets)
            {
                room.secretKeys.Remove(oldTicket);
            }

            string ticket = ServerUtility.CreateBattleTicket();
            room.secretKeys.Add(ticket, player);
            return ticket;
        }

        public void Update()
        {
            battleService.Update();
            lobbyService?.Update();
            long now = NetworkTimer.Instance.TimeNow;
            foreach (var pair in new List<KeyValuePair<Connect, long>>(settlementDeadlines))
                if (now >= pair.Value)
                {
                    settlementDeadlines.Remove(pair.Key);
                    pendingSettlements.Remove(pair.Key);
                    battleService.Disconnect(pair.Key);
                }
            foreach (var pair in new List<KeyValuePair<Connect, long>>(enterDeadlines))
                if (now >= pair.Value) { enterDeadlines.Remove(pair.Key); battleService.Disconnect(pair.Key); }

            if (battleRooms.Count == 0)
            {
                return;
            }

            BattleRoom[] rooms = new BattleRoom[battleRooms.Count];
            battleRooms.Values.CopyTo(rooms, 0);
            foreach (BattleRoom room in rooms)
            {
                if (room == null)
                {
                    continue;
                }

                bool allExpired = true;
                foreach (BattlePlayer player in room.players.Values)
                    if (!player.offline || now < player.reconnectUntil) { allExpired = false; break; }
                if (allExpired) { FinishRoom(room); continue; }

                if (room.phase == BattlePhase.Initializing)
                    TryBuildBattleWorld(room);
                else if (room.phase == BattlePhase.Running)
                {
                    if (!TryCompleteBattleExitSelection(room))
                        TrySettleDefeatedRoom(room);
                }
            }
        }

        private void TryBuildBattleWorld(BattleRoom room)
        {
            if (room?.world == null ||
                !room.world.TryGetEntityManager(out EntityManager entityManager))
            {
                return;
            }

            if (!EntitySpawnRegistryUtility.HasRegistry(entityManager))
            {
                return;
            }

            DungeonMapPlan mapPlan = null;
            List<Vector3> playerSpawnPositions = null;
            List<BattlePlayer> orderedPlayers = new List<BattlePlayer>(room.players.Values);
            orderedPlayers.Sort((left, right) =>
            {
                if (left.accountId == right.accountId)
                    return 0;
                if (left.accountId == room.ownerAccountId)
                    return -1;
                if (right.accountId == room.ownerAccountId)
                    return 1;
                return left.accountId.CompareTo(right.accountId);
            });
            string error = null;
            int candidateSeed = room.seed;
            for (int attemptIndex = 0; attemptIndex < 32; attemptIndex++)
            {
                if (DungeonMapPlanBuilder.TryBuildBattle(room.themeKey, candidateSeed, out mapPlan, out error))
                {
                    HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
                    foreach (RuntimeDungeonObstacleSpawnData obstacle in mapPlan.sceneData.ObstacleSpawns)
                    {
                        if (obstacle?.CollisionCells == null)
                            continue;

                        foreach (Vector2Int collisionCell in obstacle.CollisionCells)
                            blockedCells.Add(collisionCell);
                    }

                    List<Vector3> candidatePositions = new List<Vector3>(orderedPlayers.Count);
                    float cellSize = mapPlan.sceneData.CellWorldSize > 0f
                        ? mapPlan.sceneData.CellWorldSize
                        : 1f;
                    int maximumRadius = Mathf.Max(1, mapPlan.layout.EntranceRadius);
                    for (int radius = 0; radius <= maximumRadius && candidatePositions.Count < orderedPlayers.Count; radius++)
                    {
                        for (int offsetY = -radius; offsetY <= radius && candidatePositions.Count < orderedPlayers.Count; offsetY++)
                        {
                            for (int offsetX = -radius; offsetX <= radius && candidatePositions.Count < orderedPlayers.Count; offsetX++)
                            {
                                if (Mathf.Abs(offsetX) + Mathf.Abs(offsetY) != radius)
                                    continue;

                                int x = mapPlan.layout.Entrance.X + offsetX;
                                int y = mapPlan.layout.Entrance.Y + offsetY;
                                Vector2Int cell = new Vector2Int(x, y);
                                if (!mapPlan.layout.IsWalkable(x, y) ||
                                    !mapPlan.layout.IsReachable(x, y) ||
                                    blockedCells.Contains(cell))
                                {
                                    continue;
                                }

                                candidatePositions.Add(mapPlan.sceneData.PlayerSpawnWorldPosition + new Vector3(
                                    offsetX * cellSize,
                                    offsetY * cellSize,
                                    0f));
                            }
                        }
                    }

                    if (candidatePositions.Count >= orderedPlayers.Count)
                    {
                        playerSpawnPositions = candidatePositions;
                        mapPlan.attemptCount = attemptIndex + 1;
                        room.seed = candidateSeed;
                        break;
                    }

                    error = $"Entrance only has {candidatePositions.Count} valid player spawn slots, but {orderedPlayers.Count} are required.";
                    mapPlan = null;
                }

                candidateSeed = ServerUtility.CreateBattleSeed();
            }

            if (mapPlan == null)
            {
                Debug.LogError($"[Battle] Failed to generate battle {room.battleId}: {error}");
                FinishRoom(room);
                return;
            }

            for (int index = 0; index < orderedPlayers.Count; index++)
                orderedPlayers[index].spawnWorldPosition = playerSpawnPositions[index];

            List<NetworkEntitySpawnInfo> playerInfos = new();
            foreach (BattlePlayer player in orderedPlayers)
            {
                if (!player.entered || player.characterData == null)
                {
                    continue;
                }

                NetworkEntitySpawnInfo playerInfo = NetworkEntitySpawnUtility.CreateInfo(
                    NetworkEntityPrefabType.Unit,
                    "PlayerDungeon",
                    player.spawnWorldPosition,
                    player.accountId);
                playerInfo.characterData = player.characterData;
                playerInfo.hasBattlePlayerStatus = true;
                playerInfo.battlePlayerLifeState = BattlePlayerLifeState.Alive;
                playerInfo.battlePlayerConnectionState = player.offline
                    ? BattlePlayerConnectionState.Offline
                    : BattlePlayerConnectionState.Online;
                playerInfo.battlePlayerTransitionReady = false;
                player.lifeState = BattlePlayerLifeState.Alive;
                player.exitReady = false;
                player.unitId = playerInfo.unitId;
                playerInfos.Add(playerInfo);
            }

            if (playerInfos.Count == 0)
            {
                FinishRoom(room);
                return;
            }

            if (!DungeonSceneRuntimeBuilder.TryBuildBattleServer(entityManager, mapPlan, playerInfos))
            {
                Debug.LogError($"[Battle] Failed to create authority entities for battle {room.battleId}.");
                FinishRoom(room);
                return;
            }

            foreach (BattlePlayer player in room.players.Values)
            {
                if (player.unitId != Guid.Empty)
                {
                    NetworkEntitySpawnUtility.TryFindEntity(entityManager, player.unitId, out player.entity);
                    if (player.hasTransferVitals && entityManager.Exists(player.entity))
                    {
                        UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(player.entity);
                        float maxHealth = UnitModifierResolver.GetMaxHealth(entityManager, player.entity);
                        vitality.CurrentHealth = player.reviveOnTransfer ? maxHealth : Mathf.Clamp(player.transferHealth, 0f, maxHealth);
                        entityManager.SetComponentData(player.entity, vitality);
                        UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(player.entity);
                        mana.CurrentMana = Mathf.Clamp(player.transferMana, 0f, UnitModifierResolver.GetMaxMp(entityManager, player.entity));
                        entityManager.SetComponentData(player.entity, mana);
                        player.hasTransferVitals = false;
                    }
                }
            }

            NetworkEntitySpawnUtility.ClearSpawnQueue(entityManager);
            room.entityInfos = NetworkEntitySpawnUtility.CreateSnapshotInfos(entityManager);
            room.battleData = new BattleEnterData
            {
                battleId = room.battleId,
                sceneVersion = room.sceneVersion,
                themeKey = room.themeKey,
                seed = room.seed,
            };
            FinishInitialize(room);
        }

        private bool TryEnsureBattlePlayerEntity(BattleRoom room, BattlePlayer player)
        {
            if (room == null ||
                player == null ||
                player.characterData == null ||
                room.world == null ||
                !room.world.TryGetEntityManager(out EntityManager entityManager))
            {
                return false;
            }

            if (player.unitId != Guid.Empty &&
                NetworkEntitySpawnUtility.TryFindEntity(entityManager, player.unitId, out Entity existingEntity))
            {
                player.entity = existingEntity;
                return true;
            }

            NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                NetworkEntityPrefabType.Unit,
                "PlayerDungeon",
                player.spawnWorldPosition,
                player.accountId);
            entityInfo.characterData = player.characterData;
            entityInfo.hasBattlePlayerStatus = true;
            entityInfo.battlePlayerLifeState = player.lifeState;
            entityInfo.battlePlayerConnectionState = BattlePlayerConnectionState.Offline;
            entityInfo.battlePlayerTransitionReady = false;
            if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity playerEntity))
            {
                return false;
            }

            player.unitId = entityInfo.unitId;
            player.entity = playerEntity;
            if (room.phase == BattlePhase.WaitingForClientReady)
            {
                List<NetworkEntitySpawnInfo> entityInfos = new((room.entityInfos?.Length ?? 0) + 1);
                if (room.entityInfos != null)
                {
                    entityInfos.AddRange(room.entityInfos);
                }

                entityInfos.Add(entityInfo);
                room.entityInfos = entityInfos.ToArray();
            }

            return true;
        }

        public void Cleanup()
        {
            battleService?.Shutdown();
            lobbyService?.Shutdown();

            foreach (BattleRoom room in battleRooms.Values)
            {
                try { room.frame?.Stop(); }
                catch (Exception exception) { Debug.LogException(exception); }
                try { room.world?.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
                finally { room.world = null; }
                if (room.phaseTimerId != 0)
                {
                    NetworkTimer.Instance.Remove(room.phaseTimerId);
                }

                foreach (BattlePlayer player in room.players.Values)
                {
                    StopReloadTimeout(player);
                }
            }

            battleRooms.Clear();
            pendingSettlements.Clear();
            settlementDeadlines.Clear();
            enterDeadlines.Clear();
            connectDic.Clear();
            lobbyConnect = null;
            battleService = null;
            lobbyService = null;
            SessionEnded = null;
        }

        private static bool MatchesSessionPeer(BattleRoom room, BattlePlayer player, Connect connect, string sessionId) =>
            room != null && room.sessionId == sessionId &&
            HostedBattlePolicy.MatchesPeer(room.connection, player.accountId, connect.RemoteEndpoint);

        private void OnSettlementAck(IMessage message, Connect connect)
        {
            if (message is not C2B_BattleSettlementAck ack || !pendingSettlements.TryGetValue(connect, out B2C_BattleSettlement result) ||
                ack.sessionId != result.sessionId || ack.battleId != result.battleId || ack.connectVersion != result.connectVersion) return;
            pendingSettlements.Remove(connect);
            settlementDeadlines.Remove(connect);
            battleService.DisconnectAfterSend(connect);
        }
    }
}
