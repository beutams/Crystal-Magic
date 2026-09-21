using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkPlayerSkillChainStateData : NetworkStateData
{
    public int skillChainIndex;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        if (!context.EntityManager.HasComponent<PlayerInputComponent>(entity))
        {
            context.EntityManager.AddComponentData(entity, new PlayerInputComponent
            {
                SkillChainIndex = skillChainIndex,
            });
            return;
        }

        PlayerInputComponent input = context.EntityManager.GetComponentData<PlayerInputComponent>(entity);
        input.SkillChainIndex = skillChainIndex;
        context.EntityManager.SetComponentData(entity, input);
    }
}
