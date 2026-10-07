using System;
using UnityEngine;

namespace Server
{
    public partial class ClientLobbyManager
    {
        public bool BattleControlActive { get; private set; }
        private string activeSessionId;
        private long loginDeadline;
        private long reconnectAt;
        private string pendingHostSessionId;

        private void RegisterHostCallbacks(Connect connect)
        {
            connect.RegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleStart>(), OnHostBattleStart);
            connect.RegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleCancel>(), OnHostBattleCancel);
            connect.RegisterCallback(MessageCodec.GetOpcode<L2C_HostReloadRequest>(), OnHostReloadRequest);
        }
        private void UnregisterHostCallbacks(Connect connect)
        {
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleStart>(), OnHostBattleStart);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleCancel>(), OnHostBattleCancel);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_HostReloadRequest>(), OnHostReloadRequest);
        }

        private void OnHostBattleStart(IMessage message, Connect connect)
        {
            if (connect != lobbyConnect || message is not L2C_HostBattleStart start ||
                room == null || start.connection?.hostAccountId != accountId || start.room?.roomId != room.roomId ||
                start.room.ownerAccountId != room.ownerAccountId)
                return;
            B2L_StartRoomResult result;
            pendingHostSessionId = start.connection?.sessionId;
            try
            {
                if (!NetworkComponent.Instance.clientBattleManager.PrepareForOnlineBattle(out string error))
                    throw new InvalidOperationException(error);
                result = NetworkComponent.Instance.battleHostManager.Start(start);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = new B2L_StartRoomResult { roomId = room.roomId, connection = start.connection, error = "房主初始化失败。" };
            }
            connect.Send(new C2L_HostBattleReady { result = result });
        }

        private void OnHostBattleCancel(IMessage message, Connect connect)
        {
            if (connect != lobbyConnect || message is not L2C_HostBattleCancel cancel) return;
            NetworkComponent.Instance.battleHostManager.Cancel(cancel.sessionId);
            if (activeSessionId == cancel.sessionId && NetworkComponent.Instance.clientBattleManager.HasPreBattleSnapshot)
                NetworkComponent.Instance.clientBattleManager.AbortBattle(cancel.error ?? "房主会话已经结束。");
            else if (!BattleControlActive && pendingHostSessionId == cancel.sessionId)
            {
                NetworkComponent.Instance.clientBattleManager.ClearPreBattleSnapshot();
                pendingHostSessionId = null;
            }
        }

        private void OnHostReloadRequest(IMessage message, Connect connect)
        {
            if (connect != lobbyConnect || message is not L2C_HostReloadRequest request) return;
            B2L_ReloadRoomResult result = NetworkComponent.Instance.battleHostManager.QueryReload(request) ?? new B2L_ReloadRoomResult
            {
                accountId = request.request?.accountId ?? 0,
                saveGuid = request.request?.saveGuid,
                type = BattleReloadRoomResultType.NoBattle,
            };
            connect.Send(new C2L_HostReloadResult { sessionId = request.sessionId, requestId = request.requestId, result = result });
        }

        public void NotifyHostSessionEnded(string sessionId)
        {
            if (lobbyConnect?.State == ConnectState.Connected)
                lobbyConnect.Send(new C2L_HostBattleEnded { sessionId = sessionId });
        }

        public void CompleteBattleControl()
        {
            string previous = activeSessionId;
            activeSessionId = null;
            pendingHostSessionId = null;
            BattleControlActive = false;
            if (previous != null) NetworkComponent.Instance.battleHostManager?.Cancel(previous);
            Cleanup();
        }

        private void CancelPendingHost()
        {
            if (BattleControlActive || pendingHostSessionId == null) return;
            string previous = pendingHostSessionId;
            pendingHostSessionId = null;
            NetworkComponent.Instance.battleHostManager?.Cancel(previous);
            NetworkComponent.Instance.clientBattleManager.ClearPreBattleSnapshot();
        }

        public void Update()
        {
            long now = NetworkTimer.Instance.TimeNow;
            if (lobbyConnect != null && loginDeadline != 0 && now >= loginDeadline)
            {
                loginDeadline = 0;
                LoginFailure = LobbyRequestType.LoginFail;
                clientServic.Disconnect(lobbyConnect);
            }
            if (BattleControlActive && lobbyConnect == null && now >= reconnectAt)
            {
                reconnectAt = now + ServerUtility.BattleLobbyReconnectInterval;
                try { Initialize(); }
                catch (Exception exception) { Debug.LogWarning("[Lobby] Reconnect failed: " + exception.Message); }
            }
        }
    }
}
