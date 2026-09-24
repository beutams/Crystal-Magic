using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public sealed class NetworkPlayerInputStateData : NetworkStateData
{
    public float moveX;
    public float moveY;
    public float pointerX;
    public float pointerY;
    public float pointerZ;
    public byte isPrimaryHeld;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        BattlePlayerStatusComponent status = context.EntityManager.HasComponent<BattlePlayerStatusComponent>(entity)
            ? context.EntityManager.GetComponentData<BattlePlayerStatusComponent>(entity) : default;
        bool transitionWaiting = status.IsWaitingForTransition;
        PlayerInputComponent input = context.EntityManager.HasComponent<PlayerInputComponent>(entity)
            ? context.EntityManager.GetComponentData<PlayerInputComponent>(entity)
            : default;
        input.Move = status.ConnectionState == BattlePlayerConnectionState.Offline || transitionWaiting
            ? float2.zero
            : new float2(moveX, moveY);
        input.PointerWorldPosition = new float3(pointerX, pointerY, pointerZ);
        input.ContinuousPrimaryHeld = status.IsInputLocked ? (byte)0 : isPrimaryHeld;
        input.IsPrimaryHeld = input.ContinuousPrimaryHeld;
        // 回传该输入帧下的选择结果；即便上一帧的切链事件迟到被丢弃，也能纠正客户端。
        input.NetworkDirty = 1;
        if (status.IsInputLocked)
        {
            input.IsInteractHeld = 0;
            input.IsInventoryHeld = 0;
            input.IsPropertyHeld = 0;
            input.IsEscapeHeld = 0;
            input.IsSkillHeld = 0;
            input.IsUsePropHeld = 0;
            input.PropIndex = -1;
        }
        context.SetOrAdd(entity, input);
    }
}
