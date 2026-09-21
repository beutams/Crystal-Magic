using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillReleaseSystem))]
[UpdateBefore(typeof(SkillProjectileSystem))]
public partial class SkillProjectileSpawnSystem : SystemBase
{
    private Entity _queueEntity;
    private readonly List<SkillProjectileSpawnRequest> _pendingRequests = new();

    protected override void OnCreate()
    {
        _queueEntity = SkillProjectileSpawnQueueUtility.GetOrCreateEntity(EntityManager);
    }

    protected override void OnUpdate()
    {
        DynamicBuffer<SkillProjectileSpawnRequest> requests =
            EntityManager.GetBuffer<SkillProjectileSpawnRequest>(_queueEntity);
        _pendingRequests.Clear();
        for (int i = 0; i < requests.Length; i++)
            _pendingRequests.Add(requests[i]);
        requests.Clear();

        for (int i = 0; i < _pendingRequests.Count; i++)
        {
            SkillProjectileSpawnRequest request = _pendingRequests[i];
            NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                NetworkEntityPrefabType.Projectile,
                request.ProjectileName.ToString(),
                new Vector3(request.StartPosition.x, request.StartPosition.y, request.StartPosition.z));
            if (!NetworkEntitySpawnUtility.TrySpawn(EntityManager, entityInfo, out Entity projectileEntity))
            {
                ReleaseFailedRequest(in request);
                Debug.LogError($"[SkillProjectileSpawnSystem] Missing projectile prefab in registry: {request.ProjectileName}");
                continue;
            }

            SpawnProjectile(projectileEntity, in request);
        }

