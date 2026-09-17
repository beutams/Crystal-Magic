using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ClientFrameManager : FrameManager
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

    }
}
