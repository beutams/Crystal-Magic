using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkInteractableStateData : NetworkStateData
{
    public InteractionKind kind;
    public int dataId;
    public int amount;
    public int variant;
    public float rangeSq;
    public byte isEnabled;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        context.SetOrAdd(entity, new UnitInteractableComponent
        {
            Data = new UnitInteractionData
            {
                Kind = kind,
                DataId = dataId,
                Amount = amount,
                Variant = variant,
            },
            RangeSq = rangeSq,
            IsEnabled = isEnabled,
            NetworkDirty = 0,
        });
    }
}
