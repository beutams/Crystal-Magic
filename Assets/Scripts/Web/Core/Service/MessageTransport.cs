using System;
using System.Collections.Generic;
using UnityEngine;

namespace Server
{
    /// <summary>消息型传输共用的主线程生命周期。回调只在 Update 派发，所有队列和等待都有上限。</summary>
    public abstract class MessageTransport : IClientTransport, IServerTransport
    {
        protected sealed class Peer : IConnectionTransport, IImmediateBattleFrameTransport
        {
            public readonly Connect Connection = new();
            public readonly Queue<byte[]> Outgoing = new();
            public readonly Queue<byte[]> Incoming = new();
            public NetworkEndpoint RemoteEndpoint { get; }
            public object Native;
            public bool Ready;
            public bool IncomingConnection;
            public int OutgoingBytes;
            public int IncomingBytes;
            public long NextPing;
            public long DrainDeadline;
            public Func<byte[], bool> ImmediateBattleFrameSender;
            public bool TrySendBattleFrame(byte[] message) =>
                ImmediateBattleFrameSender?.Invoke(message) == true;
            public Peer(NetworkEndpoint endpoint, bool incoming)
            {
                RemoteEndpoint = endpoint;
                IncomingConnection = incoming;
                Connection.Init(this);
                Connection.startTime = NetworkTimer.Instance.TimeNow;
            }
            public void Send(byte[] message)
            {
                if (DrainDeadline != 0 || Connection.State == ConnectState.Close)
                    return;
                if (message.Length > MessageCodec.MaxMessageLength ||
                    OutgoingBytes + message.Length > ServerUtility.MaxQueuedBytes)
                    throw new InvalidOperationException("Message send queue exceeded its limit.");
                Outgoing.Enqueue(message);
                OutgoingBytes += message.Length;
            }
        }

        protected readonly Dictionary<Connect, Peer> peers = new();
        protected bool active;
        protected virtual bool UsesHeartbeatTimeout => true;
        public NetworkEndpoint LocalEndpoint { get; protected set; }
        public event Action<Connect> OnSend;
        public event Action<Connect> OnRecv;
        public event Action<Connect> OnDisconnected;
        public event Action<Connect> OnConnecting;
        public event Action<Connect> OnConnectedFail;
        public event Action<Connect> OnAccept;
        public event Action OnListening;
        public event Action OnListeningFail;

        protected void NotifyImmediateSend(Connect connect) => Invoke(OnSend, connect);
        protected void NotifyImmediateReceive(Connect connect) => Invoke(OnRecv, connect);

        public void Init()
        {
            if (active) return;
            active = true;
            try
            {
                Open();
                if (LocalEndpoint != null) OnListening?.Invoke();
            }
            catch
            {
                Action failed = OnListeningFail;
                Shutdown();
                failed?.Invoke();
                throw;
            }
        }

        public void Connect(NetworkEndpoint endpoint, out Connect connect)
        {
            if (!active || LocalEndpoint != null)
                throw new InvalidOperationException("Client transport is not initialized.");
            connect = AddPeer(endpoint, false).Connection;
        }

        protected Peer AddPeer(NetworkEndpoint endpoint, bool incoming)
        {
            Peer peer = new(endpoint, incoming);
            peers.Add(peer.Connection, peer);
            return peer;
        }

        protected void EnqueueIncoming(Peer peer, byte[] packet)
        {
            if (peer.Connection.State == ConnectState.Close) return;
            if (packet == null || packet.Length < MessageCodec.OpcodeLength ||
                packet.Length > MessageCodec.MaxMessageLength ||
                peer.IncomingBytes + packet.Length > ServerUtility.MaxQueuedBytes)
            {
                Fail(peer, DisconnectReason.InvalidPacket, "ReceiveQueueLimit");
                return;
            }
            peer.Incoming.Enqueue(packet);
            peer.IncomingBytes += packet.Length;
        }

        public void Update()
        {
            if (!active) return;
            try { Poll(); }
            catch (Exception exception)
            {
                foreach (Peer peer in new List<Peer>(peers.Values))
                    Fail(peer, DisconnectReason.ReceiveError, "Poll", exception);
            }
            foreach (Peer peer in new List<Peer>(peers.Values))
            {
                if (!active) break;
                try { Tick(peer); }
                catch (Exception exception) { Fail(peer, DisconnectReason.HandlerError, "Update", exception); }
                if (peer.Connection.State == ConnectState.Close) Release(peer, true);
            }
        }

