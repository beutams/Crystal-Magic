using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

namespace Server
{
    /// <summary>TCP 服务共用实现。业务代码通过 INetworkTransport 使用服务。</summary>
    public abstract class Service : INetworkTransport
    {
        protected long startTime;
        protected byte[] cache = new byte[8192];
        protected Dictionary<Guid,TCPPair> connects = new Dictionary<Guid, TCPPair>();
        protected Dictionary<Guid, DisconnectInfo> pendingDisconnects = new Dictionary<Guid, DisconnectInfo>();
        protected HashSet<Guid> closeAfterSendList = new HashSet<Guid>();
        protected readonly Dictionary<Guid, long> drainDeadlines = new();
        private uint lifecycleVersion;


        public event Action<Connect> OnSend;
        public event Action<Connect> OnRecv;
        public event Action<Connect> OnDisconnected;
        public abstract void Init();
        public abstract void Update();
        public abstract void Shutdown();

        protected void ClearCallbacks()
        {
            lifecycleVersion++;
            OnSend = null;
            OnRecv = null;
            OnDisconnected = null;
        }
        public virtual void Disconnect(Connect connect)
        {
            if (connect == null)
            {
                return;
            }

            foreach (var pair in connects)
            {
                if (pair.Value.connect != connect)
                {
                    continue;
                }

                MarkDisconnected(pair.Key, DisconnectReason.LocalClose, "Disconnect");
                return;
            }
        }
        public void DisconnectAfterSend(Connect connect)
        {
            if (connect == null)
            {
                return;
            }

            foreach (var pair in connects)
            {
                if (pair.Value.connect != connect)
                {
                    continue;
                }

                closeAfterSendList.Add(pair.Key);
                if (!drainDeadlines.ContainsKey(pair.Key))
                    drainDeadlines.Add(pair.Key, NetworkTimer.Instance.TimeNow + ServerUtility.DrainTimeout);
                return;
            }
        }

        protected void MarkDisconnected(
            Guid id,
            DisconnectReason reason,
            string phase,
            Exception exception = null,
            string detail = null)
        {
            if (!connects.TryGetValue(id, out TCPPair pair) || pendingDisconnects.ContainsKey(id))
                return;

            pair.connect.State = ConnectState.Close;
            DisconnectInfo info = new()
            {
                Reason = reason,
                Phase = phase,
                Detail = detail,
                Exception = exception,
            };
            pair.connect.LastDisconnectInfo = info;
            pendingDisconnects.Add(id, info);

            string message = $"[TCP][DisconnectPending] Connect={id}, Remote={pair.IPEndPoint}, Reason={reason}, Phase={phase}";
            if (!string.IsNullOrEmpty(detail))
                message += $", Detail={detail}";
            if (exception != null)
                message += $", Error={exception.Message}";
            Debug.LogWarning(message);
        }

