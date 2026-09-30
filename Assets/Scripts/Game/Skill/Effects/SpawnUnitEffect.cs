using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class SpawnUnitEffect : Effect
    {
        public new SpawnUnitEffectData Data { get; }

        public SpawnUnitEffect(SpawnUnitEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null)
                return;

            string[] candidates = GetCandidateUnitNames(Data);
            if (candidates.Length == 0)
                return;

            EntityManager entityManager = context.EntityManager;
            if (!TryGetCenter(context, entityManager, out float3 center))
                return;

            Vector3 offset = Data.CenterOffset;
            center += new float3(offset.x, offset.y, offset.z);

            float maxRadius = math.max(0f, Data.SpawnRadius);
            float minRadius = math.clamp(Data.MinSpawnRadius, 0f, maxRadius);
            var random = Unity.Mathematics.Random.CreateFromIndex((uint)UnityEngine.Random.Range(1, int.MaxValue));
            for (int i = 0; i < math.max(1, Data.Count); i++)
            {
                string selectedUnitName = candidates.Length == 1
                    ? candidates[0]
                    : candidates[UnityEngine.Random.Range(0, candidates.Length)];
                if (!SpawnPositionUtility.TrySample(entityManager, center, minRadius, maxRadius,
                        Data.ValidateSpawnPosition, Data.SpawnClearanceRadius, Data.SpawnValidationAttempts,
                        ref random, out float3 spawnPosition)) continue;
                NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                    NetworkEntityPrefabType.Unit,
                    selectedUnitName,
                    new Vector3(spawnPosition.x, spawnPosition.y, spawnPosition.z));
                if (Data.CopyFactionFromCaster &&
                    context.HasOriginEntity &&
                    entityManager.Exists(context.OriginEntity) &&
                    entityManager.HasComponent<UnitFactionComponent>(context.OriginEntity))
                {
                    entityInfo.hasFaction = true;
                    entityInfo.faction = entityManager.GetComponentData<UnitFactionComponent>(context.OriginEntity).Value;
                }

                if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity spawned))
                    continue;
                DungeonDifficultyUtility.Inherit(entityManager, context.OriginEntity, spawned);
                if (entityManager.HasComponent<DungeonDifficultyComponent>(spawned) &&
                    !entityManager.HasComponent<UnitSpawnInitializationComponent>(spawned))
                    entityManager.AddComponent<UnitSpawnInitializationComponent>(spawned);
            }
        }

        private static string[] GetCandidateUnitNames(SpawnUnitEffectData data)
        {
            if (data.CandidateUnitNames != null)
            {
                int validCount = 0;
                for (int i = 0; i < data.CandidateUnitNames.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(data.CandidateUnitNames[i]))
                        validCount++;
                }

                if (validCount > 0)
                {
                    string[] candidates = new string[validCount];
                    int writeIndex = 0;
                    for (int i = 0; i < data.CandidateUnitNames.Length; i++)
                    {
                        string candidate = data.CandidateUnitNames[i];
                        if (string.IsNullOrWhiteSpace(candidate))
                            continue;

                        candidates[writeIndex++] = candidate;
                    }

                    return candidates;
                }
            }

            return string.IsNullOrWhiteSpace(data.UnitName)
                ? System.Array.Empty<string>()
                : new[] { data.UnitName };
        }

        private static bool TryGetCenter(SkillContent context, EntityManager entityManager, out float3 center)
        {
            if (context.HasPosition)
            {
                Vector3 position = context.Position;
                center = new float3(position.x, position.y, position.z);
                return true;
            }

            if (context.HasOriginEntity &&
                entityManager.Exists(context.OriginEntity) &&
                entityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                center = entityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
                return true;
            }

            center = float3.zero;
            return false;
        }
    }
}
