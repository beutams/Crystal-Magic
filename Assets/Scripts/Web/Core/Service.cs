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
        protected List<Guid> disconnectList = new List<Guid>();


        public Action<Connect> OnSend;
        public Action<Connect> OnRecv;
        public Action<Connect> OnDisconnected;
        public abstract void Init();
        public abstract void Update();
        public void Disconnect(Connect connect)
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

                connect.State = ConnectState.Close;
                if (!disconnectList.Contains(pair.Key))
                {
                    disconnectList.Add(pair.Key);
                }
                return;
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
                if (sendStream.Length == 0)
                    continue;
                if (!socket.Poll(0, SelectMode.SelectWrite))
                    continue;

                try
                {
                    int totalLength = (int)sendStream.Length;
                    Debug.Log($"[TCP][Send] Sending {totalLength} bytes to {pair.Value.IPEndPoint}, Connect={id}");
                    int sentLength = socket.Send(sendStream.GetBuffer(), 0, totalLength, SocketFlags.None);
                    OnSend?.Invoke(connect);
                    if (sentLength <= 0)
                    {
                        disconnectList.Add(id);
                        continue;
                    }

                    int remainLength = totalLength - sentLength;
                    if (remainLength > 0)
                    {
                        Buffer.BlockCopy(sendStream.GetBuffer(), sentLength, sendStream.GetBuffer(), 0, remainLength);
                    }
                    sendStream.SetLength(remainLength);
                    sendStream.Position = remainLength;
                    Debug.Log($"[TCP][Send] Sent {sentLength} bytes, remaining={remainLength}, Connect={id}");
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock) { }
                catch (SocketException e)
                {
                    Debug.LogError($"[TCP][Send] Failed for Connect={id}: {e.SocketErrorCode} - {e.Message}");
                    disconnectList.Add(id);
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
                if (!socket.Poll(0, SelectMode.SelectRead))
                    continue;
                int count;
                try
                {
                    count = socket.Receive(cache);
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock)
                {
                    continue;
                }
                //count=0 断开连接
                Debug.Log($"[TCP][Recv] Received {count} raw bytes from {pair.Value.IPEndPoint}, Connect={id}");
                if (count == 0)
                {
                    Debug.LogWarning($"[TCP][Recv] Remote closed Connect={id}, Remote={pair.Value.IPEndPoint}");
                    disconnectList.Add(id);
                    continue;
                }
                //count!=0 有新消息
                readStream.Position = readStream.Length;
                readStream.Write(cache, 0, count);
                while (TCPPacketCode.TryUnPack(readStream, out byte[] body, out ushort opcode))
                {
                    connect.LastReceiveTime = TimerManager.Instance.TimeNow;
                    Debug.Log($"[TCP][Recv] Packet opcode={opcode}, body={body.Length} bytes, Connect={id}");
                    connect.OnRead(opcode, body, connect);
                    OnRecv?.Invoke(connect);
                }
            }
        }
        protected virtual void HandleDisconnect()
        {
            foreach (var id in disconnectList)
            {
                if (connects.TryGetValue(id, out TCPPair pair))
                {
                    Debug.Log($"[TCP][Disconnect] Connect={id}, Remote={pair.IPEndPoint}, State={pair.connect.State}");
                    try
                    {
                        connects.Remove(id);
                        pair.socket.Shutdown(SocketShutdown.Both);
                    }
                    catch (SocketException) { }
                    finally
                    {
                        OnDisconnected?.Invoke(pair.connect);
                        pair.socket.Close();
                        pair.socket.Dispose();
                        pair.connect.readSteam.Dispose();
                        pair.connect.Dispose();
                    }
                }
            }
            disconnectList.Clear();
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

                if(TimerManager.Instance.TimeNow - connect.LastReceiveTime > ServerUtility.Timeout)
                {
                    Debug.LogWarning($"[TCP][Timeout] Connect={guid}, Remote={pair.Value.IPEndPoint}, LastReceive={connect.LastReceiveTime}ms");
                    connect.State = ConnectState.Close;
                    disconnectList.Add(guid);
                }
            }
        }
    }
}

