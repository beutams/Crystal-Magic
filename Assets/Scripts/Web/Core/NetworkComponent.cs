using CrystalMagic.Core;
using UnityEngine;

namespace Server
{
    public enum NetworkRole
    {
        Client,
        LobbyServer,
        BattleServer,
    }

    public class NetworkComponent : GameComponent<NetworkComponent>
    {
        [SerializeField] private NetworkRole debugRole = NetworkRole.Client;
        [SerializeField] private bool debugFrameSpeedAdjustment = true;

        public ClientService clientServic;
        public ClientLobbyManager clientLobbyManager;
        public ClientBattleManager clientBattleManager;
        public ServerLobbyManager serverLobbyManager;
        public ServerBattleManager serverBattleManager;

        public NetworkRole Role
        {
            get
            {
#if CRYSTAL_MAGIC_LOBBY_SERVER
                return NetworkRole.LobbyServer;
#elif CRYSTAL_MAGIC_BATTLE_SERVER
                return NetworkRole.BattleServer;
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
            TCPPacketCode.Init();

            switch (Role)
            {
                case NetworkRole.Client:
                    clientServic = new ClientService();
                    clientServic.Init();
                    clientBattleManager = new ClientBattleManager();
                    clientBattleManager.frame.SetFrameSpeedAdjustmentEnabled(debugFrameSpeedAdjustment);
                    clientLobbyManager = new ClientLobbyManager();
                    break;
                case NetworkRole.LobbyServer:
                    serverLobbyManager = new ServerLobbyManager();
                    serverLobbyManager.Initialize();
                    break;
                case NetworkRole.BattleServer:
                    serverBattleManager = new ServerBattleManager();
                    serverBattleManager.Initialize();
                    break;
            }
        }

        private void LateUpdate()
        {
            switch (Role)
            {
                case NetworkRole.Client:
                    clientServic.Update();
                    clientBattleManager.Update();
                    break;
                case NetworkRole.LobbyServer:
                    serverLobbyManager.Update();
                    break;
                case NetworkRole.BattleServer:
                    serverBattleManager.Update();
                    break;
            }

            NetworkTimer.Instance.Update();
        }

        public void SetFrameSpeedAdjustmentEnabled(bool enabled)
        {
            debugFrameSpeedAdjustment = enabled;
            clientBattleManager?.frame.SetFrameSpeedAdjustmentEnabled(enabled);
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
            clientLobbyManager?.Cleanup();
            clientBattleManager?.Cleanup();
            clientServic?.Shutdown();
            serverLobbyManager?.Cleanup();
            serverBattleManager?.Cleanup();
            NetworkTimer.Instance.Clear();

            clientLobbyManager = null;
            clientBattleManager = null;
            clientServic = null;
            serverLobbyManager = null;
            serverBattleManager = null;

            base.Cleanup();
        }
    }
}
