using UnityEngine;

namespace Server
{
    public class StartMono : MonoBehaviour
    {
        public bool server;
        public Service service;
        private void Awake()
        {
            Application.runInBackground = true;
            if (server)
            {
                ServerService service = new ServerService();
                service.OnAccept += (connect) =>
                {
                    connect.RegisterCallback(TCPPacketCode.messages[typeof(C2S_Ping)], (message,connect) =>
                    {
                        Debug.Log($"Server Recv Ping, Client Time Is {((C2S_Ping)message).Time}");
                    });
                };
                this.service = service;
            }
            else
            {
                ClientService service = new ClientService();
                service.Connect(ServerUtility.GetLoginInIPEndPoint());
                service.OnConnectedSuccess += (connect) =>
                {
                    connect.RegisterCallback(TCPPacketCode.messages[typeof(S2C_Pong)], (message,connect) =>
                    {
                        Debug.Log($"Client Recv Pong, Server Time Is {((S2C_Pong)message).Time}");
                    });
                };
                this.service = service;
            }
        }
        private void Start()
        {
            service.Init();
            TCPPacketCode.Init();
            Debug.Log($"[TCP][Boot] Role={(server ? "Server" : "Client")}, Service={service.GetType().Name}");
        }
        private void LateUpdate()
        {
            service.Update();
            TimerManager.Instance.Update();
        }
    }
}
