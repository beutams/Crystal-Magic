using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Server
{
    public class Connect : IDisposable
    {
        public long startTime {  get; set; }
        public long LastReceiveTime { get; set; }
        protected Dictionary<int,Action<IMessage,Connect>> callback = new Dictionary<int, Action<IMessage,Connect>>();
        public ConnectState State;
        public IPEndPoint IPEndPoint;
        public Action<Connect> OnConnected;
        public Action<Connect> OnDisconnected;

        public MemoryStream readSteam;
        public MemoryStream sendSteam;
        public void Init(IPEndPoint IPEndPoint)
        {
            State = ConnectState.Pending;
            this.IPEndPoint = IPEndPoint;
            readSteam = new MemoryStream();
            sendSteam = new MemoryStream();
        }
        public void OnRead(ushort opcode, byte[] body, Connect connect)
        {
            IMessage message = TCPPacketCode.ToMessage(body, opcode);
            if(message == null)
            {
                Debug.LogError($"Message Translate Fail opcode={opcode} body={body}");
                return;
            }
            if (callback.TryGetValue(opcode, out Action<IMessage,Connect> action))
            {
                action?.Invoke(message, connect);
            }
        }
        public void Send(IMessage message)
        {
            ushort opcode = TCPPacketCode.GetOpcode(message);
            byte[] data = TCPPacketCode.Pack(opcode, TCPPacketCode.ToJson(message));
            sendSteam.Position = sendSteam.Length;
            sendSteam.Write(data, 0, data.Length);
            Debug.Log($"[TCP][Queue] opcode={opcode}, {data.Length} bytes queued for {IPEndPoint}");
        }
        public void RegisterCallback(int opcode, Action<IMessage,Connect> callback)
        {
            if(!this.callback.TryGetValue(opcode,out Action<IMessage,Connect> item))
            {
                this.callback.Add(opcode, callback);
            }
            else
            {
                this.callback[opcode] += callback;
            }
        }
        public void UnRegisterCallback(int opcode, Action<IMessage, Connect> callback)
        {
            if (this.callback.TryGetValue(opcode, out Action<IMessage, Connect> item))
            {
                this.callback[opcode] -= callback;
            }
        }
        public void Dispose()
        {
            
        }
    }
    public enum ConnectState
    {
        Pending,
        Connecting,
        Connected,
        Close,
    }
}
