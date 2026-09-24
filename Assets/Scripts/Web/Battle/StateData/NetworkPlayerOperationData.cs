using System;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public abstract class NetworkPlayerOperationData : NetworkStateData
{
    protected bool TryGetControllablePlayer(
        NetworkStateApplyContext context,
        out Entity entity,
        out PlayerInputComponent input)
    {
        input = default;
        if (context.IsClient ||
            !context.TryGetEntity(unitId, out entity) ||
            !context.EntityManager.HasComponent<PlayerInputComponent>(entity))
        {
            entity = Entity.Null;
            return false;
        }

        input = context.EntityManager.GetComponentData<PlayerInputComponent>(entity);
        return !context.EntityManager.HasComponent<BattlePlayerStatusComponent>(entity) ||
               !context.EntityManager.GetComponentData<BattlePlayerStatusComponent>(entity).IsInputLocked;
    }
}

[Serializable]
public sealed class NetworkPrimaryPressData : NetworkPlayerOperationData
{
    public float pointerX;
    public float pointerY;
    public float pointerZ;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!TryGetControllablePlayer(context, out Entity entity, out PlayerInputComponent input))
            return;

        input.PointerWorldPosition = new float3(pointerX, pointerY, pointerZ);
        PlayerInputEventUtility.Append(context.EntityManager, entity, PlayerInputOperationType.PrimaryPressed, input);
    }
}

[Serializable]
public sealed class NetworkInteractData : NetworkPlayerOperationData
{
    public override void Apply(NetworkStateApplyContext context)
    {
        if (!TryGetControllablePlayer(context, out Entity entity, out PlayerInputComponent input))
            return;

        PlayerInputEventUtility.Append(context.EntityManager, entity, PlayerInputOperationType.Interact, input);
    }
}

[Serializable]
public sealed class NetworkSkillChainSelectData : NetworkPlayerOperationData
{
    public int skillChainIndex;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.IsClient ||
            !context.TryGetEntity(unitId, out Entity entity) ||
            !context.EntityManager.HasComponent<PlayerInputComponent>(entity))
        {
            return;
        }

        PlayerInputComponent input = context.EntityManager.GetComponentData<PlayerInputComponent>(entity);
        bool inputLocked = context.EntityManager.HasComponent<BattlePlayerStatusComponent>(entity) &&
                           context.EntityManager.GetComponentData<BattlePlayerStatusComponent>(entity).IsInputLocked;
        if (!inputLocked && skillChainIndex >= 0 && skillChainIndex < 5)
        {
            input.SkillChainIndex = skillChainIndex;
            PlayerInputEventUtility.Append(context.EntityManager, entity, PlayerInputOperationType.SelectSkillChain, input);
        }

        // 同帧最终选择由正常状态同步返回，包括被拒绝的选择。
        input.NetworkDirty = 1;
        context.EntityManager.SetComponentData(entity, input);
    }
}
