using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrystalMagic.Game.Skill.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SkillExecutionSystem))]
    [UpdateBefore(typeof(SkillProjectileSystem))]
    public partial class SkillProjectileSpawnSystem : SystemBase
    {
        private EntityQuery _requestQuery;
        private readonly Dictionary<int, ProjectileRenderAsset> _renderAssetCache = new();

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(
                ComponentType.ReadOnly<SkillProjectileSpawnRequestComponent>(),
                ComponentType.ReadOnly<SkillProjectilePayloadComponent>());
        }

        protected override void OnUpdate()
        {
            if (_requestQuery.IsEmptyIgnoreFilter)
                return;

            NativeArray<Entity> requestEntities = _requestQuery.ToEntityArray(Allocator.Temp);
            NativeArray<SkillProjectileSpawnRequestComponent> requests = _requestQuery.ToComponentDataArray<SkillProjectileSpawnRequestComponent>(Allocator.Temp);

            try
            {
                for (int i = 0; i < requestEntities.Length; i++)
                {
                    Entity requestEntity = requestEntities[i];
                    SkillProjectileSpawnRequestComponent request = requests[i];
                    SkillProjectilePayloadComponent payload = EntityManager.GetComponentObject<SkillProjectilePayloadComponent>(requestEntity);

                    if (!TryResolveRenderAsset(payload, out ProjectileRenderAsset renderAsset))
                    {
                        if (EntityManager.Exists(requestEntity))
                            EntityManager.DestroyEntity(requestEntity);
                        continue;
                    }

                    SpawnProjectile(request, payload, renderAsset);

                    if (EntityManager.Exists(requestEntity))
                        EntityManager.DestroyEntity(requestEntity);
                }
            }
            finally
            {
                requestEntities.Dispose();
                requests.Dispose();
            }
        }

        private void SpawnProjectile(
            SkillProjectileSpawnRequestComponent request,
            SkillProjectilePayloadComponent payload,
            ProjectileRenderAsset renderAsset)
        {
            Entity projectileEntity = EntityManager.CreateEntity();
            quaternion rotation = CreateRotation(request.Direction);
            float scale = math.max(0.0001f, renderAsset.BaseUniformScale * request.ScaleMultiplier);

            EntityManager.AddComponentData(
                projectileEntity,
                LocalTransform.FromPositionRotationScale(
                    request.StartPosition,
                    rotation,
                    scale));

            RenderMeshUtility.AddComponents(
                projectileEntity,
                EntityManager,
                renderAsset.RenderDescription,
                renderAsset.RenderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            EntityManager.AddComponentData(
                projectileEntity,
                new SkillProjectileComponent
                {
                    Direction = math.normalizesafe(request.Direction, new float3(1f, 0f, 0f)),
                    Speed = request.Speed,
                    MaxRange = request.MaxRange,
                    TraveledDistance = 0f,
                    HitRadius = request.HitRadius,
                    Scale = scale,
                    CanPierce = request.CanPierce,
                    TriggerDestroyEffectsOnMaxRange = request.TriggerDestroyEffectsOnMaxRange,
                });

            EntityManager.AddComponentData(
                projectileEntity,
                new SkillProjectileStartTimeProperty
                {
                    Value = (float)SystemAPI.Time.ElapsedTime,
                });

            EntityManager.AddBuffer<SkillProjectileHitEntityElement>(projectileEntity);
            EntityManager.AddComponentObject(
                projectileEntity,
                new SkillProjectilePayloadComponent
                {
                    ProjectilePrefab = payload.ProjectilePrefab,
                    Context = payload.Context.Clone(),
                    OnCollisionEffects = payload.OnCollisionEffects,
                    OnDestroyEffects = payload.OnDestroyEffects,
                });
        }

        private bool TryResolveRenderAsset(SkillProjectilePayloadComponent payload, out ProjectileRenderAsset asset)
        {
            asset = default;
            if (payload?.ProjectilePrefab == null)
                return false;

            int prefabId = payload.ProjectilePrefab.GetInstanceID();
            if (_renderAssetCache.TryGetValue(prefabId, out asset))
                return true;

            MeshFilter meshFilter = payload.ProjectilePrefab.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = payload.ProjectilePrefab.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                Debug.LogError($"[SkillProjectileSpawnSystem] Invalid projectile prefab: {payload.ProjectilePrefab.name}");
                return false;
            }

            Material material = meshRenderer.sharedMaterial;
            if (!material.enableInstancing)
                material.enableInstancing = true;

            asset = new ProjectileRenderAsset
            {
                RenderDescription = new RenderMeshDescription(meshRenderer),
                RenderMeshArray = new RenderMeshArray(new[] { material }, new[] { meshFilter.sharedMesh }),
                BaseUniformScale = ResolveUniformScale(payload.ProjectilePrefab.transform.localScale),
            };

            _renderAssetCache[prefabId] = asset;
            return true;
        }

        private static float ResolveUniformScale(Vector3 localScale)
        {
            float x = math.abs(localScale.x);
            float y = math.abs(localScale.y);
            float z = math.abs(localScale.z);
            return math.max(0.0001f, math.max(x, math.max(y, z)));
        }

        private static quaternion CreateRotation(float3 direction)
        {
            float2 planar = math.normalizesafe(direction.xy, new float2(1f, 0f));
            float angle = math.atan2(planar.y, planar.x);
            return quaternion.RotateZ(angle);
        }

        private struct ProjectileRenderAsset
        {
            public RenderMeshDescription RenderDescription;
            public RenderMeshArray RenderMeshArray;
            public float BaseUniformScale;
        }
    }
}
