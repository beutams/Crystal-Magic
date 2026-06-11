using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Unit;
using Unity.Collections;
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
            for (int i = 0; i < math.max(1, Data.Count); i++)
            {
                string selectedUnitName = candidates.Length == 1
                    ? candidates[0]
                    : candidates[UnityEngine.Random.Range(0, candidates.Length)];
                FixedString128Bytes unitName = new(selectedUnitName);
                if (!EntitySpawnRegistryUtility.TryInstantiateUnit(entityManager, unitName, out Entity instance))
                    continue;

                if (entityManager.HasComponent<LocalTransform>(instance))
                {
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(instance);
                    Vector2 direction = UnityEngine.Random.insideUnitCircle;
                    if (direction.sqrMagnitude <= 0.0001f)
                        direction = Vector2.right;

                    float radius = Mathf.Sqrt(UnityEngine.Random.Range(minRadius * minRadius, maxRadius * maxRadius));
                    transform.Position = new float3(center.x + direction.x * radius, center.y + direction.y * radius, center.z);
                    transform.Rotation = quaternion.identity;
                    entityManager.SetComponentData(instance, transform);
                }

                if (Data.CopyFactionFromCaster &&
                    context.HasOriginEntity &&
                    entityManager.Exists(context.OriginEntity) &&
                    entityManager.HasComponent<UnitFactionComponent>(context.OriginEntity) &&
                    entityManager.HasComponent<UnitFactionComponent>(instance))
                {
                    entityManager.SetComponentData(instance, entityManager.GetComponentData<UnitFactionComponent>(context.OriginEntity));
                }
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
