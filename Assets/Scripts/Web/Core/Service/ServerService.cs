using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Server
{
    public class ServerService : Service
    {
        protected Socket socket;
        protected IPEndPoint iPEndPoint;

        public Action OnListening;
        public Action OnListeningFail;
        public Action<Connect> OnAccept;

        public ServerService(IPEndPoint iPEndPoint)
        {
            this.iPEndPoint = iPEndPoint;
        }

        protected ServerState state;
        public override void Init()
        {
            try
            {
                startTime = NetworkTimer.Instance.TimeNow;

                state = ServerState.CLOSED;
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;
                socket.Bind(iPEndPoint);

                socket.Listen(128);
                OnListening?.Invoke();
                state = ServerState.LISTENING;
                Debug.Log($"[TCP][Server] Listening at {socket.LocalEndPoint}");

                OnAccept += OnAcceptEvent;
                OnDisconnected += OnDisconnectEvent;
            }
            catch (SocketException e)
            {
                Debug.LogError($"[TCP][Server] Listen failed at {iPEndPoint}: {e.SocketErrorCode} - {e.Message}");
                OnListeningFail?.Invoke();
                state = ServerState.CLOSED;
            }
        }
        private void OnAcceptEvent(Connect connect)
        {
            connect.RegisterCallback(TCPPacketCode.messages[typeof(C2S_Ping)], OnClientConnect);
        }
        private void OnDisconnectEvent(Connect connect)
        {
            connect.UnRegisterCallback(TCPPacketCode.messages[typeof(C2S_Ping)], OnClientConnect);
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

        public void Shutdown()
        {
            foreach (TCPPair pair in connects.Values)
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

            connects.Clear();
            pendingDisconnects.Clear();
            closeAfterSendList.Clear();

            if (socket == null)
            {
                return;
            }

            socket.Close();
            socket.Dispose();
            socket = null;
            OnListening = null;
            OnListeningFail = null;
            OnAccept = null;
            OnSend = null;
            OnRecv = null;
            OnDisconnected = null;
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

                TCPPair pair = TCPPair.CreateTCPPair(clientSocket, (IPEndPoint)clientSocket.RemoteEndPoint, out Guid id);
                connects.Add(id, pair);

                Connect connect = pair.connect;
                connect.State = ConnectState.Connected;
                connect.startTime = NetworkTimer.Instance.TimeNow;
                connect.LastReceiveTime = NetworkTimer.Instance.TimeNow;
                Debug.Log($"[TCP][Server] Accepted {clientSocket.RemoteEndPoint}, Connect={id}");
                try
                {
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

