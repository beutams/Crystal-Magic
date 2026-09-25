using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup))]
[UpdateAfter(typeof(UnitDeathFinalizeSystem))]
partial class UnitDropOnDestroySystem : SystemBase
{
    private const float MinScatterRadius = 0.35f;
    private const float MaxScatterRadius = 1.35f;
    private const float MinScatterDuration = 0.35f;
    private const float MaxScatterDuration = 0.65f;
    private const float MinArcHeight = 0.4f;
    private const float MaxArcHeight = 0.85f;

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

                float3 startPosition = transform.ValueRO.Position;
                float angle = random.NextFloat(0f, math.PI * 2f);
                math.sincos(angle, out float sin, out float cos);
                float radius = math.lerp(MinScatterRadius, MaxScatterRadius, math.sqrt(random.NextFloat()));
                float3 targetPosition = startPosition + new float3(cos * radius, sin * radius, 0f);
                float duration = random.NextFloat(MinScatterDuration, MaxScatterDuration);
                float arcHeight = random.NextFloat(MinArcHeight, MaxArcHeight);
                WorldDropSpawnUtility.TrySpawnScatteredDrop(
                    EntityManager,
                    entry.DropType,
                    entry.ItemId,
                    quantity,
                    startPosition,
                    targetPosition,
                    duration,
                    arcHeight);
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
