using CrystalMagic.Core;
using UnityEngine;

namespace Server
{
    public enum NetworkRole
    {
        Client,
        LobbyServer,
    }

    public class NetworkComponent : GameComponent<NetworkComponent>
    {
        [SerializeField] private NetworkRole debugRole = NetworkRole.Client;
        [SerializeField] private bool debugFrameSpeedAdjustment = true;
        [Tooltip("Steam 模式使用客户端托管（Steam / Socket 混合房间）；Tcp 为独立 BattleServer。")]
        [SerializeField] private BattleTransportKind battleHosting = BattleTransportKind.Steam;

        public IClientTransport lobbyTransport;
        public IClientTransport battleTransport;
        public ClientLobbyManager clientLobbyManager;
        public ClientBattleManager clientBattleManager;
        public ServerLobbyManager serverLobbyManager;
        public BattleHostManager battleHostManager;

        public NetworkRole Role
        {
            get
            {
#if CRYSTAL_MAGIC_LOBBY_SERVER
                return NetworkRole.LobbyServer;
#elif CRYSTAL_MAGIC_CLIENT
                return NetworkRole.Client;
#else
                return debugRole;
#endif
            }
        }

        public override int Priority => 14;

        public override void Initialize()
        {
            base.Initialize();

            Application.runInBackground = true;
            MessageCodec.Init();

            switch (Role)
            {
                case NetworkRole.Client:
                    lobbyTransport = new ClientService();
                    lobbyTransport.Init();
                    clientBattleManager = new ClientBattleManager();
                    clientBattleManager.frame.SetFrameSpeedAdjustmentEnabled(debugFrameSpeedAdjustment);
                    clientLobbyManager = new ClientLobbyManager();
                    battleHostManager = new BattleHostManager();
                    battleHostManager.SessionEnded += clientLobbyManager.NotifyHostSessionEnded;
                    break;
                case NetworkRole.LobbyServer:
                    var serverConfig = ClientAccountPolicy.ForLaunch(new CrystalMagic.Game.Config.LobbyAccountConfig
                    { lobbyAddress = ServerUtility.lobbyIP, lobbyPort = ServerUtility.lobbyPort },
                        System.Environment.GetCommandLineArgs(), false);
                    serverLobbyManager = new ServerLobbyManager();
                    serverLobbyManager.Initialize(new ServerService(new TcpEndpoint(new System.Net.IPEndPoint(
                        System.Net.IPAddress.Parse(serverConfig.lobbyAddress), serverConfig.lobbyPort))),
                        battleHosting == BattleTransportKind.Tcp ? new ClientService() : null,
                        battleHosting == BattleTransportKind.Steam);
                    break;
            }
        }

        private void LateUpdate()
        {
            try
            {
                switch (Role)
                {
                case NetworkRole.Client:
                    lobbyTransport?.Update();
                    battleHostManager?.Update();
                    battleTransport?.Update();
                    clientBattleManager?.Update();
                    clientLobbyManager?.Update();
                    break;
                case NetworkRole.LobbyServer:
                    serverLobbyManager.Update();
                    break;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                if (Role == NetworkRole.Client)
                    clientBattleManager?.AbortBattle("网络处理异常，正在安全退出战斗。");
            }
            finally { NetworkTimer.Instance.Update(); }
        }

        public void SetFrameSpeedAdjustmentEnabled(bool enabled)
        {
            debugFrameSpeedAdjustment = enabled;
            clientBattleManager?.frame.SetFrameSpeedAdjustmentEnabled(enabled);
        }

        public void OpenBattleTransport(BattleConnectionInfo connection)
        {
            CloseBattleTransport();
            if (connection == null || !connection.IsValid)
                throw new System.ArgumentException("Invalid battle connection descriptor.");
            if (battleHostManager.IsHosting && battleHostManager.Connection.sessionId == connection.sessionId)
                battleTransport = battleHostManager.LocalClient;
            else if (connection.kind == BattleTransportKind.Tcp)
                battleTransport = new ClientService();
            else
                battleTransport = new SteamP2PTransport();
            battleTransport.Init();
        }

        public void CloseBattleTransport()
        {
            IClientTransport previous = battleTransport;
            battleTransport = null;
            previous?.Shutdown();
        }

        private void OnValidate()
        {
            clientBattleManager?.frame.SetFrameSpeedAdjustmentEnabled(debugFrameSpeedAdjustment);
        }

        [ContextMenu("Debug/启用客户端帧调速")]
        public void EnableFrameSpeedAdjustment() => SetFrameSpeedAdjustmentEnabled(true);

        [ContextMenu("Debug/停用客户端帧调速")]
        public void DisableFrameSpeedAdjustment() => SetFrameSpeedAdjustmentEnabled(false);

        [ContextMenu("Debug/输出客户端帧时钟")]
        public void LogFrameClock()
        {
            if (clientBattleManager == null)
                return;
            ClientFrameManager frame = clientBattleManager.frame;
            Debug.Log($"[FrameClock] enabled={frame.FrameSpeedAdjustmentEnabled}, frame={frame.currentFrame}, " +
                $"server≈{frame.EstimatedServerFrame:F2}, ahead={frame.CurrentAheadFrames:F2}/{frame.TargetAheadFrames}, " +
                $"RTT={frame.SmoothedRttMs:F1}ms, jitter={frame.RttJitterMs:F1}ms, speed={frame.SimulationSpeed:F3}");
        }

        public override void Cleanup()
        {
            SafeCleanup(() => clientLobbyManager?.Cleanup());
            SafeCleanup(() => clientBattleManager?.Cleanup());
            SafeCleanup(() => lobbyTransport?.Shutdown());
            SafeCleanup(CloseBattleTransport);
            SafeCleanup(() => battleHostManager?.Shutdown());
            SafeCleanup(() => serverLobbyManager?.Cleanup());
            NetworkTimer.Instance.Clear();

            clientLobbyManager = null;
            clientBattleManager = null;
            lobbyTransport = null;
            serverLobbyManager = null;
            battleHostManager = null;

            base.Cleanup();
        }

        private static void SafeCleanup(System.Action action)
        {
            try { action(); }
            catch (System.Exception exception) { Debug.LogException(exception); }
        }
    }
}