        _pendingRequests.Clear();
    }

    private void SpawnProjectile(Entity projectileEntity, in SkillProjectileSpawnRequest request)
    {
        quaternion rotation = CreateRotation(request.Direction);

        SetOrAddComponentData(
            projectileEntity,
            LocalTransform.FromPositionRotationScale(request.StartPosition, rotation, 1f));
        SetOrAddComponentData(projectileEntity, new SkillProjectileComponent
        {
            Direction = math.normalizesafe(request.Direction, new float3(1f, 0f, 0f)),
            Speed = request.Speed,
            MaxRange = request.MaxRange,
            TraveledDistance = 0f,
            HitRadius = request.HitRadius,
            CanPierce = request.CanPierce,
            TriggerDestroyEffectsOnMaxRange = request.TriggerDestroyEffectsOnMaxRange,
            NetworkDirty = 1,
        });

        if (!EntityManager.HasBuffer<SkillProjectileHitEntityElement>(projectileEntity))
            EntityManager.AddBuffer<SkillProjectileHitEntityElement>(projectileEntity);
        else
            EntityManager.GetBuffer<SkillProjectileHitEntityElement>(projectileEntity).Clear();

        GetOrAddBuffer<SkillProjectileConditionInstructionElement>(projectileEntity).Clear();
        GetOrAddBuffer<SkillProjectileConditionLiteralElement>(projectileEntity).Clear();
        SetOrAddComponentData(projectileEntity, default(SkillProjectileFrameResultComponent));
        if (!EntityManager.HasComponent<DestroyEntityFlag>(projectileEntity))
            EntityManager.AddComponent<DestroyEntityFlag>(projectileEntity);
        EntityManager.SetComponentEnabled<DestroyEntityFlag>(projectileEntity, false);

        ApplyPayloadComponent(projectileEntity, in request);
        SpawnProjectileVisual(projectileEntity, in request, rotation);
    }

    private void SpawnProjectileVisual(
        Entity projectileEntity,
        in SkillProjectileSpawnRequest request,
        quaternion rotation)
    {
        if (request.VisualPrefabName.Length == 0 ||
            !SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                request.VisualPrefabName.ToString(),
                request.StartPosition,
                rotation,
                request.VisualScale,
                0f,
                out Entity visualEntity))
        {
            return;
        }

        SpriteEffectSpawnUtility.SetOrAddComponentData(
            EntityManager,
            visualEntity,
            new EffectVisualFollowComponent
            {
                Target = projectileEntity,
                Offset = request.VisualOffset,
                AlignRotation = 1,
                EndWhenTargetMissing = 1,
            });
        SetOrAddComponentData(
            projectileEntity,
            new SkillProjectileVisualLinkComponent { VisualEntity = visualEntity });
    }

    private void ApplyPayloadComponent(Entity entity, in SkillProjectileSpawnRequest request)
    {
        if (EntityManager.HasComponent<SkillProjectilePayloadComponent>(entity))
        {
            SkillProjectilePayloadComponent existing =
                EntityManager.GetComponentData<SkillProjectilePayloadComponent>(entity);
            EffectUtility.ReleaseAfterExecution(EntityManager, existing.OnCollisionEffectListId);
            EffectUtility.ReleaseAfterExecution(EntityManager, existing.OnDestroyEffectListId);
            if (existing.OwnsManagedContext != 0)
            {
                EffectDataBridgeUtility.UnregisterManagedContext(
                    EntityManager,
                    existing.Context.ManagedContextId);
            }
        }

        SkillProjectileConditionState conditionState =
            CompileCollisionConditions(entity, in request);

        SetOrAddComponentData(entity, new SkillProjectilePayloadComponent
        {
            Context = request.Context,
            OwnsManagedContext = request.ReleaseManagedContextOnFailure,
            CollisionConditionState = conditionState,
            OnCollisionEffectListId = request.OnCollisionEffectListId,
            OnDestroyEffectListId = request.OnDestroyEffectListId,
        });
    }

    private SkillProjectileConditionState CompileCollisionConditions(
        Entity entity,
        in SkillProjectileSpawnRequest request)
    {
        DynamicBuffer<SkillProjectileConditionInstructionElement> instructions =
            GetOrAddBuffer<SkillProjectileConditionInstructionElement>(entity);
        DynamicBuffer<SkillProjectileConditionLiteralElement> literals =
            GetOrAddBuffer<SkillProjectileConditionLiteralElement>(entity);
        instructions.Clear();
        literals.Clear();

        if (!request.CollisionTargetConditionsId.IsValid)
            return SkillProjectileConditionState.None;

        try
        {
            if (!EffectDataBridgeUtility.TryGetConditions(
                    EntityManager,
                    request.CollisionTargetConditionsId,
                    out List<ConditionConfig> conditions))
            {
                return SkillProjectileConditionState.Invalid;
            }

            if (conditions.Count == 0)
                return SkillProjectileConditionState.None;

            SkillContent context = EffectUtility.CreateContext(EntityManager, in request.Context);
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
            EffectDataBridgeUtility.UnregisterConditions(
                EntityManager,
                request.CollisionTargetConditionsId);
        }
    }

    private void ReleaseFailedRequest(in SkillProjectileSpawnRequest request)
    {
        EffectDataBridgeUtility.Unregister(EntityManager, request.OnCollisionEffectListId);
        EffectDataBridgeUtility.Unregister(EntityManager, request.OnDestroyEffectListId);
        EffectDataBridgeUtility.UnregisterConditions(
            EntityManager,
            request.CollisionTargetConditionsId);
        if (request.ReleaseManagedContextOnFailure != 0)
            EffectDataBridgeUtility.UnregisterManagedContext(EntityManager, request.Context.ManagedContextId);
    }

    private static quaternion CreateRotation(float3 direction)
    {
        float2 planar = math.normalizesafe(direction.xy, new float2(1f, 0f));
        float angle = math.atan2(planar.y, planar.x);
        return quaternion.RotateZ(angle);
    }

    private void SetOrAddComponentData<T>(Entity entity, T value)
        where T : unmanaged, IComponentData
    {
        if (EntityManager.HasComponent<T>(entity))
            EntityManager.SetComponentData(entity, value);
        else
            EntityManager.AddComponentData(entity, value);
    }

    private DynamicBuffer<T> GetOrAddBuffer<T>(Entity entity)
        where T : unmanaged, IBufferElementData
    {
        return EntityManager.HasBuffer<T>(entity)
            ? EntityManager.GetBuffer<T>(entity)
            : EntityManager.AddBuffer<T>(entity);
    }
}
