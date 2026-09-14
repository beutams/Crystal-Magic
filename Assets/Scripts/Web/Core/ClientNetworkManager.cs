using CrystalMagic.Core;
using UnityEngine;

namespace Server
{
    public class ClientNetworkManager : GameComponent<ClientNetworkManager>
    {
        public ClientService clientServic;
        public ClientLobbyManager clientLobbyManager;
        public ClientBattleManager clientBattleManager;

        public override int Priority => 31;

        public override void Initialize()
        {
            base.Initialize();

            Application.runInBackground = true;
            TCPPacketCode.Init();

            clientServic = new ClientService();
            clientServic.Init();

            clientBattleManager = new ClientBattleManager();
            clientLobbyManager = new ClientLobbyManager();
        }

        private void LateUpdate()
        {
            if (clientServic == null)
            {
                return;
            }

            clientServic.Update();
            NetworkTimer.Instance.Update();
        }

        public override void Cleanup()
        {
            clientLobbyManager?.Cleanup();
            clientBattleManager?.Cleanup();
            clientServic?.Shutdown();
            NetworkTimer.Instance.Clear();

            clientLobbyManager = null;
            clientBattleManager = null;
            clientServic = null;

            base.Cleanup();
        }
    }
}
