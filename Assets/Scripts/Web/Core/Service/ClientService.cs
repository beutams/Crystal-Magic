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
        private Action<Connect> OnConnectedSuccess;
        public Action<Connect> OnConnectedFail;

        protected Dictionary<Guid, Task> connectingTask;
        protected Dictionary<Connect, long> timerIds;
        protected Dictionary<Guid, TCPPair> pendingConnects = new Dictionary<Guid, TCPPair>();
        public void Connect(IPEndPoint iPEndPoint,out Connect connect)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            TCPPair pair = TCPPair.CreateTCPPair(socket, iPEndPoint, out Guid id);
            connect = pair.connect;
            pendingConnects.Add(id, pair);
            Debug.Log($"[TCP][Client] Created Connect={id}, Target={iPEndPoint}");
        }
        public override void Disconnect(Connect connect)
        {
            if (connect == null)
            {
                return;
            }

            Guid pendingId = Guid.Empty;
            foreach (var pair in pendingConnects)
            {
                if (pair.Value.connect == connect)
                {
                    pendingId = pair.Key;
                    break;
                }
            }

            if (pendingId == Guid.Empty)
            {
                base.Disconnect(connect);
                return;
            }

            TCPPair pendingPair = pendingConnects[pendingId];
            pendingConnects.Remove(pendingId);
            connects.Add(pendingId, pendingPair);
            MarkDisconnected(pendingId, DisconnectReason.LocalClose, "PendingDisconnect");
        }
        public override void Init()
        {
            connectingTask = new Dictionary<Guid, Task>();
            timerIds = new Dictionary<Connect, long>();
            startTime = NetworkTimer.Instance.TimeNow;
            OnConnectedSuccess += (connect) =>
            {
                long timerid = NetworkTimer.Instance.AddRepeated(ServerUtility.PingInterval, () =>
                {
                    Debug.Log($"[TCP][Client] Heartbeat triggered, Connect={connect.IPEndPoint}");
                    connect.Send(new C2S_Ping() { Time = NetworkTimer.Instance.TimeNow });
                });
                //connect.RegisterCallback(TCPPacketCode.GetOpcode<S2C_Pong>(null), OnPong);
                timerIds.Add(connect, timerid);
                Debug.Log($"[TCP][Client] Connected {connect.IPEndPoint}; heartbeat timer={timerid}, interval={ServerUtility.PingInterval}ms");
            };
            OnDisconnected += (connect) =>
            {
                if (timerIds.TryGetValue(connect, out long timerId))
                {
                    NetworkTimer.Instance.Remove(timerId);
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
        public void Shutdown()
        {
            if (timerIds != null)
            {
                foreach (long timerId in timerIds.Values)
                {
                    NetworkTimer.Instance.Remove(timerId);
                }

                timerIds.Clear();
            }

            ClosePairs(connects);
            ClosePairs(pendingConnects);
            connectingTask?.Clear();
            pendingDisconnects.Clear();
            closeAfterSendList.Clear();
            OnConnecting = null;
            OnConnectedSuccess = null;
            OnConnectedFail = null;
            OnSend = null;
            OnRecv = null;
            OnDisconnected = null;
        }
        private void HandleConnect()
        {
            foreach (var pair in pendingConnects)
            {
                connects.Add(pair.Key, pair.Value);
            }
            pendingConnects.Clear();

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
                        MarkDisconnected(id, DisconnectReason.ConnectFailed, "ConnectStart", e, e.SocketErrorCode.ToString());
                        InvokeConnectionEventSafely(OnConnectedFail, connect, nameof(OnConnectedFail));
                        connectingTask.Remove(id);
                    }
                    catch (Exception e)
                    {
                        MarkDisconnected(id, DisconnectReason.ConnectFailed, "ConnectStart", e);
                        InvokeConnectionEventSafely(OnConnectedFail, connect, nameof(OnConnectedFail));
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
                if (connect.State == ConnectState.Close)
                {
                    completeList.Add(id);
                    continue;
                }

                if (!task.IsCompleted)
                    continue;
                try
                {
                    //成功连接
                    task.GetAwaiter().GetResult();
                    connect.State = ConnectState.Connected;
                    connect.startTime = NetworkTimer.Instance.TimeNow;
                    connect.LastReceiveTime = NetworkTimer.Instance.TimeNow;

                    Debug.Log($"[TCP][Client] Connect succeeded: {connect.IPEndPoint}, Connect={id}");
                    InvokeConnectionEventSafely(OnConnectedSuccess, connect, nameof(OnConnectedSuccess));
                    InvokeConnectionEventSafely(connect.OnConnected, connect, "Connect.OnConnected");
                    completeList.Add(id);
                }
                catch(SocketException e) 
                {
                    MarkDisconnected(id, DisconnectReason.ConnectFailed, "ConnectComplete", e, e.SocketErrorCode.ToString());
                    InvokeConnectionEventSafely(OnConnectedFail, connect, nameof(OnConnectedFail));
                    completeList.Add(id);
                }
                catch(Exception e)
                {
                    MarkDisconnected(id, DisconnectReason.ConnectFailed, "ConnectComplete", e);
                    InvokeConnectionEventSafely(OnConnectedFail, connect, nameof(OnConnectedFail));
                    completeList.Add(id);
                }
            }

            foreach(var complete in completeList)
            {
                connectingTask.Remove(complete);
            }
        }
        private void ClosePairs(Dictionary<Guid, TCPPair> pairs)
        {
            foreach (TCPPair pair in pairs.Values)
            {
                try
                {
                    pair.socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                }
                finally
                {
                    pair.socket.Close();
                    pair.socket.Dispose();
                    pair.connect.readSteam.Dispose();
                    pair.connect.sendSteam.Dispose();
                    pair.connect.Dispose();
                }
            }

            pairs.Clear();
        }
    }
}

