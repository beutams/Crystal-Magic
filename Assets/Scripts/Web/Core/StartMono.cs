using UnityEngine;

namespace Server
{
    public class StartMono : MonoBehaviour
    {
        public Service service;
        private void Awake()
        {
            Application.runInBackground = true;
            ServerService serverService = new ServerService(ServerUtility.GetLoginInIPEndPoint());
            serverService.OnAccept += (connect) =>
            {
                connect.RegisterCallback(TCPPacketCode.messages[typeof(C2S_Ping)], (message,connect) =>
                {
                    Debug.Log($"Server Recv Ping, Client Time Is {((C2S_Ping)message).Time}");
                });
            };
            service = serverService;
        }
        private void Start()
        {
            TCPPacketCode.Init();
            service.Init();
            Debug.Log($"[TCP][Boot] Role=Server, Service={service.GetType().Name}");
        }
        private void LateUpdate()
        {
            service.Update();
            NetworkTimer.Instance.Update();
        }
    }
}