        private void Tick(Peer peer)
        {
            Connect connect = peer.Connection;
            long now = NetworkTimer.Instance.TimeNow;
            if (connect.State == ConnectState.Pending)
            {
                connect.State = ConnectState.Connecting;
                if (!peer.IncomingConnection)
                {
                    Invoke(OnConnecting, connect);
                    if (!active || connect.State != ConnectState.Connecting) return;
                    BeginConnect(peer);
                }
            }
            if (peer.Ready && connect.State == ConnectState.Connecting)
            {
                peer.Ready = false;
                connect.State = ConnectState.Connected;
                connect.LastReceiveTime = now;
                peer.NextPing = now + ServerUtility.PingInterval;
                if (peer.IncomingConnection)
                {
                    connect.RegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), (_, connection) =>
                        connection.Send(new S2C_Pong { Time = NetworkTimer.Instance.TimeNow }));
                    Invoke(OnAccept, connect);
                }
                else Invoke(connect.OnConnected, connect);
            }
            if (!active || connect.State == ConnectState.Close) return;
            if (connect.State != ConnectState.Connected)
            {
                if (now - connect.startTime >= ServerUtility.ConnectTimeout)
                {
                    Fail(peer, DisconnectReason.Timeout, "ConnectTimeout");
                    Invoke(OnConnectedFail, connect);
                }
                return;
            }

            for (int i = 0; i < 64 && active && connect.State == ConnectState.Connected && peer.Incoming.Count > 0; i++)
            {
                byte[] packet = peer.Incoming.Dequeue();
                peer.IncomingBytes -= packet.Length;
                if (!connect.TryReceive(packet, out DisconnectInfo failure))
                {
                    connect.LastDisconnectInfo = failure;
                    connect.State = ConnectState.Close;
                    break;
                }
                if (active && connect.State == ConnectState.Connected) Invoke(OnRecv, connect);
            }
            if (!active || connect.State != ConnectState.Connected) return;
            if (UsesHeartbeatTimeout && now - connect.LastReceiveTime >= ServerUtility.Timeout)
            {
                Fail(peer, DisconnectReason.Timeout, "HeartbeatTimeout");
                return;
            }
            if (!peer.IncomingConnection && peer.DrainDeadline == 0 && now >= peer.NextPing)
            {
                peer.NextPing = now + ServerUtility.PingInterval;
                connect.Send(new C2S_Ping { Time = now });
            }
            for (int i = 0; i < 64 && active && connect.State == ConnectState.Connected && peer.Outgoing.Count > 0; i++)
            {
                byte[] packet = peer.Outgoing.Peek();
                if (!TrySendPacket(peer, packet)) break;
                peer.Outgoing.Dequeue();
                peer.OutgoingBytes -= packet.Length;
                Invoke(OnSend, connect);
            }
            if (peer.DrainDeadline != 0 && connect.State == ConnectState.Connected)
            {
                if (peer.Outgoing.Count == 0 && IsNativeDrained(peer))
                    Fail(peer, DisconnectReason.CloseAfterSend, "SendComplete");
                else if (now >= peer.DrainDeadline)
                    Fail(peer, DisconnectReason.Timeout, "DrainTimeout");
            }
        }

        public void Disconnect(Connect connect)
        {
            if (connect != null && peers.TryGetValue(connect, out Peer peer))
                Fail(peer, DisconnectReason.LocalClose, "Disconnect");
        }

        public void DisconnectAfterSend(Connect connect)
        {
            if (connect == null || !peers.TryGetValue(connect, out Peer peer)) return;
            if (connect.State != ConnectState.Connected) { Disconnect(connect); return; }
            if (peer.DrainDeadline == 0)
                peer.DrainDeadline = NetworkTimer.Instance.TimeNow + ServerUtility.DrainTimeout;
        }

        protected void Fail(Peer peer, DisconnectReason reason, string phase, Exception exception = null)
        {
            if (peer.Connection.State == ConnectState.Close) return;
            peer.Connection.LastDisconnectInfo = new DisconnectInfo { Reason = reason, Phase = phase, Exception = exception };
            peer.Connection.State = ConnectState.Close;
        }

        private void Release(Peer peer, bool notify)
        {
            if (!peers.Remove(peer.Connection)) return;
            peer.Connection.State = ConnectState.Close;
            try { ClosePeer(peer); }
            catch (Exception exception) { Debug.LogException(exception); }
            peer.Incoming.Clear();
            peer.Outgoing.Clear();
            if (notify && active)
            {
                Invoke(OnDisconnected, peer.Connection);
                if (active) Invoke(peer.Connection.OnDisconnected, peer.Connection);
            }
            peer.Connection.Dispose();
        }

        public void Shutdown()
        {
            active = false;
            foreach (Peer peer in new List<Peer>(peers.Values)) Release(peer, false);
            try { Close(); }
            finally
            {
                OnSend = OnRecv = OnDisconnected = OnConnecting = OnConnectedFail = OnAccept = null;
                OnListening = OnListeningFail = null;
            }
        }

        private void Invoke(Action<Connect> callbacks, Connect connection)
        {
            if (callbacks == null) return;
            foreach (Action<Connect> callback in callbacks.GetInvocationList())
            {
                if (!active) break;
                try { callback(connection); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        protected abstract void Open();
        protected abstract void BeginConnect(Peer peer);
        protected abstract void Poll();
        protected abstract bool TrySendPacket(Peer peer, byte[] packet);
        protected virtual bool IsNativeDrained(Peer peer) => true;
        protected abstract void ClosePeer(Peer peer);
        protected abstract void Close();
    }
}
