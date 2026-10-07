using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
public partial struct DungeonTreasureRewardSystem : ISystem
{
    private const float MinScatterRadius = 0.35f;
    private const float MaxScatterRadius = 1.35f;
    private const float MinScatterDuration = 0.35f;
    private const float MaxScatterDuration = 0.65f;
    private const float MinArcHeight = 0.4f;
    private const float MaxArcHeight = 0.85f;

    private EntityQuery _treasureQuery;

    public void OnCreate(ref SystemState state)
    {
        _treasureQuery = state.GetEntityQuery(
            ComponentType.ReadWrite<TreasureComponent>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<DungeonTreasureCandidateItemElement>());
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton(out BattleSimulationScope scope) && scope.Pass == BattleSimulationPass.Players)
            return;
        DungeonConfig config = ConfigComponent.Instance.Get<DungeonConfig>();
        config.EnsureValid();
        EntityManager entityManager = state.EntityManager;

        using NativeArray<Entity> entities = _treasureQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities[entityIndex];
            if (!entityManager.Exists(entity))
                continue;

            TreasureComponent treasure = entityManager.GetComponentData<TreasureComponent>(entity);
            if (treasure.IsOpened == 0 || treasure.RewardsSpawned != 0)
                continue;

            DynamicBuffer<DungeonTreasureCandidateItemElement> candidates =
                entityManager.GetBuffer<DungeonTreasureCandidateItemElement>(entity);
            if (candidates.Length == 0)
            {
                MarkRewardsSpawned(entityManager, entity, treasure);
                continue;
            }

            if (!WorldDropSpawnUtility.CanSpawnDrop(entityManager))
                continue;

            Unity.Mathematics.Random random = new(treasure.RandomSeed == 0 ? 1u : treasure.RandomSeed);
            Vector2 luckRange = config.GetChestLuckRange(treasure.Quality);
            float luck = luckRange.y > luckRange.x
                ? random.NextFloat(luckRange.x, luckRange.y)
                : luckRange.x;
            int rewardCount = config.ChestRewardCountRange.y > config.ChestRewardCountRange.x
                ? random.NextInt(config.ChestRewardCountRange.x, config.ChestRewardCountRange.y + 1)
                : config.ChestRewardCountRange.x;

            // Spawning a drop performs structural changes. Resolve every reward while the
            // candidate buffer is still valid, then use the independent snapshot below.
            NativeArray<int> selectedItemIds = new(rewardCount, Allocator.Temp);
            try
            {
                for (int rewardIndex = 0; rewardIndex < rewardCount; rewardIndex++)
                    selectedItemIds[rewardIndex] = SelectCandidate(candidates, config, luck, ref random);

                float3 startPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;
                for (int rewardIndex = 0; rewardIndex < rewardCount; rewardIndex++)
                {
                    int selectedItemId = selectedItemIds[rewardIndex];
                    if (selectedItemId < 0)
                        continue;

                    float angle = random.NextFloat(0f, math.PI * 2f);
                    math.sincos(angle, out float sin, out float cos);
                    float radius = math.lerp(MinScatterRadius, MaxScatterRadius, math.sqrt(random.NextFloat()));
                    float3 targetPosition = startPosition + new float3(cos * radius, sin * radius, 0f);
                    float duration = random.NextFloat(MinScatterDuration, MaxScatterDuration);
                    float arcHeight = random.NextFloat(MinArcHeight, MaxArcHeight);
                    WorldDropSpawnUtility.TrySpawnScatteredDrop(
                        entityManager,
                        DropRewardType.Item,
                        selectedItemId,
                        1,
                        startPosition,
                        targetPosition,
                        duration,
                        arcHeight);
                }
            }
            finally
            {
                selectedItemIds.Dispose();
            }

            MarkRewardsSpawned(entityManager, entity, treasure);
        }
    }

    private static int SelectCandidate(
        in DynamicBuffer<DungeonTreasureCandidateItemElement> candidates,
        DungeonConfig config,
        float luck,
        ref Unity.Mathematics.Random random)
    {
        float totalWeight = 0f;
        for (int index = 0; index < candidates.Length; index++)
        {
            int itemId = candidates[index].ItemId;
            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData != null)
                totalWeight += config.GetChestRarityWeight(itemData.Rarity, luck);
        }

        if (totalWeight <= 0f)
            return -1;

        float roll = random.NextFloat(0f, totalWeight);
        int fallbackItemId = -1;
        for (int index = 0; index < candidates.Length; index++)
        {
            int itemId = candidates[index].ItemId;
            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData == null)
                continue;

            float weight = config.GetChestRarityWeight(itemData.Rarity, luck);
            if (weight <= 0f)
                continue;

            fallbackItemId = itemId;
            roll -= weight;
            if (roll < 0f)
                return itemId;
        }

        return fallbackItemId;
    }

    private static void MarkRewardsSpawned(
        EntityManager entityManager,
        Entity entity,
        TreasureComponent treasure)
    {
        treasure.RewardsSpawned = 1;
        entityManager.SetComponentData(entity, treasure);
    }
}
