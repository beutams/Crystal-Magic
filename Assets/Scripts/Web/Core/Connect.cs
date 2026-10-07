using System;
using System.Collections.Generic;

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
        private readonly Queue<uint> battleFrameSendWindow = new();
        private readonly List<uint> acknowledgedBattleFrameSequences = new List<uint>();
        private uint nextBattleFrameSequence;
        private uint lastAcknowledgedBattleFrameSequence;
        private IConnectionTransport transport;
        public ConnectState State;
        public NetworkEndpoint RemoteEndpoint { get; private set; }
        public Action<Connect> OnConnected;
        public Action<Connect> OnDisconnected;

        public void Init(IConnectionTransport transport)
        {
            if (this.transport != null)
                throw new InvalidOperationException("Connection is already initialized.");
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            RemoteEndpoint = transport.RemoteEndpoint;
            State = ConnectState.Pending;
        }

        /// <summary>传输端交入一条完整消息；失败由所属服务负责断开连接，原因在此统一生成。</summary>
        public bool TryReceive(byte[] packet, out DisconnectInfo failure)
        {
            failure = null;
            MessageDecodeResult result = MessageCodec.TryDecode(packet, out ushort opcode, out IMessage message, out Exception exception);
            if (result != MessageDecodeResult.Success)
            {
                failure = new DisconnectInfo
                {
                    Reason = result == MessageDecodeResult.UnknownOpcode ? DisconnectReason.UnknownOpcode : DisconnectReason.DeserializeError,
                    Phase = result == MessageDecodeResult.UnknownOpcode ? "Opcode" : "Deserialize",
                    Detail = $"opcode={opcode}",
                    Exception = exception,
                };
                return false;
            }

            LastReceiveTime = NetworkTimer.Instance.TimeNow;
            try
            {
                if (callback.TryGetValue(opcode, out Action<IMessage, Connect> action) && action != null)
                    foreach (Action<IMessage, Connect> handler in action.GetInvocationList())
                    {
                        if (State == ConnectState.Close) break;
                        handler(message, this);
                    }
                return true;
            }
            catch (Exception handlerException)
            {
                failure = new DisconnectInfo
                {
                    Reason = DisconnectReason.HandlerError,
                    Phase = "Dispatch",
                    Detail = $"opcode={opcode}",
                    Exception = handlerException,
                };
                return false;
            }
        }

        public void Send(IMessage message)
        {
            TrySend(message);
        }

        // 断线和帧发送可以发生在同一帧；失效连接不应再抛异常打断退出流程。
        public bool TrySend(IMessage message)
        {
            if (State == ConnectState.Close || transport == null)
                return false;
            try
            {
                byte[] packet = MessageCodec.Encode(message);
                if (message is General_FrameStateData &&
                    transport is IImmediateBattleFrameTransport immediate &&
                    immediate.TrySendBattleFrame(packet))
                    return State != ConnectState.Close;
                transport.Send(packet);
                return State != ConnectState.Close;
            }
            catch (Exception exception)
            {
                LastDisconnectInfo = new DisconnectInfo
                {
                    Reason = DisconnectReason.SendError, Phase = "Enqueue", Exception = exception,
                };
                State = ConnectState.Close;
                return false;
            }
        }

        public uint RecordBattleFrameSend(long sendTime)
        {
            nextBattleFrameSequence++;
            if (nextBattleFrameSequence == 0)
                nextBattleFrameSequence++;

            pendingBattleFrameSendTimes[nextBattleFrameSequence] = sendTime;
            battleFrameSendWindow.Enqueue(nextBattleFrameSequence);
            while (battleFrameSendWindow.Count > 1024)
                pendingBattleFrameSendTimes.Remove(battleFrameSendWindow.Dequeue());
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
            State = ConnectState.Close;
            transport = null;
            callback.Clear();
            pendingBattleFrameSendTimes.Clear();
            battleFrameSendWindow.Clear();
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
