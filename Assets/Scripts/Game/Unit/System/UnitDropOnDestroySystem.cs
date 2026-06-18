using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitPostProcessSystemGroup))]
[UpdateBefore(typeof(DestroyEntitySystem))]
partial class UnitDropOnDestroySystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((EnabledRefRO<DestroyEntityFlag> destroyFlag,
                  RefRO<UnitDropComponent> unitDrop,
                  RefRO<LocalTransform> transform,
                  Entity entity) in
                 SystemAPI.Query<EnabledRefRO<DestroyEntityFlag>, RefRO<UnitDropComponent>, RefRO<LocalTransform>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            if (!destroyFlag.ValueRO || unitDrop.ValueRO.DropDataId < 0)
                continue;

            DropData dropData = DataComponent.Instance.Get<DropData>(unitDrop.ValueRO.DropDataId);
            if (dropData == null)
                continue;

            dropData.EnsureValid();
            Unity.Mathematics.Random random = CreateRandom(entity, transform.ValueRO.Position);
            for (int i = 0; i < dropData.Entries.Count; i++)
            {
                DropEntryData entry = dropData.Entries[i];
                if (entry == null || !IsValidEntry(entry))
                    continue;

                float chance = math.clamp(entry.Chance, 0f, 1f);
                if (chance <= 0f || random.NextFloat() > chance)
                    continue;

                int minQuantity = math.max(0, entry.MinQuantity);
                int maxQuantity = math.max(minQuantity, entry.MaxQuantity);
                int quantity = maxQuantity > minQuantity ? random.NextInt(minQuantity, maxQuantity + 1) : minQuantity;
                if (quantity <= 0)
                    continue;

                WorldDropSpawnUtility.TrySpawnDrop(EntityManager, entry.DropType, entry.ItemId, quantity, transform.ValueRO.Position);
            }
        }
    }

    private static bool IsValidEntry(DropEntryData entry)
    {
        if (entry == null)
            return false;

        if (entry.DropType == DropRewardType.Money)
            return true;

        return entry.ItemId >= 0;
    }

    private static Unity.Mathematics.Random CreateRandom(Entity entity, float3 position)
    {
        uint seed = math.hash(new int4( entity.Index, entity.Version, (int)math.round(position.x * 100f), (int)math.round(position.y * 100f)));
        if (seed == 0)
            seed = 1u;
        return new Unity.Mathematics.Random(seed);
    }
}
