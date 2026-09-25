using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Server
{
    public abstract class Service
    {
        protected long startTime;
        protected byte[] cache = new byte[8192];
        protected Dictionary<Guid,TCPPair> connects = new Dictionary<Guid, TCPPair>();
        protected Dictionary<Guid, DisconnectInfo> pendingDisconnects = new Dictionary<Guid, DisconnectInfo>();
        protected HashSet<Guid> closeAfterSendList = new HashSet<Guid>();


        public Action<Connect> OnSend;
        public Action<Connect> OnRecv;
        public Action<Connect> OnDisconnected;
        public abstract void Init();
        public abstract void Update();
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

        protected static void InvokeConnectionEventSafely(
            Action<Connect> handlers,
            Connect connect,
            string eventName)
        {
            if (handlers == null)
                return;

            foreach (Action<Connect> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(connect);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[TCP][Callback] {eventName} failed for {connect?.IPEndPoint}: {exception}");
                }
            }
        }
        protected virtual void HandleSend()
        {
            foreach (var pair in connects)
            {
                Guid id = pair.Key;
                Socket socket = pair.Value.socket;
                Connect connect = pair.Value.connect;
                MemoryStream sendStream = connect.sendSteam;

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
                    Debug.Log($"[TCP][Send] Sending {totalLength} bytes to {pair.Value.IPEndPoint}, Connect={id}");
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
                    Debug.Log($"[TCP][Send] Sent {sentLength} bytes, remaining={remainLength}, Connect={id}");
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
            foreach (var pair in connects)
            {
                Guid id = pair.Key;
                Connect connect = pair.Value.connect;
                Socket socket = pair.Value.socket;
                MemoryStream readStream = connect.readSteam;

                if (connect.State != ConnectState.Connected)
                    continue;

                try
                {
                    if (!socket.Poll(0, SelectMode.SelectRead))
                        continue;

                    int count = socket.Receive(cache);
                    Debug.Log($"[TCP][Recv] Received {count} raw bytes from {pair.Value.IPEndPoint}, Connect={id}");
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
                            out byte[] body,
                            out ushort opcode,
                            out string packetError);
                        if (packetResult == PacketReadResult.NeedMoreData)
                            break;
                        if (packetResult == PacketReadResult.Invalid)
                        {
                            MarkDisconnected(id, DisconnectReason.InvalidPacket, "Unpack", detail: packetError);
                            break;
                        }

                        MessageDecodeResult decodeResult = TCPPacketCode.TryToMessage(
                            body,
                            opcode,
                            out IMessage message,
                            out Exception decodeException);
                        if (decodeResult == MessageDecodeResult.UnknownOpcode)
                        {
                            MarkDisconnected(
                                id,
                                DisconnectReason.UnknownOpcode,
                                "Opcode",
                                detail: opcode.ToString());
                            break;
                        }
                        if (decodeResult == MessageDecodeResult.InvalidPayload)
                        {
                            MarkDisconnected(
                                id,
                                DisconnectReason.DeserializeError,
                                "Deserialize",
                                decodeException,
                                $"opcode={opcode}");
                            break;
                        }

                        connect.LastReceiveTime = NetworkTimer.Instance.TimeNow;
                        Debug.Log($"[TCP][Recv] Packet opcode={opcode}, body={body.Length} bytes, Connect={id}");
                        try
                        {
                            connect.OnRead(opcode, message, connect);
                            InvokeConnectionEventSafely(OnRecv, connect, nameof(OnRecv));
                        }
                        catch (Exception handlerException)
                        {
                            MarkDisconnected(
                                id,
                                DisconnectReason.HandlerError,
                                "Dispatch",
                                handlerException,
                                $"opcode={opcode}");
                            break;
                        }
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
                try { pair.connect.readSteam.Dispose(); }
                catch (Exception exception) { Debug.LogError($"[TCP][Disconnect] Read stream dispose failed: {exception}"); }
                try { pair.connect.sendSteam.Dispose(); }
                catch (Exception exception) { Debug.LogError($"[TCP][Disconnect] Send stream dispose failed: {exception}"); }

                InvokeConnectionEventSafely(OnDisconnected, pair.connect, nameof(OnDisconnected));
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

