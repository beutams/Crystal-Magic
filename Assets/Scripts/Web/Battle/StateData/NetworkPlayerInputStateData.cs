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
    public byte isInteractHeld;
    public byte isInventoryHeld;
    public byte isPropertyHeld;
    public byte isEscapeHeld;
    public byte isSkillHeld;
    public int skillChainIndex;
    public byte isNextSkillChainHeld;
    public byte isUsePropHeld;
    public int propIndex;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        context.SetOrAdd(entity, new PlayerInputComponent
        {
            Move = new float2(moveX, moveY),
            PointerWorldPosition = new float3(pointerX, pointerY, pointerZ),
            IsPrimaryHeld = isPrimaryHeld,
            IsInteractHeld = isInteractHeld,
            IsInventoryHeld = isInventoryHeld,
            IsPropertyHeld = isPropertyHeld,
            IsEscapeHeld = isEscapeHeld,
            IsSkillHeld = isSkillHeld,
            SkillChainIndex = skillChainIndex,
            IsNextSkillChainHeld = isNextSkillChainHeld,
            IsUsePropHeld = isUsePropHeld,
            PropIndex = propIndex,
            NetworkDirty = 0,
        });
    }
}
