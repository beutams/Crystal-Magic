using System;
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
        public readonly SortedDictionary<uint, ClientPlayerPredictionSnapshot> playerStates = new();
        public readonly SortedDictionary<uint, Queue<NetworkStateData>> inputOrder = new();
        public uint latestServerFrame;
        public bool hasLatestServerFrame;
        public uint lastPredictedFrame;
        public bool hasLastPredictedFrame;
        public PlayerFrameHandler onHandlePlayerFrame;
        private uint pendingReplayBaseFrame;
        private bool hasPendingReplay;
        public bool HasPendingPredictionReplay => hasPendingReplay;
        private Guid characterEditRequestId;
        private Guid characterEditUnitId;
        private readonly Queue<NetworkPlayerOperationData> pendingOperations = new();
        private uint pendingSkillSelectionFrame;
        private bool hasPendingSkillSelection;
        private uint lastAppliedSkillSelectionFrame;
        private bool hasAppliedSkillSelection;
        public bool HasPendingCharacterEdit => characterEditRequestId != Guid.Empty;
        public bool CharacterEditsBlocked;

        public bool EnqueueOperation(NetworkPlayerOperationData operation)
        {
            if (!running || connect == null || operation == null)
                return false;

            pendingOperations.Enqueue(operation);
            if (operation is NetworkSkillChainSelectData)
            {
                pendingSkillSelectionFrame = currentFrame;
                hasPendingSkillSelection = true;
            }
            return true;
        }

        public void AppendPendingOperations(Guid unitId, Queue<NetworkStateData> states)
        {
            if (unitId == Guid.Empty || states == null)
                return;

            while (pendingOperations.Count > 0)
            {
                NetworkPlayerOperationData operation = pendingOperations.Dequeue();
                operation.unitId = unitId;
                states.Enqueue(operation);
                RecordInput(currentFrame, operation);
            }
        }

        public bool ShouldApplySkillChainState(uint serverFrame)
        {
            if (hasPendingSkillSelection && serverFrame < pendingSkillSelectionFrame)
            {
                return false;
            }

            if (hasAppliedSkillSelection && serverFrame < lastAppliedSkillSelectionFrame)
                return false;

            // 达到该输入帧的服务器状态即可纠正本地选择，包括输入迟到被丢弃的情况。
            hasPendingSkillSelection = false;
            lastAppliedSkillSelectionFrame = serverFrame;
            hasAppliedSkillSelection = true;
            return true;
        }

        private void ResetOperationState()
        {
            pendingOperations.Clear();
            pendingSkillSelectionFrame = 0;
            hasPendingSkillSelection = false;
            lastAppliedSkillSelectionFrame = 0;
            hasAppliedSkillSelection = false;
        }

        public bool TrySendCharacterEdit(NetworkCharacterEditData edit)
        {
            if (!running || connect == null || CharacterEditsBlocked || HasPendingCharacterEdit || edit?.characterData == null)
                return false;

            NetworkCharacterEditData request = new()
            {
                unitId = edit.unitId,
                requestId = Guid.NewGuid(),
                revision = edit.revision,
                characterData = PlayerCharacterUtility.Clone(edit.characterData),
            };
            characterEditRequestId = request.requestId;
            characterEditUnitId = request.unitId;
            if (!sendOrder.TryGetValue(currentFrame, out Queue<NetworkStateData> states))
            {
                states = new Queue<NetworkStateData>();
                sendOrder.Add(currentFrame, states);
            }
            states.Enqueue(request);
            return true;
        }

        public bool CompleteCharacterEdit(Guid unitId, Guid requestId)
        {
            if (!HasPendingCharacterEdit || unitId != characterEditUnitId || requestId != characterEditRequestId)
                return false;

            characterEditRequestId = Guid.Empty;
            characterEditUnitId = Guid.Empty;
            return true;
        }

        private long pingTimerId;
        private bool hasRtt;
        private bool hasServerClock;
        private double serverFrameAtSample;
        private long serverClockSampleTime;
        private long lastPongSendTime = -1;

        public bool FrameSpeedAdjustmentEnabled { get; private set; } = true;
        public double SmoothedRttMs { get; private set; }
        public double RttJitterMs { get; private set; }
        public int TargetAheadFrames { get; private set; } = 1;
        public double EstimatedServerFrame { get; private set; }
        public double CurrentAheadFrames { get; private set; }
        public double SimulationSpeed { get; private set; } = 1;

        public void SetFrameSpeedAdjustmentEnabled(bool enabled)
        {
            FrameSpeedAdjustmentEnabled = enabled;
            if (!enabled)
                SimulationSpeed = 1;
        }

        public bool HasFreshServerClock(long now)
        {
            return hasServerClock && now - serverClockSampleTime <= 2000;
        }

        public double UpdateSimulationSpeed(long now)
        {
            TargetAheadFrames = Math.Min(32, Math.Max(1,
                (int)Math.Ceiling((SmoothedRttMs * 0.5 + RttJitterMs) / frameInterval) + 1));
            EstimatedServerFrame = serverFrameAtSample + Math.Max(0, now - serverClockSampleTime) / (double)frameInterval;
            CurrentAheadFrames = currentFrame + Math.Min(1, clock.AccumulatedMilliseconds / frameInterval) - EstimatedServerFrame;
            double error = TargetAheadFrames - CurrentAheadFrames;
            SimulationSpeed = !FrameSpeedAdjustmentEnabled || !HasFreshServerClock(now) || Math.Abs(error) <= 0.5
                ? 1
                : Math.Max(0.9, Math.Min(1.1, 1 + error * frameInterval / 1000));
            return SimulationSpeed;
        }

        public void Start(uint startFrame, int interval, double frameElapsedMs)
        {
            if (running)
                return;
            frameInterval = Math.Max(1, interval);
            base.Start(startFrame);
            serverFrameAtSample = startFrame + (frameElapsedMs + SmoothedRttMs * 0.5) / frameInterval;
            serverClockSampleTime = NetworkTimer.Instance.TimeNow;
            hasServerClock = true;
        }

        private void SendFramePing()
        {
            connect.Send(new C2B_FramePing { clientSendTime = NetworkTimer.Instance.TimeNow, sceneVersion = sceneVersion });
        }

        private void OnFramePong(IMessage message, Connect receivedConnect)
        {
            if (receivedConnect != connect || message is not B2C_FramePong pong)
                return;
            ReceiveClockSample(pong, NetworkTimer.Instance.TimeNow);
        }

        public void ReceiveClockSample(B2C_FramePong pong, long receiveTime)
        {
            if (pong.sceneVersion != sceneVersion || pong.clientSendTime <= lastPongSendTime || pong.clientSendTime > receiveTime)
                return;
            lastPongSendTime = pong.clientSendTime;
            double rtt = receiveTime - pong.clientSendTime;
            if (!hasRtt)
            {
                SmoothedRttMs = rtt;
                hasRtt = true;
            }
            else
            {
                RttJitterMs += (Math.Abs(rtt - SmoothedRttMs) - RttJitterMs) * 0.25;
                SmoothedRttMs += (rtt - SmoothedRttMs) * 0.125;
            }

            if (pong.running)
            {
                // 用本机 RTT 推算回程，不相减两台机器起点不同的 Stopwatch。
                serverFrameAtSample = pong.serverFrame + (pong.frameElapsedMs + rtt * 0.5) / frameInterval;
                serverClockSampleTime = receiveTime;
                hasServerClock = true;
            }
        }

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

        public void RecordPlayerStates(uint frame, ClientPlayerPredictionSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            playerStates[frame] = snapshot;
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

        public void RequestPredictionReplay(uint authoritativeFrame)
        {
            if (!hasPendingReplay || authoritativeFrame >= pendingReplayBaseFrame)
            {
                pendingReplayBaseFrame = authoritativeFrame;
                hasPendingReplay = true;
            }
        }

        public bool TryConsumePredictionReplay(out uint authoritativeFrame)
        {
            authoritativeFrame = pendingReplayBaseFrame;
            if (!hasPendingReplay)
                return false;

            pendingReplayBaseFrame = 0;
            hasPendingReplay = false;
            return true;
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
                this.connect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_FramePong>(), OnFramePong);
                pingTimerId = NetworkTimer.Instance.AddRepeated(500, SendFramePing);
                SendFramePing();
            }
        }

        public override void RemoveConnect(Connect connect)
        {
            if (connect == null || this.connect != connect)
            {
                return;
            }

            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<General_FrameStateData>(), OnReceiveMessage);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_FramePong>(), OnFramePong);
            NetworkTimer.Instance.Remove(pingTimerId);
            pingTimerId = 0;
            this.connect = null;
            characterEditRequestId = Guid.Empty;
            characterEditUnitId = Guid.Empty;
            ResetOperationState();
            CharacterEditsBlocked = false;
        }

        public override void HandleReceive()
        {
            while (receivedOrder.Count > 0)
            {
                var frame = receivedOrder.First();
                // 当前正要模拟 N，权威状态最多应用到 N - 1。
                if (frame.Key >= currentFrame)
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
                    sceneVersion = sceneVersion,
                });
            }
        }

        public override void OnReceiveMessage(IMessage message, Connect receivedConnect)
        {
            if (receivedConnect != connect || message is not General_FrameStateData receivedFrame ||
                receivedFrame.sceneVersion != sceneVersion)
                return;
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
            characterEditRequestId = Guid.Empty;
            characterEditUnitId = Guid.Empty;
            ResetOperationState();
            playerStates.Clear();
            inputOrder.Clear();
            latestServerFrame = 0;
            hasLatestServerFrame = false;
            lastPredictedFrame = 0;
            hasLastPredictedFrame = false;
            pendingReplayBaseFrame = 0;
            hasPendingReplay = false;
            hasRtt = false;
            hasServerClock = false;
            serverFrameAtSample = 0;
            serverClockSampleTime = 0;
            lastPongSendTime = -1;
            SmoothedRttMs = 0;
            RttJitterMs = 0;
            TargetAheadFrames = 1;
            EstimatedServerFrame = 0;
            CurrentAheadFrames = 0;
            SimulationSpeed = 1;
        }

        private static void TrimHistory<T>(SortedDictionary<uint, T> history)
        {
            while (history.Count > PredictionHistoryCapacity)
                history.Remove(history.First().Key);
        }

        private static void RemoveHistoryThrough<T>(
            SortedDictionary<uint, T> history,
            uint frame)
        {
            while (history.Count > 0 && history.First().Key <= frame)
                history.Remove(history.First().Key);
        }

    }
}
