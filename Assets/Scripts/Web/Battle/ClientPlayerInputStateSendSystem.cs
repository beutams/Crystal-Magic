using Server;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientInputSystemGroup), OrderLast = true)]
public partial class ClientPlayerInputStateSendSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!ClientFrameManager.Instance.running)
            return;

        foreach ((RefRW<PlayerInputComponent> inputRef,
                  RefRO<NetworkIdentityComponent> identityRef) in
                 SystemAPI.Query<RefRW<PlayerInputComponent>, RefRO<NetworkIdentityComponent>>()
                     .WithAll<NetworkPlayerComponent>())
        {
            PlayerInputComponent input = inputRef.ValueRO;
            if (input.NetworkDirty == 0 || identityRef.ValueRO.id == System.Guid.Empty)
                continue;

            uint frame = ClientFrameManager.Instance.currentFrame;
            if (!ClientFrameManager.Instance.sendOrder.TryGetValue(frame, out System.Collections.Generic.Queue<NetworkStateData> queue))
            {
                queue = new System.Collections.Generic.Queue<NetworkStateData>();
                ClientFrameManager.Instance.sendOrder.Add(frame, queue);
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
