using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Server
{
    public class ClientService : Service
    {
        public Action<Connect> OnConnecting;
        public Action<Connect> OnConnectedSuccess;
        public Action<Connect> OnConnectedFail;

        protected Dictionary<Guid, Task> connectingTask;
        protected Dictionary<Connect, long> timerIds;
        public void Connect(IPEndPoint iPEndPoint,out Connect connect)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            TCPPair pair = TCPPair.CreateTCPPair(socket, iPEndPoint, out Guid id);
            connect = pair.connect;
            connects.Add(id, pair);
            Debug.Log($"[TCP][Client] Created Connect={id}, Target={iPEndPoint}");
        }
        public override void Init()
        {
            connectingTask = new Dictionary<Guid, Task>();
            timerIds = new Dictionary<Connect, long>();
            startTime = TimerManager.Instance.TimeNow;
            OnConnectedSuccess += (connect) =>
            {
                long timerid = TimerManager.Instance.AddRepeated(ServerUtility.PingInterval, () =>
                {
                    Debug.Log($"[TCP][Client] Heartbeat triggered, Connect={connect.IPEndPoint}");
                    connect.Send(new C2S_Ping() { Time = TimerManager.Instance.TimeNow });
                });
                //connect.RegisterCallback(TCPPacketCode.GetOpcode<S2C_Pong>(null), OnPong);
                timerIds.Add(connect, timerid);
                Debug.Log($"[TCP][Client] Connected {connect.IPEndPoint}; heartbeat timer={timerid}, interval={ServerUtility.PingInterval}ms");
            };
            OnDisconnected += (connect) =>
            {
                if (timerIds.TryGetValue(connect, out long timerId))
                {
                    TimerManager.Instance.Remove(timerId);
                    timerIds.Remove(connect);
                }
            };
            
        }
        public override void Update()
        {
            HandleConnect();
            HandleRecv();
            HandleSend();
            HandleTimeout();
            HandleDisconnect();
        }
        private void HandleConnect()
        {
            foreach (var pair in connects)
            {
                Guid id = pair.Key;
                Connect connect = pair.Value.connect;
                Socket socket = pair.Value.socket;

                if(connect.State == ConnectState.Pending)
                {
                    try
                    {
                        connect.State = ConnectState.Connecting;
                        Debug.Log($"[TCP][Client] Connecting to {pair.Value.IPEndPoint}, Connect={id}");
                        OnConnecting?.Invoke(connect);
                        //开始连接
                        connectingTask.Add(id, socket.ConnectAsync(pair.Value.IPEndPoint));
                    }
                    catch (SocketException e)
                    {
                        Debug.LogError($"[TCP][Client] Connect start failed: {e.SocketErrorCode} - {e.Message}");
                        connect.State = ConnectState.Close;
                        disconnectList.Add(id);
                        OnConnectedFail?.Invoke(connect);
                        connectingTask.Remove(id);
                    }
                }
            }

            List<Guid> completeList = new List<Guid>();
            foreach(var connecting in connectingTask)
            {
                Guid id = connecting.Key;
                Connect connect = connects[id].connect;
                Task task = connecting.Value;
                if (!task.IsCompleted)
                    continue;
                try
                {
                    //成功连接
                    task.GetAwaiter().GetResult();
                    connect.State = ConnectState.Connected;
                    connect.startTime = TimerManager.Instance.TimeNow;
                    connect.LastReceiveTime = TimerManager.Instance.TimeNow;

                    Debug.Log($"[TCP][Client] Connect succeeded: {connect.IPEndPoint}, Connect={id}");
                    OnConnectedSuccess.Invoke(connect);
                    completeList.Add(id);
                }
                catch(SocketException e) 
                {
                    Debug.LogError($"[TCP][Client] Connect failed: {e.SocketErrorCode} - {e.Message}");
                    connect.State = ConnectState.Close;
                    disconnectList.Add(id);
                    OnConnectedFail?.Invoke(connect);
                    connectingTask.Remove(id);
                }
            }

            foreach(var complete in completeList)
            {
                connectingTask.Remove(complete);
            }
        }
    }
}

