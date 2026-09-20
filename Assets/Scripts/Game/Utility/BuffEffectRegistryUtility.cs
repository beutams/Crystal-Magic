using Unity.Entities;

public static class BuffEffectRegistryUtility
{
    public static bool TryGet(EntityManager entityManager, out BlobAssetReference<BuffEffectRegistryBlob> registry)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuffEffectRegistryComponent>());
        if (query.IsEmptyIgnoreFilter)
        {
            registry = default;
            return false;
        }

        registry = query.GetSingleton<BuffEffectRegistryComponent>().Value;
        return registry.IsCreated;
    }

    public static int FindBuffIndex(in BlobAssetReference<BuffEffectRegistryBlob> registry, int buffId)
    {
        if (!registry.IsCreated)
            return -1;

        ref BuffEffectRegistryBlob value = ref registry.Value;
        for (int i = 0; i < value.Buffs.Length; i++)
        {
            if (value.Buffs[i].BuffId == buffId)
                return i;
        }

        return -1;
    }
}
