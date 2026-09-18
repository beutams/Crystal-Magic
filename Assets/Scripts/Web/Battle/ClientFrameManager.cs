using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ClientFrameManager : FrameManager
    {
        private const int PredictionHistoryCapacity = 256;

        public delegate bool PlayerFrameHandler(
            uint frame,
            IReadOnlyList<NetworkStateData> states,
            NetworkStateApplyContext context);

        public Connect connect;
        public readonly SortedDictionary<uint, Queue<NetworkStateData>> playerStates = new();
        public readonly SortedDictionary<uint, Queue<NetworkStateData>> inputOrder = new();
        public uint latestServerFrame;
        public bool hasLatestServerFrame;
        public uint lastPredictedFrame;
        public bool hasLastPredictedFrame;
        public PlayerFrameHandler onHandlePlayerFrame;

        public void RecordInput(uint frame, NetworkStateData input)
        {
            if (input == null)
                return;

            if (!inputOrder.TryGetValue(frame, out Queue<NetworkStateData> states))
            {
                states = new Queue<NetworkStateData>();
                inputOrder.Add(frame, states);
            }

            states.Enqueue(input);
            TrimHistory(inputOrder);
        }

        public void RecordPlayerStates(uint frame, Queue<NetworkStateData> states)
        {
            if (states == null)
                return;

            playerStates[frame] = new Queue<NetworkStateData>(states);
            lastPredictedFrame = frame;
            hasLastPredictedFrame = true;
            TrimHistory(playerStates);
        }

        public void RecordServerFrame(uint frame)
        {
            if (!hasLatestServerFrame || frame > latestServerFrame)
            {
                latestServerFrame = frame;
                hasLatestServerFrame = true;
            }
        }

        public bool TryHandlePlayerFrame(
            uint frame,
            IReadOnlyList<NetworkStateData> states,
            NetworkStateApplyContext context)
        {
            return states != null &&
                   states.Count > 0 &&
                   onHandlePlayerFrame?.Invoke(frame, states, context) == true;
        }

        public void RemovePredictionHistoryThrough(uint frame)
        {
            RemoveHistoryThrough(inputOrder, frame);
            RemoveHistoryThrough(playerStates, frame);
        }

        public override void AddConnect(Connect connect)
        {
            if (this.connect == connect)
            {
                return;
            }

            RemoveConnect(this.connect);
            this.connect = connect;
            if (this.connect != null)
            {
                this.connect.RegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
            }
        }

        public override void RemoveConnect(Connect connect)
        {
            if (connect == null || this.connect != connect)
            {
                return;
            }

            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
            this.connect = null;
        }

        public override void HandleReceive()
        {
            while (receivedOrder.Count > 0)
            {
                var frame = receivedOrder.First();
                if (frame.Key > currentFrame)
                {
                    return;
                }

                receivedOrder.Remove(frame.Key);
                onHandleReceive?.Invoke(frame.Key, frame.Value);
            }
        }

        protected override void SendFrame(NetworkFrameData data)
        {
            if (connect != null)
            {
                uint clientFrameSequence = connect.RecordBattleFrameSend(NetworkTimer.Instance.TimeNow);
                connect.Send(new General_FrameStateData
                {
                    data = data,
                    clientFrameSequence = clientFrameSequence,
                });
            }
        }

        public override void OnReceiveMessage(IMessage message, Connect receivedConnect)
        {
            if (receivedConnect == connect &&
                message is General_FrameStateData frameMessage &&
                frameMessage.data != null)
            {
                receivedConnect.AcknowledgeBattleFrame(
                    frameMessage.acknowledgedClientFrameSequence,
                    NetworkTimer.Instance.TimeNow);
            }

            base.OnReceiveMessage(message, receivedConnect);
        }

        public override void Stop()
        {
            RemoveConnect(connect);
            base.Stop();
        }

        public override void ClearOrders()
        {
            base.ClearOrders();
            playerStates.Clear();
            inputOrder.Clear();
            latestServerFrame = 0;
            hasLatestServerFrame = false;
            lastPredictedFrame = 0;
            hasLastPredictedFrame = false;
        }

        private static void TrimHistory(SortedDictionary<uint, Queue<NetworkStateData>> history)
        {
            while (history.Count > PredictionHistoryCapacity)
                history.Remove(history.First().Key);
        }

        private static void RemoveHistoryThrough(
            SortedDictionary<uint, Queue<NetworkStateData>> history,
            uint frame)
        {
            while (history.Count > 0 && history.First().Key <= frame)
                history.Remove(history.First().Key);
        }

    }
}
