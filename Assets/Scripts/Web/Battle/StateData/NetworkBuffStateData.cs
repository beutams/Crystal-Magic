using System;
using System.Collections.Generic;
using Unity.Entities;

[Serializable]
public sealed class NetworkBuffStateData : NetworkStateData
{
    public List<NetworkBuffEntryStateData> buffs = new();

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        UnitBuffRuntimeComponent runtime;
        if (context.EntityManager.HasComponent<UnitBuffRuntimeComponent>(entity))
            runtime = context.EntityManager.GetComponentObject<UnitBuffRuntimeComponent>(entity);
        else
        {
            runtime = new UnitBuffRuntimeComponent();
            context.EntityManager.AddComponentObject(entity, runtime);
        }

        runtime.Buffs.Clear();
        for (int index = 0; index < buffs.Count; index++)
        {
            NetworkBuffEntryStateData state = buffs[index];
            bool hasOriginEntity = context.TryGetEntity(state.originUnitId, out Entity originEntity);
            UnitBuffRuntimeEntry entry = new()
            {
                BuffId = state.buffId,
                RemainingTime = context.GetRemainingSeconds(state.endFrame),
                StackCount = state.stackCount,
                HasOriginEntity = hasOriginEntity,
                OriginEntity = hasOriginEntity ? originEntity : Entity.Null,
                SourceSkillId = state.sourceSkillId,
            };
            entry.EnsureDefinitionLoaded();
            runtime.Buffs.Add(entry);
        }

        runtime.NetworkDirty = 0;
    }
}

[Serializable]
public sealed class NetworkBuffEntryStateData
{
    public int buffId;
    public uint endFrame;
    public int stackCount;
    public Guid originUnitId;
    public int sourceSkillId;
}
