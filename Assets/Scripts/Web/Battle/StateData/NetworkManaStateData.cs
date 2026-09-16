using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkManaStateData : NetworkStateData
{
    public float baseMaxMp;
    public float baseMaxMpOffset;
    public float currentMana;
    public float baseMpRegenPerSecond;
    public float baseMpRegenPerSecondOffset;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        context.SetOrAdd(entity, new UnitManaComponent
        {
            BaseMaxMp = baseMaxMp,
            BaseMaxMpOffset = baseMaxMpOffset,
            CurrentMana = currentMana,
            BaseMpRegenPerSecond = baseMpRegenPerSecond,
            BaseMpRegenPerSecondOffset = baseMpRegenPerSecondOffset,
            NetworkDirty = 0,
        });
    }
}
