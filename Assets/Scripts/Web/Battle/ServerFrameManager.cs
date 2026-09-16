using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ServerFrameManager : FrameManager<ServerFrameManager>
    {
        public List<Connect> connects = new List<Connect>();
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

        public override void RemoveConnect(Connect connect)
        {
            if (connect == null || !connects.Remove(connect))
            {
                return;
            }

            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
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

        protected override void SendFrame(NetworkFrameData data)
        {
            foreach (Connect connect in connects)
            {
                connect.Send(new General_FrameStateData { data = data });
            }
        }

        public override void Stop()
        {
            for (int index = connects.Count - 1; index >= 0; index--)
            {
                RemoveConnect(connects[index]);
            }

            base.Stop();
        }
    }
}
