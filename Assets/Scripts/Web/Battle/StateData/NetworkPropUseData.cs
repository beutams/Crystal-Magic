using System;
using CrystalMagic.Core;
using CrystalMagic.Game;
using Unity.Entities;

[Serializable]
public sealed class NetworkPropUseData : NetworkPlayerOperationData
{
    public int slotIndex;
    public int itemId;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.IsClient || !context.TryGetEntity(unitId, out Entity player) ||
            BattlePlayerStatusUtility.IsInputLocked(context.EntityManager, player) ||
            !GameRuntimeStateUtility.TryGetPlayerCharacterData(context.EntityManager, player, out CharacterData data) ||
            !PropInventoryUtility.TryGetSlot(data.Props, slotIndex, out CharacterPropSlotData slot) ||
            slot.ItemId != itemId)
            return;

        if (PropUseUtility.TryBuildContext(context.EntityManager, player, out PropUseRequestContext request, out _))
            PropUseUtility.TryUsePropSlot(slotIndex, request, out _);
    }
}
