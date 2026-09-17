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
