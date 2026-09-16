using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkPlayerPropCooldownStateData : NetworkStateData
{
    public uint endFrame;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.TryGetEntity(unitId, out Entity entity))
            context.SetOrAdd(entity, new PlayerPropCooldownComponent
            {
                SharedCooldownRemaining = context.GetRemainingSeconds(endFrame),
                NetworkDirty = 0,
            });
    }
}
