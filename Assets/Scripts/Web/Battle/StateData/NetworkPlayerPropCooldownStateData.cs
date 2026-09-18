using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkPlayerPropCooldownStateData : NetworkStateData
{
    public uint endFrame;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        float remainingTime = context.GetRemainingSeconds(endFrame);
        context.SetOrAdd(entity, new PlayerPropCooldownComponent
        {
            SharedCooldownRemaining = remainingTime,
            NetworkDirty = 0,
        });

        if (context.IsClient)
        {
            context.SetOrAdd(entity, new ClientCooldownPresentationComponent
            {
                PropCooldownEndFrame = endFrame,
                PropCooldownRemaining = remainingTime,
            });
        }
    }
}
