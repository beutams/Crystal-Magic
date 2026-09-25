using System.Collections.Generic;
using Server;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientInputSystemGroup), OrderLast = true)]
public partial class ClientPlayerInputStateSendSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame) || !frame.running)
            return;

        foreach ((RefRW<PlayerInputComponent> inputRef,
                  RefRO<NetworkIdentityComponent> identityRef) in
                 SystemAPI.Query<RefRW<PlayerInputComponent>, RefRO<NetworkIdentityComponent>>()
                     .WithAll<NetworkPlayerComponent>())
        {
            PlayerInputComponent input = inputRef.ValueRO;
            if (identityRef.ValueRO.id == System.Guid.Empty)
                continue;

            uint currentFrame = frame.currentFrame;
            NetworkPlayerInputStateData inputState = CreateInputState(identityRef.ValueRO.id, input);
            if (!frame.inputOrder.ContainsKey(currentFrame) || input.NetworkDirty != 0)
                frame.RecordInput(currentFrame, inputState);

            // 每个逻辑帧携带完整输入：网络卡顿后旧帧被丢弃，仍可恢复持续按键/松键状态。

            if (!frame.sendOrder.TryGetValue(currentFrame, out Queue<NetworkStateData> queue))
            {
                queue = new Queue<NetworkStateData>();
                frame.sendOrder.Add(currentFrame, queue);
            }

            queue.Enqueue(inputState);
            frame.AppendPendingOperations(identityRef.ValueRO.id, queue);

            input.NetworkDirty = 0;
            inputRef.ValueRW = input;
        }
    }

    private static NetworkPlayerInputStateData CreateInputState(
        System.Guid unitId,
        in PlayerInputComponent input)
    {
        return new NetworkPlayerInputStateData
        {
            unitId = unitId,
            moveX = input.Move.x,
            moveY = input.Move.y,
            pointerX = input.PointerWorldPosition.x,
            pointerY = input.PointerWorldPosition.y,
            pointerZ = input.PointerWorldPosition.z,
            isPrimaryHeld = input.ContinuousPrimaryHeld,
        };
    }
}
