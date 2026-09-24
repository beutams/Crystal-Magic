using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class SpawnProjectileEffect : Effect
    {
        public new SpawnProjectileEffectData Data { get; }

        public SpawnProjectileEffect(SpawnProjectileEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null)
                return;

            if (!TryGetSpawnPosition(context, out Vector3 spawnPosition))
                return;

            if (!TryGetProjectileDirection(context, spawnPosition, out Vector3 direction))
            {
                Debug.LogWarning($"[SpawnProjectileEffect] Skill {context.SourceSkillId} requires a target position different from its spawn position.");
                return;
            }

            Vector3 finalPosition = spawnPosition + direction * Data.SpawnOffsetDistance;
            SpawnProjectile(context, finalPosition, direction);
        }

        private bool TryGetSpawnPosition(SkillContent context, out Vector3 position)
        {
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                context.EntityManager.Exists(context.OriginEntity) &&
                context.EntityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                float3 entityPosition = context.EntityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
                position = new Vector3(entityPosition.x, entityPosition.y, entityPosition.z);
                return true;
            }

            if (context.HasPosition)
            {
                position = context.Position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static bool TryGetProjectileDirection(SkillContent context, Vector3 spawnPosition, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!context.HasPosition)
                return false;

            direction = context.Position - spawnPosition;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            direction.Normalize();
            return true;
        }

        private void SpawnProjectile(SkillContent context, Vector3 startPosition, Vector3 direction)
        {
            EntityManager entityManager = context.EntityManager;
            string projectileName = string.IsNullOrWhiteSpace(Data.ProjectilePrefabName)
                ? "Projectile"
                : Data.ProjectilePrefabName;
            NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                NetworkEntityPrefabType.Projectile,
                projectileName,
                startPosition);
            if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity projectileEntity))
            {
                Debug.LogError($"[SpawnProjectileEffect] Missing projectile prefab in registry: {projectileName}");
                return;
            }

            float3 position = new(startPosition.x, startPosition.y, startPosition.z);
            float3 projectileDirection = new(direction.x, direction.y, direction.z);
            quaternion rotation = CreateRotation(projectileDirection);
            SetOrAddComponentData(
                entityManager,
                projectileEntity,
                LocalTransform.FromPositionRotationScale(position, rotation, 1f));
            SetOrAddComponentData(entityManager, projectileEntity, new SkillProjectileComponent
            {
                Direction = math.normalizesafe(projectileDirection, new float3(1f, 0f, 0f)),
                Speed = Data.Speed,
                MaxRange = Data.MaxRange,
                TraveledDistance = 0f,
                HitRadius = math.max(Data.HitRadius, 0.01f),
                CanPierce = Data.CanPierce ? (byte)1 : (byte)0,
                TriggerDestroyEffectsOnMaxRange = Data.TriggerDestroyEffectsOnMaxRange ? (byte)1 : (byte)0,
                NetworkDirty = 1,
            });

            if (!entityManager.HasBuffer<SkillProjectileHitEntityElement>(projectileEntity))
                entityManager.AddBuffer<SkillProjectileHitEntityElement>(projectileEntity);
            else
                entityManager.GetBuffer<SkillProjectileHitEntityElement>(projectileEntity).Clear();

            GetOrAddBuffer<SkillProjectileConditionInstructionElement>(entityManager, projectileEntity).Clear();
            GetOrAddBuffer<SkillProjectileConditionLiteralElement>(entityManager, projectileEntity).Clear();
            SetOrAddComponentData(
                entityManager,
                projectileEntity,
                default(SkillProjectileFrameResultComponent));
            if (!entityManager.HasComponent<DestroyEntityFlag>(projectileEntity))
                entityManager.AddComponent<DestroyEntityFlag>(projectileEntity);
            entityManager.SetComponentEnabled<DestroyEntityFlag>(projectileEntity, false);

            ApplyPayloadComponent(entityManager, projectileEntity, context);
            SpawnProjectileVisual(entityManager, projectileEntity, context, position, rotation);
        }

        private void SpawnProjectileVisual(
            EntityManager entityManager,
            Entity projectileEntity,
            SkillContent context,
            float3 startPosition,
            quaternion rotation)
        {
            if (string.IsNullOrWhiteSpace(Data.VisualPrefabName))
                return;

            float3 offset = new(Data.VisualOffset.x, Data.VisualOffset.y, Data.VisualOffset.z);
            if (NetworkPresentationEventUtility.TryEnqueueFollowVfx(
                    entityManager,
                    Data.VisualPrefabName,
                    projectileEntity,
                    startPosition,
                    offset,
                    rotation,
                    Data.VisualScale,
                    0f,
                    true,
                    context.OriginEntity,
                    context.SourceSkillId))
            {
                return;
            }

            if (!SpriteEffectSpawnUtility.TrySpawn(
                    entityManager,
                    Data.VisualPrefabName,
                    startPosition,
                    rotation,
                    Data.VisualScale,
                    0f,
                    out Entity visualEntity))
            {
                return;
            }

            SpriteEffectSpawnUtility.SetOrAddComponentData(
                entityManager,
                visualEntity,
                new EffectVisualFollowComponent
                {
                    Target = projectileEntity,
                    Offset = offset,
                    AlignRotation = 1,
                    EndWhenTargetMissing = 1,
                });
            SetOrAddComponentData(
                entityManager,
                projectileEntity,
                new SkillProjectileVisualLinkComponent { VisualEntity = visualEntity });
        }

        private void ApplyPayloadComponent(
            EntityManager entityManager,
            Entity entity,
            SkillContent context)
        {
            if (entityManager.HasComponent<SkillProjectilePayloadComponent>(entity))
            {
                SkillProjectilePayloadComponent existing =
                    entityManager.GetComponentData<SkillProjectilePayloadComponent>(entity);
                EffectUtility.ReleaseAfterExecution(entityManager, existing.OnCollisionEffectListId);
                EffectUtility.ReleaseAfterExecution(entityManager, existing.OnDestroyEffectListId);
                if (existing.OwnsManagedContext != 0)
                {
                    EffectDataBridgeUtility.UnregisterManagedContext(
                        entityManager,
                        existing.Context.ManagedContextId);
                }
            }

            EffectRequestContext requestContext = EffectUtility.CaptureContext(
                entityManager,
                context,
                out bool ownsManagedContext);
            ConditionDataListId conditionsId = EffectDataBridgeUtility.RegisterConditions(
                entityManager,
                Data.CollisionTargetConditions);
            SkillProjectilePayloadComponent payload = new()
            {
                Context = requestContext,
                OwnsManagedContext = ownsManagedContext ? (byte)1 : (byte)0,
                OnCollisionEffectListId = EffectDataBridgeUtility.Register(
                    entityManager,
                    Data.OnCollisionEffects),
                OnDestroyEffectListId = EffectDataBridgeUtility.Register(
                    entityManager,
                    Data.OnDestroyEffects),
            };
            payload.CollisionConditionState = CompileCollisionConditions(
                entityManager,
                entity,
                conditionsId,
                in requestContext);
            SetOrAddComponentData(entityManager, entity, payload);
        }

        private static SkillProjectileConditionState CompileCollisionConditions(
            EntityManager entityManager,
            Entity entity,
            ConditionDataListId conditionsId,
            in EffectRequestContext requestContext)
        {
            DynamicBuffer<SkillProjectileConditionInstructionElement> instructions =
                GetOrAddBuffer<SkillProjectileConditionInstructionElement>(entityManager, entity);
            DynamicBuffer<SkillProjectileConditionLiteralElement> literals =
                GetOrAddBuffer<SkillProjectileConditionLiteralElement>(entityManager, entity);
            instructions.Clear();
            literals.Clear();

            if (!conditionsId.IsValid)
                return SkillProjectileConditionState.None;

            try
            {
                if (!EffectDataBridgeUtility.TryGetConditions(
                        entityManager,
                        conditionsId,
                        out List<ConditionConfig> conditions))
                {
                    return SkillProjectileConditionState.Invalid;
                }

                if (conditions.Count == 0)
                    return SkillProjectileConditionState.None;

                SkillContent context = EffectUtility.CreateContext(entityManager, in requestContext);
                Comparator comparator = EffectConditionUtility.BuildComparator(conditions, context);
                if (!comparator.IsValid)
                    return SkillProjectileConditionState.Invalid;

                ExpressionProgram program = comparator.Program;
                instructions.EnsureCapacity(program.Instructions.Length);
                literals.EnsureCapacity(program.Literals.Length);
                for (int index = 0; index < program.Instructions.Length; index++)
                {
                    instructions.Add(new SkillProjectileConditionInstructionElement
                    {
                        Value = program.Instructions[index],
                    });
                }

                for (int index = 0; index < program.Literals.Length; index++)
                {
                    literals.Add(new SkillProjectileConditionLiteralElement
                    {
                        Value = program.Literals[index],
                    });
                }

                return SkillProjectileConditionState.Valid;
            }
            finally
            {
                EffectDataBridgeUtility.UnregisterConditions(entityManager, conditionsId);
            }
        }

        private static quaternion CreateRotation(float3 direction)
        {
            float2 planar = math.normalizesafe(direction.xy, new float2(1f, 0f));
            return quaternion.RotateZ(math.atan2(planar.y, planar.x));
        }

        private static void SetOrAddComponentData<T>(
            EntityManager entityManager,
            Entity entity,
            T value)
            where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
                entityManager.SetComponentData(entity, value);
            else
                entityManager.AddComponentData(entity, value);
        }

        private static DynamicBuffer<T> GetOrAddBuffer<T>(EntityManager entityManager, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            return entityManager.HasBuffer<T>(entity)
                ? entityManager.GetBuffer<T>(entity)
                : entityManager.AddBuffer<T>(entity);
        }
    }
}
