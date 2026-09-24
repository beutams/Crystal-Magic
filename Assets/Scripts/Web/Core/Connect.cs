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
    public enum DisconnectReason
    {
        None,
        LocalClose,
        CloseAfterSend,
        RemoteClosed,
        Timeout,
        ReceiveError,
        SendError,
        InvalidPacket,
        UnknownOpcode,
        DeserializeError,
        HandlerError,
        ConnectFailed,
    }

    public sealed class DisconnectInfo
    {
        public DisconnectReason Reason { get; internal set; }
        public string Phase { get; internal set; }
        public string Detail { get; internal set; }
        public Exception Exception { get; internal set; }
    }

    public class Connect : IDisposable
    {
        public long startTime {  get; set; }
        public long LastReceiveTime { get; set; }
        public long RttMs { get; private set; }
        public uint LastReceivedBattleFrameSequence { get; private set; }
        public DisconnectInfo LastDisconnectInfo { get; internal set; }

        protected Dictionary<int,Action<IMessage,Connect>> callback = new Dictionary<int, Action<IMessage,Connect>>();
        private readonly Dictionary<uint, long> pendingBattleFrameSendTimes = new Dictionary<uint, long>();
        private readonly List<uint> acknowledgedBattleFrameSequences = new List<uint>();
        private uint nextBattleFrameSequence;
        private uint lastAcknowledgedBattleFrameSequence;
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
        public void OnRead(ushort opcode, IMessage message, Connect connect)
        {
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

        public uint RecordBattleFrameSend(long sendTime)
        {
            nextBattleFrameSequence++;
            if (nextBattleFrameSequence == 0)
                nextBattleFrameSequence++;

            pendingBattleFrameSendTimes[nextBattleFrameSequence] = sendTime;
            return nextBattleFrameSequence;
        }

        public void RecordBattleFrameReceive(uint sequence)
        {
            if (sequence > LastReceivedBattleFrameSequence)
                LastReceivedBattleFrameSequence = sequence;
        }

        public void AcknowledgeBattleFrame(uint sequence, long receiveTime)
        {
            if (sequence == 0 || sequence <= lastAcknowledgedBattleFrameSequence)
                return;

            lastAcknowledgedBattleFrameSequence = sequence;
            if (pendingBattleFrameSendTimes.TryGetValue(sequence, out long sendTime))
                RttMs = Math.Max(0, receiveTime - sendTime);

            acknowledgedBattleFrameSequences.Clear();
            foreach (KeyValuePair<uint, long> pending in pendingBattleFrameSendTimes)
            {
                if (pending.Key <= sequence)
                    acknowledgedBattleFrameSequences.Add(pending.Key);
            }

            foreach (uint acknowledgedSequence in acknowledgedBattleFrameSequences)
                pendingBattleFrameSendTimes.Remove(acknowledgedSequence);
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
            callback.Clear();
            pendingBattleFrameSendTimes.Clear();
            acknowledgedBattleFrameSequences.Clear();
            OnConnected = null;
            OnDisconnected = null;
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
