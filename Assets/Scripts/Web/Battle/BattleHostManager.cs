using System;
using CrystalMagic.Core;
using UnityEngine;

namespace Server
{
    /// <summary>客户端角色不变，额外持有独立的权威 BattleWorld。由 NetworkComponent 统一驱动和释放。</summary>
    public sealed class BattleHostManager
    {
        public ServerBattleManager Runtime { get; private set; }
        public BattleConnectionInfo Connection { get; private set; }
        public LoopbackTransport LocalClient { get; private set; }
        public bool IsHosting => Runtime != null;
        private B2L_StartRoomResult startResult;
        private long shutdownAt;
        public event Action<string> SessionEnded;

        public B2L_StartRoomResult Start(L2C_HostBattleStart request)
        {
            if (request?.connection == null || !request.connection.IsValid || request.room == null ||
                request.connection.kind != BattleTransportKind.Steam ||
                request.connection.hostSteamId != SteamComponent.Instance.SteamId ||
                request.room.ownerAccountId != request.connection.hostSteamId)
                throw new ArgumentException("Invalid host session request.");
            if (IsHosting)
            {
                if (Connection.sessionId == request.connection.sessionId) return startResult;
                throw new InvalidOperationException("Another hosted battle is still active.");
            }
            try
            {
                Connection = request.connection;
                LoopbackTransport.CreatePair(Connection.hostSteamId, out LoopbackTransport server, out LoopbackTransport client);
                LocalClient = client;
                SteamP2PTransport steam = new(new SteamEndpoint(Connection.hostSteamId, Connection.port), request.room.players);
                Runtime = new ServerBattleManager();
                Runtime.SessionEnded += OnSessionEnded;
                Runtime.Initialize(new CompositeServerTransport(steam, server));
                startResult = Runtime.CreateSession(request.room, Connection) ??
                    throw new InvalidOperationException("Invalid battle roster.");
                // HostReady 只表示已监听且建好入场房间；World 要等角色数据入场后才初始化。
                return startResult;
            }
            catch { Shutdown(); throw; }
        }

        public B2L_ReloadRoomResult QueryReload(L2C_HostReloadRequest request) =>
            Runtime != null && shutdownAt == 0 && request.sessionId == Connection.sessionId
                ? Runtime.QueryReload(request.request) : null;

        private void OnSessionEnded(string sessionId)
        {
            if (shutdownAt != 0) return;
            shutdownAt = NetworkTimer.Instance.TimeNow + ServerUtility.SettlementTimeout;
            SessionEnded?.Invoke(sessionId);
        }

        public void Cancel(string sessionId)
        {
            if (Runtime == null || Connection.sessionId != sessionId) return;
            Runtime.EndSession(sessionId);
            OnSessionEnded(sessionId);
        }

        public void Update()
        {
            if (Runtime == null) return;
            try { Runtime.Update(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Cancel(Connection.sessionId);
            }
            if (shutdownAt != 0 && NetworkTimer.Instance.TimeNow >= shutdownAt) Shutdown();
        }

        public void Shutdown()
        {
            ServerBattleManager previous = Runtime;
            Runtime = null;
            try { previous?.Cleanup(); }
            finally
            {
                LocalClient?.Shutdown();
                LocalClient = null;
                Connection = null;
                startResult = null;
                shutdownAt = 0;
            }
        }
    }
}
