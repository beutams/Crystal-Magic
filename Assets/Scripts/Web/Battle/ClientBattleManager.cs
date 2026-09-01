using CrystalMagic.Core;
using System;

namespace Server
{
    public class ClientBattleManager : Singleton<ClientBattleManager>
    {
        public ClientService battleServic;
        public Connect battleConnect;
        public string ticket;
        public Action onEnterBattle;
        public Action onEnterBattleFail;
        protected override void Awake()
        {
            base.Awake();
            battleServic = ClientNetworkManager.Instance.clientServic;
        }
        public void ConnectWithTicket(string ticket)
        {
            if (battleConnect != null || string.IsNullOrEmpty(ticket))
            {
                return;
            }

            this.ticket = ticket;
            battleServic.Connect(ServerUtility.GetBattleIPEndPoint(), out battleConnect);
            battleConnect.OnConnected += OnBattleConnected;
            battleConnect.OnDisconnected += OnBattleDisconnected;
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleResult>(), OnEnterBattleResult);
        }
        private void OnBattleConnected(Connect connect)
        {
            connect.Send(new C2B_EnterBattle() { ticket = ticket });
        }
        private void OnEnterBattleResult(IMessage message, Connect connect)
        {
            B2C_EnterBattleResult realMessage = message as B2C_EnterBattleResult;
            if (realMessage == null)
            {
                return;
            }

            switch (realMessage.type)
            {
                case BattleRequestType.EnterBattleSuccess:
                    ticket = null;
                    onEnterBattle?.Invoke();
                    break;
                case BattleRequestType.EnterBattleFail:
                    ticket = null;
                    onEnterBattleFail?.Invoke();
                    battleServic.Disconnect(connect);
                    break;
            }
        }
        private void OnBattleDisconnected(Connect connect)
        {
            if (battleConnect != connect)
            {
                return;
            }

            bool enterBattleFail = ticket != null;
            battleConnect = null;
            ticket = null;
            if (enterBattleFail)
            {
                onEnterBattleFail?.Invoke();
            }
        }
    }
}
