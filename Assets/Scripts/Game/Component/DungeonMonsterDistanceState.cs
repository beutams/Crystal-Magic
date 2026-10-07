using Unity.Entities;

// The entity itself retains health, ownership and script buffers while Disabled.
// Sleeping is never a death or a new spawn from the encounter's perspective.
public struct DungeonMonsterDistanceState : IComponentData
{
    public double FarSince;
    public float LastHealth;
    public byte HasHealth;
    public byte Sleeping;
}

public enum DungeonMonsterSleepBlockReason
{
    None,
    Initializing,
    Buff,
    Control,
    PendingEffect,
    Referenced,
    Combat,
    RunningAction,
    Perception,
    RecentDamage,
    FeatureDisabled,
    NoObserver,
}