        protected void InvokeConnectionEventSafely(
            Action<Connect> handlers,
            Connect connect,
            string eventName)
        {
            if (handlers == null)
                return;

            uint version = lifecycleVersion;
            foreach (Action<Connect> handler in handlers.GetInvocationList())
            {
                if (version != lifecycleVersion) break;
                try
                {
                    handler(connect);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[TCP][Callback] {eventName} failed for {connect?.RemoteEndpoint}: {exception}");
                }
            }
        }
        protected virtual void HandleSend()
        {
            foreach (var pair in new List<KeyValuePair<Guid, TCPPair>>(connects))
            {
                Guid id = pair.Key;
                Socket socket = pair.Value.socket;
                Connect connect = pair.Value.connect;
                MemoryStream sendStream = pair.Value.SendStream;

                if (connect.State != ConnectState.Connected)
                    continue;
                try
                {
                    if (sendStream.Length == 0)
                    {
                        if (closeAfterSendList.Contains(id))
                            MarkDisconnected(id, DisconnectReason.CloseAfterSend, "SendComplete");
                        continue;
                    }

                    if (!socket.Poll(0, SelectMode.SelectWrite))
                        continue;

                    int totalLength = (int)sendStream.Length;
#if UNITY_EDITOR && NETWORK_TRACE
                    Debug.Log($"[TCP][Send] Sending {totalLength} bytes to {pair.Value.IPEndPoint}, Connect={id}");
#endif
                    int sentLength = socket.Send(sendStream.GetBuffer(), 0, totalLength, SocketFlags.None);
                    if (sentLength <= 0)
                    {
                        MarkDisconnected(id, DisconnectReason.SendError, "Send", detail: "Socket.Send returned zero bytes.");
                        continue;
                    }

                    int remainLength = totalLength - sentLength;
                    if (remainLength > 0)
                    {
                        Buffer.BlockCopy(sendStream.GetBuffer(), sentLength, sendStream.GetBuffer(), 0, remainLength);
                    }
                    sendStream.SetLength(remainLength);
                    sendStream.Position = remainLength;
                    // 回调可以排入新消息，必须先移走已发送的数据，避免覆盖回调新入队的字节。
                    InvokeConnectionEventSafely(OnSend, connect, nameof(OnSend));
                    if (connect.State != ConnectState.Connected)
                        continue;
#if UNITY_EDITOR && NETWORK_TRACE
                    Debug.Log($"[TCP][Send] Sent {sentLength} bytes, remaining={remainLength}, Connect={id}");
#endif
                    if (sendStream.Length == 0 && closeAfterSendList.Contains(id))
                        MarkDisconnected(id, DisconnectReason.CloseAfterSend, "SendComplete");
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock) { }
                catch (SocketException e)
                {
                    MarkDisconnected(id, DisconnectReason.SendError, "Send", e, e.SocketErrorCode.ToString());
                }
                catch (ObjectDisposedException e)
                {
                    MarkDisconnected(id, DisconnectReason.SendError, "Send", e);
                }
                catch (Exception e)
                {
                    MarkDisconnected(id, DisconnectReason.SendError, "Send", e);
                }
            }
        }
        protected virtual void HandleRecv()
        {
            foreach (var pair in new List<KeyValuePair<Guid, TCPPair>>(connects))
            {
                Guid id = pair.Key;
                Connect connect = pair.Value.connect;
                Socket socket = pair.Value.socket;
                MemoryStream readStream = pair.Value.ReadStream;

                if (connect.State != ConnectState.Connected)
                    continue;

                try
                {
                    if (!socket.Poll(0, SelectMode.SelectRead))
                        continue;

                    int count = socket.Receive(cache);
#if UNITY_EDITOR && NETWORK_TRACE
                    Debug.Log($"[TCP][Recv] Received {count} raw bytes from {pair.Value.IPEndPoint}, Connect={id}");
#endif
                    if (count == 0)
                    {
                        MarkDisconnected(id, DisconnectReason.RemoteClosed, "Receive");
                        continue;
                    }

                    readStream.Position = readStream.Length;
                    readStream.Write(cache, 0, count);
                    while (connect.State == ConnectState.Connected)
                    {
                        PacketReadResult packetResult = TCPPacketCode.TryUnPack(
                            readStream,
                            out byte[] packet,
                            out string packetError);
                        if (packetResult == PacketReadResult.NeedMoreData)
                            break;
                        if (packetResult == PacketReadResult.Invalid)
                        {
                            MarkDisconnected(id, DisconnectReason.InvalidPacket, "Unpack", detail: packetError);
                            break;
                        }

                        if (!connect.TryReceive(packet, out DisconnectInfo failure))
                        {
                            MarkDisconnected(
                                id,
                                failure.Reason,
                                failure.Phase,
                                failure.Exception,
                                failure.Detail);
                            break;
                        }

#if UNITY_EDITOR && NETWORK_TRACE
                        Debug.Log($"[TCP][Recv] Message={packet.Length} bytes, Connect={id}");
#endif
                        InvokeConnectionEventSafely(OnRecv, connect, nameof(OnRecv));
                    }
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock)
                {
                }
                catch (SocketException e)
                {
                    MarkDisconnected(id, DisconnectReason.ReceiveError, "Receive", e, e.SocketErrorCode.ToString());
                }
                catch (ObjectDisposedException e)
                {
                    MarkDisconnected(id, DisconnectReason.ReceiveError, "Receive", e);
                }
                catch (Exception e)
                {
                    MarkDisconnected(id, DisconnectReason.ReceiveError, "Receive", e);
                }
            }
        }
        protected virtual void HandleDisconnect()
        {
            if (pendingDisconnects.Count == 0)
                return;

            List<Guid> pendingIds = new List<Guid>(pendingDisconnects.Keys);
            foreach (Guid id in pendingIds)
            {
                pendingDisconnects.Remove(id);
                if (!connects.TryGetValue(id, out TCPPair pair))
                    continue;

                connects.Remove(id);
                closeAfterSendList.Remove(id);
                drainDeadlines.Remove(id);
                pair.connect.State = ConnectState.Close;
                Debug.Log(
                    $"[TCP][Disconnect] Connect={id}, Remote={pair.IPEndPoint}, " +
                    $"Reason={pair.connect.LastDisconnectInfo?.Reason}, Phase={pair.connect.LastDisconnectInfo?.Phase}");

                try
                {
                    pair.socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                catch (Exception exception)
                {
                    Debug.LogError($"[TCP][Disconnect] Socket shutdown failed: {exception}");
                }

                try { pair.socket.Close(); }
                catch (Exception exception) { Debug.LogError($"[TCP][Disconnect] Socket close failed: {exception}"); }
                try { pair.socket.Dispose(); }
                catch (Exception exception) { Debug.LogError($"[TCP][Disconnect] Socket dispose failed: {exception}"); }
                try { pair.ReadStream.Dispose(); }
                catch (Exception exception) { Debug.LogError($"[TCP][Disconnect] Read stream dispose failed: {exception}"); }
                try { pair.SendStream.Dispose(); }
                catch (Exception exception) { Debug.LogError($"[TCP][Disconnect] Send stream dispose failed: {exception}"); }

                uint version = lifecycleVersion;
                InvokeConnectionEventSafely(OnDisconnected, pair.connect, nameof(OnDisconnected));
                if (version == lifecycleVersion)
                    InvokeConnectionEventSafely(pair.connect.OnDisconnected, pair.connect, nameof(Connect.OnDisconnected));
                pair.connect.Dispose();
            }
        }
        protected virtual void HandleTimeout()
        {
            foreach(var pair  in connects)
            {
                Guid guid = pair.Key;
                Connect connect = pair.Value.connect;
                if (connect.State == ConnectState.Close && !pendingDisconnects.ContainsKey(guid))
                {
                    DisconnectInfo failure = connect.LastDisconnectInfo;
                    MarkDisconnected(guid, failure?.Reason ?? DisconnectReason.LocalClose,
                        failure?.Phase ?? "Closed", failure?.Exception, failure?.Detail);
                }
                if (drainDeadlines.TryGetValue(guid, out long deadline) && NetworkTimer.Instance.TimeNow >= deadline)
                {
                    MarkDisconnected(guid, DisconnectReason.Timeout, "DrainTimeout");
                }
                if(connect.State != ConnectState.Connected)
                {
                    continue;
                }

                if(NetworkTimer.Instance.TimeNow - connect.LastReceiveTime > ServerUtility.Timeout)
                {
                    MarkDisconnected(
                        guid,
                        DisconnectReason.Timeout,
                        "Timeout",
                        detail: $"LastReceive={connect.LastReceiveTime}ms");
                }
            }
        }
    }
}

