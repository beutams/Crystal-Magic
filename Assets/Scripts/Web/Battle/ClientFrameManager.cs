using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ClientFrameManager : FrameManager<ClientFrameManager>
    {
        public Connect connect;

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
                connect.Send(new General_FrameStateData { data = data });
            }
        }

        public override void Stop()
        {
            RemoveConnect(connect);
            base.Stop();
        }

    }
}
