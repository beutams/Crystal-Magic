using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

namespace Server
{
    public class FrameManager
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
        public virtual void ClearOrders()
        {
            receivedOrder.Clear();
            sendOrder.Clear();
        }
        public virtual void Start()
        {
            Start(0U);
        }
        public virtual void Start(uint startFrame)
        {
            if (running)
                return;

            currentFrame = startFrame;
            timerId = NetworkTimer.Instance.AddRepeated(frameInterval, OnTick);
            running = true;
        }
        public virtual void Stop()
        {
            if (running)
            {
                NetworkTimer.Instance.Remove(timerId);
            }

            ClearOrders();
            currentFrame = 0;
            running = false;
            timerId = -1;
        }
    }

    public sealed class FrameManagerComponent : IComponentData
    {
        public FrameManager manager;
    }

    public static class FrameManagerUtility
    {
        public static void Bind(EntityManager entityManager, FrameManager manager)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FrameManagerComponent>());
            if (query.IsEmptyIgnoreFilter)
            {
                Entity entity = entityManager.CreateEntity();
                entityManager.AddComponentObject(entity, new FrameManagerComponent { manager = manager });
                return;
            }

            entityManager.GetComponentObject<FrameManagerComponent>(query.GetSingletonEntity()).manager = manager;
        }

        public static bool TryGet(EntityManager entityManager, out FrameManager manager)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FrameManagerComponent>());
            if (query.IsEmptyIgnoreFilter)
            {
                manager = null;
                return false;
            }

            manager = entityManager
                .GetComponentObject<FrameManagerComponent>(query.GetSingletonEntity())
                ?.manager;
            return manager != null;
        }

        public static bool TryGet<T>(EntityManager entityManager, out T manager) where T : FrameManager
        {
            if (TryGet(entityManager, out FrameManager frameManager) && frameManager is T typedManager)
            {
                manager = typedManager;
                return true;
            }

            manager = null;
            return false;
        }
    }
}
