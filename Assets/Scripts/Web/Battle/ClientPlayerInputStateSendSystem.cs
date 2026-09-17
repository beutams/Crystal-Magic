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
            if (input.NetworkDirty == 0 || identityRef.ValueRO.id == System.Guid.Empty)
                continue;

            uint currentFrame = frame.currentFrame;
            if (!frame.sendOrder.TryGetValue(currentFrame, out System.Collections.Generic.Queue<NetworkStateData> queue))
            {
                queue = new System.Collections.Generic.Queue<NetworkStateData>();
                frame.sendOrder.Add(currentFrame, queue);
            }

            queue.Enqueue(new NetworkPlayerInputStateData
            {
                unitId = identityRef.ValueRO.id,
                moveX = input.Move.x,
                moveY = input.Move.y,
                pointerX = input.PointerWorldPosition.x,
                pointerY = input.PointerWorldPosition.y,
                pointerZ = input.PointerWorldPosition.z,
                isPrimaryHeld = input.IsPrimaryHeld,
                isInteractHeld = input.IsInteractHeld,
                isInventoryHeld = input.IsInventoryHeld,
                isPropertyHeld = input.IsPropertyHeld,
                isEscapeHeld = input.IsEscapeHeld,
                isSkillHeld = input.IsSkillHeld,
                skillChainIndex = input.SkillChainIndex,
                isNextSkillChainHeld = input.IsNextSkillChainHeld,
                isUsePropHeld = input.IsUsePropHeld,
                propIndex = input.PropIndex,
            });

            input.NetworkDirty = 0;
            inputRef.ValueRW = input;
        }
    }
}
