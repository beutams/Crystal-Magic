using System;
using System.Collections.Generic;
using System.Net;
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
            if (!SteamComponent.Instance.Account.IsValid || !HostedBattlePolicy.IsHostRequest(request,
                SteamComponent.Instance.Account.NetworkAccountId,
                SteamComponent.Instance.CanUseSteamP2P ? SteamComponent.Instance.SteamId : 0))
                throw new ArgumentException("Invalid host session request.");
            if (IsHosting)
            {
                if (Connection.sessionId == request.connection.sessionId) return startResult;
                throw new InvalidOperationException("Another hosted battle is still active.");
            }
            try
            {
                Connection = request.connection;
                LoopbackTransport.CreatePair(Connection.hostAccountId, out LoopbackTransport server, out LoopbackTransport client);
                LocalClient = client;
                List<IServerTransport> listeners = new() { server };
                ServerService tcp = null;
                if (request.tcpRequired)
                {
                    IPAddress advertised = IPAddress.Parse(Connection.address);
                    IPAddress bind = IPAddress.IsLoopback(advertised) ? advertised :
                        advertised.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any;
                    tcp = new ServerService(new TcpEndpoint(new IPEndPoint(bind, Connection.port)));
                    listeners.Add(tcp);
                }
                if (Connection.HasSteamEndpoint)
                {
                    listeners.Add(new SteamP2PTransport(new SteamEndpoint(Connection.hostSteamId, Connection.steamPort), request.steamMembers));
                }
                Runtime = new ServerBattleManager();
                Runtime.SessionEnded += OnSessionEnded;
                Runtime.Initialize(new CompositeServerTransport(listeners.ToArray()));
                if (tcp != null) Connection.port = ((TcpEndpoint)tcp.LocalEndpoint).Address.Port;
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
