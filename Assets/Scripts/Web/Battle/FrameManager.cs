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
        public int sendInterval = 33;
        private long lastSendTime;
        private bool immediateFlushRequested;
        public bool running;
        // Next player simulation tick, independent of world updates and packet flushes.
        public uint currentFrame;
        public uint sceneVersion;
        public readonly BattleFrameClock clock = new BattleFrameClock();

        public SortedDictionary<uint, Queue<NetworkState>> receivedOrder = new SortedDictionary<uint, Queue<NetworkState>>();
        public SortedDictionary<uint, Queue<NetworkStateData>> sendOrder = new SortedDictionary<uint, Queue<NetworkStateData>>();

        public Action<uint, Queue<NetworkState>> onHandleReceive;
        public Action<uint, Queue<NetworkStateData>> onSendMessage;
        public virtual void OnTick()
        {
            if (running)
                currentFrame++;
        }
        public virtual void SendMessage()
        {
            List<NetworkFrameData> frames = new();
            while (sendOrder.Count > 0)
            {
                var pending = sendOrder.First();
                // Never transmit an input frame before prediction has consumed it.
                if (this is ClientFrameManager && pending.Key >= currentFrame)
                    break;
                frames.Add(new NetworkFrameData { frameId = pending.Key, datas = pending.Value.ToList() });
                onSendMessage?.Invoke(pending.Key, pending.Value);
                sendOrder.Remove(pending.Key);
            }
            if (this is ServerFrameManager)
                NetworkSnapshotBatchUtility.Coalesce(frames);
            if (frames.Count > 0)
                SendFrames(frames);
        }

        public void RequestImmediateFlush() => immediateFlushRequested = true;

        public bool FlushNetwork(long now, bool immediate = false)
        {
            if (!running || (!immediate && !immediateFlushRequested &&
                now - lastSendTime < Math.Max(1, sendInterval)))
                return false;
            // One flush after a stall, never a burst of overdue send timers.
            lastSendTime = now;
            immediateFlushRequested = false;
            SendMessage();
            return true;
        }
        public virtual void OnReceiveMessage(IMessage message,Connect connect)
        {
            General_FrameStateData realMessage = message as General_FrameStateData;
            if (realMessage?.frames == null || realMessage.sceneVersion != sceneVersion)
            {
                return;
            }

            foreach (NetworkFrameData frameData in realMessage.frames)
            {
                if (frameData?.datas == null)
                    continue;
                if (!receivedOrder.TryGetValue(frameData.frameId, out var states))
                    receivedOrder.Add(frameData.frameId, states = new Queue<NetworkState>());
                foreach (var data in frameData.datas)
                    if (data != null)
                        states.Enqueue(new NetworkState { data = data });
            }
        }
        public virtual void HandleReceive() { }
        protected virtual void SendFrames(List<NetworkFrameData> frames){}
        public virtual void AddConnect(Connect connect){}
        public virtual void RemoveConnect(Connect connect){}
        public virtual void ClearOrders()
        {
            receivedOrder.Clear();
            sendOrder.Clear();
            immediateFlushRequested = false;
            lastSendTime = NetworkTimer.Instance.TimeNow;
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
            clock.Reset(NetworkTimer.Instance.TimeNow);
            lastSendTime = NetworkTimer.Instance.TimeNow;
            running = true;
        }
        public virtual void Stop()
        {
            ClearOrders();
            currentFrame = 0;
            running = false;
            clock.Reset(NetworkTimer.Instance.TimeNow);
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
            }
            else
                entityManager.GetComponentObject<FrameManagerComponent>(query.GetSingletonEntity()).manager = manager;

            BattleSimulationSystemGroup.Bind(entityManager.World, manager);
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
