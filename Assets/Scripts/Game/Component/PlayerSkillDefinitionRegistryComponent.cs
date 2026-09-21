using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;

public struct PlayerSkillDefinitionBlob
{
    public int Id;
    public int MpCost;
    public float ChantDuration;
    public float CastingMoveMultiplier;
    public FixedString128Bytes RuntimeType;
    public byte RuntimeTypeValid;
    public SkillInputType InputType;
}

public struct PlayerSkillModifierMinimumFactorBlob
{
    public SkillModifierChannel Channel;
    public float MinimumFactor;
}

public struct PlayerSkillDefinitionRegistryBlob
{
    public BlobArray<PlayerSkillDefinitionBlob> Skills;
    public BlobArray<PlayerSkillModifierMinimumFactorBlob> ModifierMinimumFactors;
}

public struct PlayerSkillDefinitionRegistryComponent : IComponentData
{
    public BlobAssetReference<PlayerSkillDefinitionRegistryBlob> Value;
}

public static class PlayerSkillDefinitionRegistryUtility
{
    public static bool TryGet(
        EntityManager entityManager,
        out BlobAssetReference<PlayerSkillDefinitionRegistryBlob> registry)
    {
        registry = default;
        Entity worldEntity = WorldStateUtility.GetEntity(entityManager);
        registry = entityManager.GetComponentData<PlayerSkillDefinitionRegistryComponent>(worldEntity).Value;
        return registry.IsCreated;
    }

    public static bool TryGetSkill(
        in BlobAssetReference<PlayerSkillDefinitionRegistryBlob> registry,
        int skillId,
        out PlayerSkillDefinitionBlob skill)
    {
        skill = default;
        if (!registry.IsCreated)
            return false;

        ref PlayerSkillDefinitionRegistryBlob value = ref registry.Value;
        int low = 0;
        int high = value.Skills.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            PlayerSkillDefinitionBlob candidate = value.Skills[middle];
            if (candidate.Id == skillId)
            {
                skill = candidate;
                return true;
            }

            if (candidate.Id < skillId)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return false;
    }

    public static bool TryGetModifierMinimumFactor(
        in BlobAssetReference<PlayerSkillDefinitionRegistryBlob> registry,
        SkillModifierChannel channel,
        out float minimumFactor)
    {
        minimumFactor = 0f;
        if (!registry.IsCreated)
            return false;

        ref PlayerSkillDefinitionRegistryBlob value = ref registry.Value;
        int rawChannel = (int)channel;
        int low = 0;
        int high = value.ModifierMinimumFactors.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            PlayerSkillModifierMinimumFactorBlob candidate = value.ModifierMinimumFactors[middle];
            int candidateChannel = (int)candidate.Channel;
            if (candidateChannel == rawChannel)
            {
                minimumFactor = candidate.MinimumFactor;
                return true;
            }

            if (candidateChannel < rawChannel)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return false;
    }
}
