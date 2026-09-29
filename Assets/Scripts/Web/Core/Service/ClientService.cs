using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Server
{
    public class ClientService : Service, IClientTransport
    {
        public event Action<Connect> OnConnecting;
        private Action<Connect> OnConnectedSuccess;
        public event Action<Connect> OnConnectedFail;

        protected Dictionary<Guid, Task> connectingTask;
        protected Dictionary<Connect, long> timerIds;
        protected Dictionary<Guid, TCPPair> pendingConnects = new Dictionary<Guid, TCPPair>();
        private bool initialized;
        public void Connect(NetworkEndpoint endpoint, out Connect connect)
        {
            if (!initialized)
                throw new InvalidOperationException("Client transport is not initialized.");
            if (endpoint is not TcpEndpoint tcpEndpoint)
                throw new ArgumentException("ClientService requires a TCP endpoint.", nameof(endpoint));

            IPEndPoint iPEndPoint = tcpEndpoint.Address;
            Socket socket = new Socket(iPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
            socket.NoDelay = true;
            socket.Bind(new IPEndPoint(iPEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
            }
            catch { socket.Dispose(); throw; }

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
            if (initialized)
                return;
            initialized = true;
            connectingTask = new Dictionary<Guid, Task>();
            timerIds = new Dictionary<Connect, long>();
            startTime = NetworkTimer.Instance.TimeNow;
            OnConnectedSuccess += (connect) =>
            {
                long timerid = NetworkTimer.Instance.AddRepeated(ServerUtility.PingInterval, () =>
                {
                    if (connect.State == ConnectState.Connected)
                        connect.Send(new C2S_Ping() { Time = NetworkTimer.Instance.TimeNow });
                });
                timerIds.Add(connect, timerid);
                Debug.Log($"[TCP][Client] Connected {connect.RemoteEndpoint}; heartbeat timer={timerid}, interval={ServerUtility.PingInterval}ms");
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
            if (!initialized)
                return;
            HandleConnect();
            HandleRecv();
            HandleSend();
            HandleTimeout();
            HandleDisconnect();
        }
        public override void Shutdown()
        {
            initialized = false;
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
            drainDeadlines.Clear();
            OnConnecting = null;
            OnConnectedSuccess = null;
            OnConnectedFail = null;
            ClearCallbacks();
        }
        private void HandleConnect()
        {
            foreach (var pair in pendingConnects)
            {
                connects.Add(pair.Key, pair.Value);
            }
            pendingConnects.Clear();

            foreach (var pair in new List<KeyValuePair<Guid, TCPPair>>(connects))
            {
                Guid id = pair.Key;
                Connect connect = pair.Value.connect;
                Socket socket = pair.Value.socket;

                if(connect.State == ConnectState.Pending)
                {
                    try
                    {
                        connect.State = ConnectState.Connecting;
                        connect.startTime = NetworkTimer.Instance.TimeNow;
                        Debug.Log($"[TCP][Client] Connecting to {pair.Value.IPEndPoint}, Connect={id}");
                        InvokeConnectionEventSafely(OnConnecting, connect, nameof(OnConnecting));
                        if (!initialized || connect.State != ConnectState.Connecting)
                            continue;
                        //开始连接
                        Task task = socket.ConnectAsync(pair.Value.IPEndPoint);
                        // Dispose 会取消连接；仍观察延迟完成的异常，不把 Task 留给终结器。
                        _ = task.ContinueWith(failed => { _ = failed.Exception; },
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                        connectingTask.Add(id, task);
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
            foreach(var connecting in new List<KeyValuePair<Guid, Task>>(connectingTask))
            {
                Guid id = connecting.Key;
                if (!connects.TryGetValue(id, out TCPPair pair))
                    continue;
                Connect connect = pair.connect;
                Task task = connecting.Value;
                if (connect.State == ConnectState.Close)
                {
                    completeList.Add(id);
                    continue;
                }

                if (NetworkTimer.Instance.TimeNow - connect.startTime >= ServerUtility.ConnectTimeout)
                {
                    MarkDisconnected(id, DisconnectReason.Timeout, "ConnectTimeout");
                    InvokeConnectionEventSafely(OnConnectedFail, connect, nameof(OnConnectedFail));
                    completeList.Add(id);
                    continue;
                }
                if (!task.IsCompleted)
                    continue;
                try
                {
                    //成功连接
                    task.GetAwaiter().GetResult();
                    pair.socket.Blocking = false;
                    connect.State = ConnectState.Connected;
                    connect.startTime = NetworkTimer.Instance.TimeNow;
                    connect.LastReceiveTime = NetworkTimer.Instance.TimeNow;

                    Debug.Log($"[TCP][Client] Connect succeeded: {connect.RemoteEndpoint}, Connect={id}");
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
                pair.Dispose();
            }

            pairs.Clear();
        }
    }
}

