using CrystalMagic.Core;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(PlayerInputBridgeSystem))]
public partial class PlayerRuntimeStateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((RefRO<UnitFactionComponent> factionRef, Entity entity) in
                 SystemAPI.Query<RefRO<UnitFactionComponent>>().WithEntityAccess())
        {
            if (!UnitFactionUtility.IsPlayer(factionRef.ValueRO.Value))
                continue;

            if (!EntityManager.HasComponent<PlayerSkillSelectionComponent>(entity))
                EntityManager.AddComponentData(entity, new PlayerSkillSelectionComponent());

            if (!EntityManager.HasComponent<PlayerPropCooldownComponent>(entity))
                EntityManager.AddComponentData(entity, new PlayerPropCooldownComponent());

            if (!EntityManager.HasComponent<PlayerSkillRuntimeDataComponent>(entity))
                EntityManager.AddComponentObject(entity, new PlayerSkillRuntimeDataComponent());

            if (!GameRuntimeStateUtility.TryGetPlayerCharacterData(EntityManager, entity, out CharacterData characterData))
                continue;

            PlayerSkillSelectionComponent selection = EntityManager.GetComponentData<PlayerSkillSelectionComponent>(entity);
            int chainCount = characterData.Skills?.Chains?.Length ?? 0;
            int maxIndex = chainCount > 0 ? chainCount - 1 : 0;
            if (selection.CurrentChainIndex < 0 || selection.CurrentChainIndex > maxIndex)
            {
                selection.CurrentChainIndex = 0;
                selection.NetworkDirty = 1;
                EntityManager.SetComponentData(entity, selection);
            }

            EntityManager.GetComponentObject<PlayerSkillRuntimeDataComponent>(entity)
                .Synchronize(characterData.Skills, DataComponent.Instance, selection.CurrentChainIndex);
        }
    }
}
