using System.Collections.Generic;
using Server;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientPlayerPredictionSystemGroup), OrderLast = true)]
public partial class ClientPlayerStateRecordSystem : SystemBase
{
    private ClientFrameManager _frameManager;
    private uint _lastRecordedFrame;
    private bool _hasRecordedFrame;

    protected override void OnUpdate()
    {
        if (!FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame) || !frame.running)
            return;

        if (_frameManager != frame)
        {
            _frameManager = frame;
            _lastRecordedFrame = 0;
            _hasRecordedFrame = false;
        }

        uint currentFrame = frame.currentFrame;
        if (_hasRecordedFrame && _lastRecordedFrame == currentFrame)
            return;

        foreach ((RefRO<NetworkIdentityComponent> identityRef, Entity entity) in
                 SystemAPI.Query<RefRO<NetworkIdentityComponent>>()
                     .WithAll<NetworkPlayerComponent>()
                     .WithEntityAccess())
        {
            Queue<NetworkStateData> states = NetworkUnitStateSnapshotUtility.CapturePlayerState(
                EntityManager,
                entity,
                identityRef.ValueRO.id);
            frame.RecordPlayerStates(currentFrame, states);
            _lastRecordedFrame = currentFrame;
            _hasRecordedFrame = true;
            break;
        }
    }
}
