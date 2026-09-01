using CrystalMagic.Core;

namespace Server
{
    public class ClientBattleManager : Singleton<ClientBattleManager>
    {
        public ClientService battleServic;
        public Connect battleConnect;
        protected override void Awake()
        {
            base.Awake();
            battleServic = new ClientService();
            battleServic.Init();
        }
        public void ConnectWithTicket(string ticket)
        {
            battleServic.Connect(ServerUtility.GetLobbyIPEndPoint(), out battleConnect);
        }
    }
}