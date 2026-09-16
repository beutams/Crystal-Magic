using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkVitalityStateData : NetworkStateData
{
    public float baseMaxHealth;
    public float baseMaxHealthOffset;
    public float currentHealth;
    public float baseHealthRegenPerSecond;
    public float baseHealthRegenOffset;
    public float baseDefense;
    public float baseDefenseOffset;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        context.SetOrAdd(entity, new UnitVitalityComponent
        {
            BaseMaxHealth = baseMaxHealth,
            BaseMaxHealthOffset = baseMaxHealthOffset,
            CurrentHealth = currentHealth,
            BaseHealthRegenPerSecond = baseHealthRegenPerSecond,
            BaseHealthRegenOffset = baseHealthRegenOffset,
            BaseDefense = baseDefense,
            BaseDefenseOffset = baseDefenseOffset,
            NetworkDirty = 0,
        });
    }
}
