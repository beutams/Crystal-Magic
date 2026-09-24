using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Server
{
    public class ServerFrameManager : FrameManager
    {
        public List<Connect> connects = new List<Connect>();
        public List<Connect> syncingConnects = new List<Connect>();
        public SortedDictionary<uint, Queue<NetworkStateData>> allcmds = new SortedDictionary<uint, Queue<NetworkStateData>>();
        private readonly Dictionary<Connect, Guid> playerUnitIds = new();
        private readonly Dictionary<Connect, uint> lastReceivedInputFrames = new();

        public void AddConnect(Connect connect, Guid playerUnitId)
        {
            if (connect == null || playerUnitId == Guid.Empty)
                return;

            playerUnitIds[connect] = playerUnitId;
            AddConnect(connect);
        }

        public override void AddConnect(Connect connect)
        {
            if (connect == null || connects.Contains(connect))
            {
                return;
            }

            connects.Add(connect);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
        }

        public void AddSyncingConnect(Connect connect, Guid playerUnitId)
        {
            if (connect == null || playerUnitId == Guid.Empty ||
                connects.Contains(connect) || syncingConnects.Contains(connect))
            {
                return;
            }

            playerUnitIds[connect] = playerUnitId;
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
            // 旧连接已入队但尚未执行的输入不能留给新连接。
            if (playerUnitIds.TryGetValue(connect, out Guid unitId))
            {
                foreach (Queue<NetworkState> states in receivedOrder.Values)
                    RemoveUnitCommands(states, unitId);
            }
            playerUnitIds.Remove(connect);
            lastReceivedInputFrames.Remove(connect);
            if (connects.Remove(connect))
            {
                connect.UnRegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
            }
        }

        public override void HandleReceive()
        {
            while (receivedOrder.Count > 0 && receivedOrder.First().Key < currentFrame)
            {
                KeyValuePair<uint, Queue<NetworkState>> expiredFrame = receivedOrder.First();
                foreach (NetworkState state in expiredFrame.Value)
                    RejectExpiredRequest(state.data);
                receivedOrder.Remove(expiredFrame.Key);
            }

            // 现在由 ECS 帧首调用，currentFrame 已经是本轮要模拟的帧。
            uint frame = currentFrame;
            if (!receivedOrder.TryGetValue(frame, out Queue<NetworkState> states))
                return;

            receivedOrder.Remove(frame);
            onHandleReceive?.Invoke(frame, states);
        }

        private void RejectExpiredRequest(NetworkStateData data)
        {
            // 丢弃过期编辑，不修改角色；仅返回失败，解除客户端编辑界面的等待。
            if (data is not NetworkCharacterEditData edit)
                return;

            if (!sendOrder.TryGetValue(currentFrame, out Queue<NetworkStateData> responses))
            {
                responses = new Queue<NetworkStateData>();
                sendOrder.Add(currentFrame, responses);
            }
            responses.Enqueue(new NetworkCharacterStateData
            {
                unitId = edit.unitId,
                requestId = edit.requestId,
                accepted = false,
            });
        }

        private static void RemoveUnitCommands(Queue<NetworkState> states, Guid unitId)
        {
            int count = states.Count;
            for (int index = 0; index < count; index++)
            {
                NetworkState state = states.Dequeue();
                if (state.data?.unitId != unitId)
                    states.Enqueue(state);
            }
        }

        public override void OnReceiveMessage(IMessage message, Connect connect)
        {
            if (message is not General_FrameStateData frameMessage || frameMessage.data?.datas == null ||
                frameMessage.sceneVersion != sceneVersion || !connects.Contains(connect))
                return;

            connect?.RecordBattleFrameReceive(frameMessage.clientFrameSequence);
            // 每个客户端每帧只发送一个包，TCP 保持顺序；重复帧不能重复执行事件。
            if (lastReceivedInputFrames.TryGetValue(connect, out uint lastFrame) &&
                frameMessage.data.frameId <= lastFrame)
                return;
            lastReceivedInputFrames[connect] = frameMessage.data.frameId;
            foreach (NetworkStateData data in frameMessage.data.datas)
            {
                if (data == null || !playerUnitIds.TryGetValue(connect, out Guid playerUnitId) ||
                    data.unitId != playerUnitId)
                {
                    Debug.LogWarning(
                        $"[Battle][Input] Rejected state from {connect?.IPEndPoint}: unit={data?.unitId}.");
                    continue;
                }

                if (frameMessage.data.frameId < currentFrame)
                {
                    RejectExpiredRequest(data);
                    continue;
                }

                // 连续输入和一次性操作都只在消息指定的逻辑帧执行。
                if (!receivedOrder.TryGetValue(frameMessage.data.frameId, out Queue<NetworkState> states))
                {
                    states = new Queue<NetworkState>();
                    receivedOrder.Add(frameMessage.data.frameId, states);
                }
                states.Enqueue(new NetworkState { data = data });
            }
        }

        public override void ClearOrders()
        {
            base.ClearOrders();
            playerUnitIds.Clear();
            lastReceivedInputFrames.Clear();
        }

        protected override void SendFrame(NetworkFrameData data)
        {
            foreach (Connect connect in connects)
            {
                connect.Send(new General_FrameStateData
                {
                    data = data,
                    acknowledgedClientFrameSequence = connect.LastReceivedBattleFrameSequence,
                    sceneVersion = sceneVersion,
                });
            }

            foreach (Connect connect in syncingConnects)
            {
                connect.Send(new General_FrameStateData
                {
                    data = data,
                    sceneVersion = sceneVersion,
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
