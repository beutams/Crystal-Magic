using System;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public partial class ServerLobbyManager
    {
        private sealed class HostedSession
        {
            public BattleConnectionInfo Connection;
            public L2B_StartRoom Request;
            public Connect Host;
            public bool Ready;
            public long Deadline;
        }
        private sealed class HostedReload
        {
            public string SessionId;
            public Player Player;
            public long Deadline;
        }
        public bool UseSteamHosting { get; private set; }
        private readonly Dictionary<string, HostedSession> hostedSessions = new();
        private readonly Dictionary<string, HostedReload> hostedReloads = new();
        private readonly Dictionary<Connect, long> loginTimeouts = new();

        private void RegisterHostControl(Connect connect)
        {
            connect.RegisterCallback(MessageCodec.GetOpcode<C2L_HostBattleReady>(), OnHostReady);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2L_HostBattleEnded>(), OnHostEnded);
            connect.RegisterCallback(MessageCodec.GetOpcode<C2L_HostReloadResult>(), OnHostReloadResult);
        }

        private void StartHostedRoom(Room room, Connect owner)
        {
            if (room.players.Values.Any(player => !player.steamP2PAvailable || player.connect?.State != ConnectState.Connected))
            { owner.Send(new L2C_StartReturn { type = LobbyRequestType.StartFail }); return; }
            string sessionId = Guid.NewGuid().ToString("N");
            HostedSession session = new()
            {
                Connection = new BattleConnectionInfo { kind = BattleTransportKind.Steam, sessionId = sessionId,
                    hostSteamId = room.ownerAccountId, port = 0 },
                Request = new L2B_StartRoom
                {
                    roomId = room.roomId, sessionId = sessionId, ownerAccountId = room.ownerAccountId, themeKey = room.themeKey,
                    players = room.players.Keys.ToArray(), saveGuids = room.players.ToDictionary(pair => pair.Key, pair => pair.Value.saveGuid),
                },
                Host = owner,
                Deadline = NetworkTimer.Instance.TimeNow + ServerUtility.HostStartTimeout,
            };
            hostedSessions.Add(sessionId, session);
            room.start = true;
            room.startSessionId = sessionId;
            room.startDeadline = session.Deadline;
            RoomData roomData = RoomData.CreateRoomData(room);
            foreach (Player player in room.players.Values) player.connect.Send(new L2C_RefreshRoomInfo { roomData = roomData });
            owner.Send(new L2C_HostBattleStart { connection = session.Connection, room = session.Request });
        }

        private void OnHostReady(IMessage message, Connect connect)
        {
            if (message is not C2L_HostBattleReady ready || ready.result?.connection?.sessionId == null ||
                !hostedSessions.TryGetValue(ready.result.connection.sessionId, out HostedSession session) ||
                session.Host != connect || session.Ready ||
                !roomList.TryGetValue(session.Request.roomId, out Room room) || room.startSessionId != session.Connection.sessionId)
                return;
            B2L_StartRoomResult result = ready.result;
            if (result.error != null || result.secretKeys == null || session.Deadline <= NetworkTimer.Instance.TimeNow ||
                session.Request.players.Any(id => !result.secretKeys.TryGetValue(id, out string ticket) || string.IsNullOrEmpty(ticket)))
            { CancelHostedStart(session); return; }
            session.Ready = true;
            session.Deadline = 0;
            foreach (Player player in room.players.Values)
            {
                player.roomId = 0;
                player.start = true;
                player.connect.Send(new L2C_StartTicket
                { connection = session.Connection, ticket = result.secretKeys[player.accountId], reload = false });
            }
            room.players.Clear();
            roomList.Remove(room.roomId);
            BroadcastRoomListChanged();
        }

        private void ResetStartingRoom(Room room)
        {
            room.start = false;
            room.startSessionId = null;
            room.startDeadline = 0;
            foreach (Player player in room.players.Values) player.ready = false;
            RoomData data = RoomData.CreateRoomData(room);
            foreach (Player player in room.players.Values)
            {
                player.connect?.Send(new L2C_RefreshRoomInfo { roomData = data });
                player.connect?.Send(new L2C_StartReturn { type = LobbyRequestType.StartFail });
            }
        }

        private void CancelHostedStart(HostedSession session)
        {
            hostedSessions.Remove(session.Connection.sessionId);
            session.Host?.Send(new L2C_HostBattleCancel { sessionId = session.Connection.sessionId, error = "房主创建失败或队员已断开，请重新准备。" });
            if (roomList.TryGetValue(session.Request.roomId, out Room room) && room.startSessionId == session.Connection.sessionId)
                ResetStartingRoom(room);
        }

        private void OnHostEnded(IMessage message, Connect connect)
        {
            if (message is not C2L_HostBattleEnded ended || ended.sessionId == null ||
                !hostedSessions.TryGetValue(ended.sessionId, out HostedSession session) || session.Host != connect) return;
            if (!session.Ready) { CancelHostedStart(session); return; }
            RemoveHostedSession(session);
        }

        private void RemoveHostedSession(HostedSession session)
        {
            hostedSessions.Remove(session.Connection.sessionId);
            foreach (ulong id in session.Request.players)
                if (playerList.TryGetValue(id, out Player player)) player.start = false;
            // 战斗连接自行处理结算/断线，不通过大厅消息抢先打断正在传输的结算。
        }

        private void BeginHostedLogin(Player player, C2L_LoginLobby login)
        {
            HostedSession session = hostedSessions.Values.FirstOrDefault(candidate => candidate.Ready &&
                candidate.Request.saveGuids.ContainsKey(player.accountId));
            if (session == null) { CreatePlayer(player); return; }
            if (session.Request.saveGuids[player.accountId] != player.saveGuid)
            {
                player.connect.Send(new L2C_LoginLobbyResult { type = LobbyRequestType.LoginSaveMismatch });
                lobbyService.DisconnectAfterSend(player.connect);
                return;
            }
            // 已在战斗中的连接只是恢复大厅控制通道，不能重新换票踢掉健康的 P2P 连接。
            if (login.activeSessionId == session.Connection.sessionId)
            {
                player.start = true;
                CreatePlayer(player);
                if (player.accountId == session.Connection.hostSteamId)
                { session.Host = player.connect; session.Deadline = 0; }
                return;
            }
            if (player.accountId == session.Connection.hostSteamId)
            {
                RemoveHostedSession(session); // 房主进程已重启，没有可迁移的权威 World。
                CreatePlayer(player);
                return;
            }
            if (session.Host?.State != ConnectState.Connected)
            {
                player.connect.Send(new L2C_LoginLobbyResult { type = LobbyRequestType.LoginFail });
                lobbyService.DisconnectAfterSend(player.connect);
                return;
            }
            string requestId = Guid.NewGuid().ToString("N");
            hostedReloads.Add(requestId, new HostedReload { SessionId = session.Connection.sessionId, Player = player,
                Deadline = NetworkTimer.Instance.TimeNow + ServerUtility.Timeout });
            session.Host.Send(new L2C_HostReloadRequest
            {
                sessionId = session.Connection.sessionId, requestId = requestId,
                request = new L2B_TryReloadRoom { accountId = player.accountId, saveGuid = player.saveGuid },
            });
        }

        private void OnHostReloadResult(IMessage message, Connect connect)
        {
            if (message is not C2L_HostReloadResult response || response.requestId == null || response.sessionId == null ||
                !hostedReloads.TryGetValue(response.requestId, out HostedReload pending) ||
                pending.SessionId != response.sessionId || !hostedSessions.TryGetValue(pending.SessionId, out HostedSession session) ||
                session.Host != connect || response.result?.accountId != pending.Player.accountId ||
                response.result.saveGuid != pending.Player.saveGuid) return;
            hostedReloads.Remove(response.requestId);
            Player player = pending.Player;
            if (player.connect.State != ConnectState.Connected || !pendingPlayerList.ContainsKey(player.accountId)) return;
            if (response.result.type == BattleReloadRoomResultType.NoBattle) { CreatePlayer(player); return; }
            if (response.result.type != BattleReloadRoomResultType.ReloadAvailable || string.IsNullOrEmpty(response.result.ticket))
            {
                player.connect.Send(new L2C_LoginLobbyResult { type = LobbyRequestType.LoginFail });
                lobbyService.DisconnectAfterSend(player.connect);
                return;
            }
            player.start = true;
            CreatePlayer(player);
            player.connect.Send(new L2C_StartTicket { reload = true, connection = session.Connection, ticket = response.result.ticket });
        }

        private void OnHostedLobbyDisconnected(Connect connect)
        {
            loginTimeouts.Remove(connect);
            foreach (HostedSession session in hostedSessions.Values.ToArray())
            {
                bool member = connectAccountDic.TryGetValue(connect, out ulong id) && session.Request.saveGuids.ContainsKey(id);
                if (!session.Ready && member) { CancelHostedStart(session); continue; }
                if (session.Host == connect)
                {
                    session.Host = null;
                    session.Deadline = NetworkTimer.Instance.TimeNow + ServerUtility.ReconnectGrace;
                }
            }
            foreach (var pair in hostedReloads.ToArray())
                if (pair.Value.Player.connect == connect) hostedReloads.Remove(pair.Key);
        }

        private void UpdateHostedSessions()
        {
            long now = NetworkTimer.Instance.TimeNow;
            foreach (var pair in loginTimeouts.ToArray())
                if (now >= pair.Value)
                { loginTimeouts.Remove(pair.Key); lobbyService.Disconnect(pair.Key); }
            foreach (HostedSession session in hostedSessions.Values.ToArray())
            {
                if (session.Deadline == 0 || now < session.Deadline) continue;
                if (!session.Ready) CancelHostedStart(session);
                else RemoveHostedSession(session);
            }
            foreach (Room room in roomList.Values.ToArray())
                if (room.start && room.startDeadline != 0 && now >= room.startDeadline) ResetStartingRoom(room);
            foreach (var pair in hostedReloads.ToArray())
                if (now >= pair.Value.Deadline)
                {
                    hostedReloads.Remove(pair.Key);
                    pair.Value.Player.connect.Send(new L2C_LoginLobbyResult { type = LobbyRequestType.LoginFail });
                    lobbyService.DisconnectAfterSend(pair.Value.Player.connect);
                }
        }
    }
}
