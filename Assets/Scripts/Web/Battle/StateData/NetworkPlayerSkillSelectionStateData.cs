using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkPlayerSkillSelectionStateData : NetworkStateData
{
    public int currentChainIndex;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.TryGetEntity(unitId, out Entity entity))
        {
            context.SetOrAdd(entity, new PlayerSkillSelectionComponent
            {
                CurrentChainIndex = currentChainIndex,
                NetworkDirty = 0,
            });
            PlayerSkillRuntimeDataUtility.SetCurrentChain(context.EntityManager, entity, currentChainIndex);
        }
    }
}
