using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ServerFrameManager : FrameManager
    {
        public List<Connect> connects = new List<Connect>();
        public List<Connect> syncingConnects = new List<Connect>();
        public SortedDictionary<uint, Queue<NetworkStateData>> allcmds = new SortedDictionary<uint, Queue<NetworkStateData>>();

        public override void AddConnect(Connect connect)
        {
            if (connect == null || connects.Contains(connect))
            {
                return;
            }

            connects.Add(connect);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
        }

        public void AddSyncingConnect(Connect connect)
        {
            if (connect == null || connects.Contains(connect) || syncingConnects.Contains(connect))
            {
                return;
            }

            syncingConnects.Add(connect);
        }

        public void PromoteSyncingConnect(Connect connect)
        {
            if (connect == null)
            {
                return;
            }

            syncingConnects.Remove(connect);
            AddConnect(connect);
        }

        public override void RemoveConnect(Connect connect)
        {
            if (connect == null)
            {
                return;
            }

            syncingConnects.Remove(connect);
            if (connects.Remove(connect))
            {
                connect.UnRegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
            }
        }

        public override void HandleReceive()
        {
            while (receivedOrder.Count > 0 && receivedOrder.First().Key <= currentFrame)
            {
                receivedOrder.Remove(receivedOrder.First().Key);
            }

            uint frame = currentFrame + 1;
            if (!receivedOrder.TryGetValue(frame, out Queue<NetworkState> states))
            {
                return;
            }

            receivedOrder.Remove(frame);
            onHandleReceive?.Invoke(frame, states);
        }

        public override void OnReceiveMessage(IMessage message, Connect connect)
        {
            if (message is General_FrameStateData frameMessage && frameMessage.data != null)
                connect.RecordBattleFrameReceive(frameMessage.clientFrameSequence);

            base.OnReceiveMessage(message, connect);
        }

        protected override void SendFrame(NetworkFrameData data)
        {
            foreach (Connect connect in connects)
            {
                connect.Send(new General_FrameStateData
                {
                    data = data,
                    acknowledgedClientFrameSequence = connect.LastReceivedBattleFrameSequence,
                });
            }

            foreach (Connect connect in syncingConnects)
            {
                connect.Send(new General_FrameStateData
                {
                    data = data,
                });
            }
        }

        public override void Stop()
        {
            for (int index = connects.Count - 1; index >= 0; index--)
            {
                RemoveConnect(connects[index]);
            }

            syncingConnects.Clear();

            base.Stop();
        }
    }
}
