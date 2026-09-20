using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSpawnSystem))]
public partial struct SkillProjectileSystem : ISystem
{
    private UnitSourceDispatcher _sources;

    public void OnCreate(ref SystemState state)
    {
        _sources.InitializeReadOnly(ref state);
        state.RequireForUpdate<UnitQuerySingleton>();
        state.RequireForUpdate<SkillProjectilePayloadComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        UnitQuerySingleton query = SystemAPI.GetSingleton<UnitQuerySingleton>();
        BufferLookup<UnitQueryEntry> grids = SystemAPI.GetBufferLookup<UnitQueryEntry>(true);
        if (!grids.TryGetBuffer(
                query.UnitGridEntity,
                out DynamicBuffer<UnitQueryEntry> unitEntries))
        {
            return;
        }

        _sources.Update(ref state);
        state.Dependency = new SkillProjectileSimulationJob
        {
            UnitEntries = unitEntries.AsNativeArray(),
            Variables = SystemAPI.GetComponentLookup<UnitVariableComponent>(true),
            Sources = _sources,
            InverseCellSize = query.InverseCellSize,
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct SkillProjectileSimulationJob : IJobEntity
{
    [ReadOnly]
    public NativeArray<UnitQueryEntry> UnitEntries;

    [ReadOnly]
    public ComponentLookup<UnitVariableComponent> Variables;

    [ReadOnly]
    public UnitSourceDispatcher Sources;

    public float InverseCellSize;
    public float DeltaTime;

    private void Execute(
        Entity entity,
        ref SkillProjectileComponent projectile,
        ref LocalTransform transform,
        in SkillProjectilePayloadComponent payload,
        ref SkillProjectileFrameResultComponent frameResult,
        ref DynamicBuffer<SkillProjectileHitEntityElement> hitEntities,
        in DynamicBuffer<SkillProjectileConditionInstructionElement> conditionInstructions,
        in DynamicBuffer<SkillProjectileConditionLiteralElement> conditionLiterals)
    {
        frameResult = default;

        float moveDistance = projectile.Speed * DeltaTime;
        transform.Position += projectile.Direction * moveDistance;
        float2 planar = math.normalizesafe(projectile.Direction.xy, new float2(1f, 0f));
        transform.Rotation = quaternion.RotateZ(math.atan2(planar.y, planar.x));
        projectile.TraveledDistance += math.abs(moveDistance);
        projectile.NetworkDirty = 1;

        if (TryFindHitEntity(
                entity,
                in projectile,
                in payload,
                ref hitEntities,
                in conditionInstructions,
                in conditionLiterals,
                transform.Position,
                out Entity hitEntity,
                out float3 hitPosition))
        {
            hitEntities.Add(new SkillProjectileHitEntityElement { Value = hitEntity });
            frameResult.HitEntity = hitEntity;
            frameResult.HitPosition = hitPosition;
            frameResult.HasHit = 1;

            if (projectile.CanPierce == 0)
            {
                frameResult.ShouldDestroy = 1;
                frameResult.TriggerDestroyEffects = 1;
                frameResult.DestroyUsesHitContext = 1;
                return;
            }
        }

        if (projectile.MaxRange > 0f && projectile.TraveledDistance >= projectile.MaxRange)
        {
            frameResult.ShouldDestroy = 1;
            frameResult.TriggerDestroyEffects = projectile.TriggerDestroyEffectsOnMaxRange;
        }
    }

    private bool TryFindHitEntity(
        Entity projectileEntity,
        in SkillProjectileComponent projectile,
        in SkillProjectilePayloadComponent payload,
        ref DynamicBuffer<SkillProjectileHitEntityElement> hitEntities,
        in DynamicBuffer<SkillProjectileConditionInstructionElement> conditionInstructions,
        in DynamicBuffer<SkillProjectileConditionLiteralElement> conditionLiterals,
        float3 projectilePosition,
        out Entity hitEntity,
        out float3 hitPosition)
    {
        hitEntity = Entity.Null;
        hitPosition = float3.zero;
        if (projectile.HitRadius <= 0f ||
            payload.CollisionConditionState == SkillProjectileConditionState.Invalid)
        {
            return false;
        }

        NativeArray<ExpressionInstruction> instructions =
            conditionInstructions.Reinterpret<ExpressionInstruction>().AsNativeArray();
        NativeArray<UnitSourceValue> literals =
            conditionLiterals.Reinterpret<UnitSourceValue>().AsNativeArray();

        float2 center = projectilePosition.xy;
        float radiusSq = projectile.HitRadius * projectile.HitRadius;
        int2 minCell = UnitQueryGrid.GetCell(center - projectile.HitRadius, InverseCellSize);
        int2 maxCell = UnitQueryGrid.GetCell(center + projectile.HitRadius, InverseCellSize);
        float bestDistanceSq = float.MaxValue;

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                FindBestHitInCell(
                    projectileEntity,
                    UnitQueryGrid.GetCellKey(new int2(x, y)),
                    center,
                    projectilePosition.z,
                    radiusSq,
                    in payload,
                    ref hitEntities,
                    instructions,
                    literals,
                    ref bestDistanceSq,
                    ref hitEntity,
                    ref hitPosition);
            }
        }

        return hitEntity != Entity.Null;
    }

    private void FindBestHitInCell(
        Entity projectileEntity,
        long cellKey,
        float2 center,
        float projectileZ,
        float radiusSq,
        in SkillProjectilePayloadComponent payload,
        ref DynamicBuffer<SkillProjectileHitEntityElement> hitEntities,
        NativeArray<ExpressionInstruction> conditionInstructions,
        NativeArray<UnitSourceValue> conditionLiterals,
        ref float bestDistanceSq,
        ref Entity hitEntity,
        ref float3 hitPosition)
    {
        if (!UnitQueryGrid.TryGetCellRange(
                UnitEntries,
                cellKey,
                out int startIndex,
                out int endIndex))
        {
            return;
        }

        for (int index = startIndex; index < endIndex; index++)
        {
            UnitQueryEntry entry = UnitEntries[index];
            if (entry.Entity == projectileEntity ||
                payload.Context.HasOriginEntity != 0 && entry.Entity == payload.Context.OriginEntity ||
                HasHitEntity(in hitEntities, entry.Entity))
            {
                continue;
            }

            float distanceSq = math.lengthsq(entry.Position.xy - center);
            if (distanceSq > radiusSq ||
                !PassesConditions(
                    entry.Entity,
                    in payload,
                    conditionInstructions,
                    conditionLiterals))
            {
                continue;
            }

            if (distanceSq > bestDistanceSq ||
                distanceSq == bestDistanceSq && !IsEntityBefore(entry.Entity, hitEntity))
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            hitEntity = entry.Entity;
            hitPosition = new float3(entry.Position.x, entry.Position.y, projectileZ);
        }
    }

    private bool PassesConditions(
        Entity evaluatedEntity,
        in SkillProjectilePayloadComponent payload,
        NativeArray<ExpressionInstruction> conditionInstructions,
        NativeArray<UnitSourceValue> conditionLiterals)
    {
        if (payload.CollisionConditionState == SkillProjectileConditionState.None)
            return true;

        Entity other = payload.Context.HasOtherEntity != 0
            ? payload.Context.OtherEntity
            : Variables.TryGetComponent(evaluatedEntity, out UnitVariableComponent variables)
                ? variables.Other
                : Entity.Null;
        UnitSourceContext context = new(evaluatedEntity, other);
        return CompiledExpressionEvaluator.TryEvaluateConditions(
            conditionInstructions,
            conditionLiterals,
            in context,
            in Sources);
    }

    private static bool HasHitEntity(
        in DynamicBuffer<SkillProjectileHitEntityElement> hitEntities,
        Entity entity)
    {
        for (int index = 0; index < hitEntities.Length; index++)
        {
            if (hitEntities[index].Value == entity)
                return true;
        }

        return false;
    }

    private static bool IsEntityBefore(Entity candidate, Entity current)
    {
        return current == Entity.Null ||
               candidate.Index < current.Index ||
               candidate.Index == current.Index && candidate.Version < current.Version;
    }
}
