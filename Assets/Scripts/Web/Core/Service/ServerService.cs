using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Server
{
    public class ServerService : Service, IServerTransport
    {
        protected Socket socket;
        protected IPEndPoint iPEndPoint;

        public event Action OnListening;
        public event Action OnListeningFail;
        public event Action<Connect> OnAccept;
        public NetworkEndpoint LocalEndpoint { get; private set; }

        public ServerService(NetworkEndpoint endpoint)
        {
            if (endpoint is not TcpEndpoint tcpEndpoint)
                throw new ArgumentException("ServerService requires a TCP endpoint.", nameof(endpoint));

            iPEndPoint = tcpEndpoint.Address;
            LocalEndpoint = endpoint;
        }

        protected ServerState state;
        public override void Init()
        {
            if (state == ServerState.LISTENING)
                return;
            try
            {
                startTime = NetworkTimer.Instance.TimeNow;

                state = ServerState.CLOSED;
                socket = new Socket(iPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;
                socket.Bind(iPEndPoint);

                socket.Listen(128);
                socket.Blocking = false;
                LocalEndpoint = new TcpEndpoint((IPEndPoint)socket.LocalEndPoint);
                state = ServerState.LISTENING;
                Debug.Log($"[TCP][Server] Listening at {socket.LocalEndPoint}");

                OnDisconnected += OnDisconnectEvent;
                OnListening?.Invoke();
            }
            catch (SocketException e)
            {
                Debug.LogError($"[TCP][Server] Listen failed at {iPEndPoint}: {e.SocketErrorCode} - {e.Message}");
                state = ServerState.CLOSED;
                socket?.Dispose();
                socket = null;
                OnListeningFail?.Invoke();
            }
        }
        private void OnAcceptEvent(Connect connect)
        {
            connect.RegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), OnClientConnect);
        }
        private void OnDisconnectEvent(Connect connect)
        {
            connect.UnRegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), OnClientConnect);
        }
        private void OnClientConnect(IMessage message, Connect connect)
        {
            connect.Send(new S2C_Pong() { Time = NetworkTimer.Instance.TimeNow });
        }
        public override void Update()
        {
            HandleAccept();
            HandleRecv();
            HandleSend();
            HandleTimeout();
            HandleDisconnect();
        }

        public override void Shutdown()
        {
            foreach (TCPPair pair in connects.Values)
            {
                pair.Dispose();
            }

            connects.Clear();
            pendingDisconnects.Clear();
            closeAfterSendList.Clear();
            drainDeadlines.Clear();

            socket?.Dispose();
            socket = null;
            OnListening = null;
            OnListeningFail = null;
            OnAccept = null;
            ClearCallbacks();
            state = ServerState.CLOSED;
        }
        protected void HandleAccept()
        {
            if (state != ServerState.LISTENING || socket == null)
            {
                return;
            }

            try
            {
                if (!socket.Poll(0, SelectMode.SelectRead))
                    return;

                Socket clientSocket = socket.Accept();
                clientSocket.NoDelay = true;
                clientSocket.Blocking = false;

                TCPPair pair = TCPPair.CreateTCPPair(clientSocket, (IPEndPoint)clientSocket.RemoteEndPoint, out Guid id);
                connects.Add(id, pair);

                Connect connect = pair.connect;
                connect.State = ConnectState.Connected;
                connect.startTime = NetworkTimer.Instance.TimeNow;
                connect.LastReceiveTime = NetworkTimer.Instance.TimeNow;
                Debug.Log($"[TCP][Server] Accepted {clientSocket.RemoteEndPoint}, Connect={id}");
                try
                {
                    OnAcceptEvent(connect);
                    OnAccept?.Invoke(connect);
                }
                catch (Exception exception)
                {
                    MarkDisconnected(id, DisconnectReason.HandlerError, "AcceptCallback", exception);
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock) { }
            catch (SocketException e)
            {
                Debug.LogError($"Accept 失败: {e}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TCP][Server] Accept failed: {e}");
            }
        }
    }
    public enum ServerState
    {
        CLOSED,
        LISTENING,
    }
}

