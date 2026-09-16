using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class FrameManager<T> : SingletonNonMono<T> where T : FrameManager<T>, new()
    {
        public int frameInterval = 33;
        public bool running;
        public uint currentFrame;
        public long timerId;

        public SortedDictionary<uint, Queue<NetworkState>> receivedOrder = new SortedDictionary<uint, Queue<NetworkState>>();
        public SortedDictionary<uint, Queue<NetworkStateData>> sendOrder = new SortedDictionary<uint, Queue<NetworkStateData>>();

        public Action<uint, Queue<NetworkState>> onHandleReceive;
        public Action<uint, Queue<NetworkStateData>> onSendMessage;
        public virtual void OnTick()
        {
            HandleReceive();
            SendMessage();
            currentFrame++;
        }
        public virtual void SendMessage()
        {
            if(sendOrder.TryGetValue(currentFrame, out var queue))
            {
                NetworkFrameData data = new NetworkFrameData() { frameId = currentFrame };
                data.datas = queue.ToList();
                SendFrame(data);
                onSendMessage?.Invoke(currentFrame, queue);

                sendOrder.Remove(currentFrame);
            }

        }
        public virtual void OnReceiveMessage(IMessage message,Connect connect)
        {
            General_FrameStateData realMessage = message as General_FrameStateData;
            if (realMessage == null || realMessage.data == null)
            {
                return;
            }

            NetworkFrameData frameData = realMessage.data;
            if (!receivedOrder.ContainsKey(frameData.frameId))
            {
                receivedOrder.Add(frameData.frameId, new Queue<NetworkState>());
            }
            foreach (var data in frameData.datas)
            {
                NetworkState state = new NetworkState() { data = data, hasChecked = false };
                receivedOrder[frameData.frameId].Enqueue(state);
            }
        }
        public virtual void HandleReceive() { }
        protected virtual void SendFrame(NetworkFrameData data){}
        public virtual void AddConnect(Connect connect){}
        public virtual void RemoveConnect(Connect connect){}
        public virtual void Start()
        {
            if (running)
                return;

            currentFrame = 0;
            timerId = NetworkTimer.Instance.AddRepeated(frameInterval, OnTick);
            running = true;
        }
        public virtual void Stop()
        {
            if (!running)
                return;

            NetworkTimer.Instance.Remove(timerId);
            currentFrame = 0;
            running = false;
            timerId = -1;
        }
    }
}
