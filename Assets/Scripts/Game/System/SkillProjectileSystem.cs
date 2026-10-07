using CrystalMagic.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SkillProjectileSimulationSystemGroup))]
public partial struct SkillProjectileSystem : ISystem
{
    private UnitSourceDispatcher _sources;
    private EntityQuery _terrainQuery;

    public void OnCreate(ref SystemState state)
    {
        _sources.InitializeReadOnly(ref state);
        _terrainQuery = state.GetEntityQuery(ComponentType.ReadOnly<DungeonNavigationMapComponent>(),
            ComponentType.ReadOnly<DungeonNavigationCollisionWord>());
        state.RequireForUpdate<UnitQuerySingleton>();
        state.RequireForUpdate<SkillProjectilePayloadComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        UnitQuerySingleton query = SystemAPI.GetSingleton<UnitQuerySingleton>();
        BufferLookup<UnitQueryNode> nodes = SystemAPI.GetBufferLookup<UnitQueryNode>(true);
        BufferLookup<UnitQueryEntry> entries = SystemAPI.GetBufferLookup<UnitQueryEntry>(true);
        if (!nodes.TryGetBuffer(query.TreeEntity, out DynamicBuffer<UnitQueryNode> treeNodes) ||
            !entries.TryGetBuffer(query.TreeEntity, out DynamicBuffer<UnitQueryEntry> treeEntries))
        {
            return;
        }

        _sources.Update(ref state);
        ComponentLookup<ClientPredictedProjectileComponent> predictions = SystemAPI.GetComponentLookup<ClientPredictedProjectileComponent>(true);
        JobHandle movementHandle = new SkillProjectileMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Predictions = predictions,
        }.ScheduleParallel(state.Dependency);
        Entity terrainEntity = _terrainQuery.IsEmptyIgnoreFilter ? Entity.Null : _terrainQuery.GetSingletonEntity();
        state.Dependency = new SkillProjectileCollisionJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Tree = new UnitQueryTree(treeNodes.AsNativeArray(), treeEntries.AsNativeArray()),
            Variables = SystemAPI.GetComponentLookup<UnitVariableComponent>(true),
            Sources = _sources,
            Predictions = predictions,
            TerrainEntity = terrainEntity,
            TerrainMap = terrainEntity == Entity.Null ? default : SystemAPI.GetComponent<DungeonNavigationMapComponent>(terrainEntity),
            TerrainWords = SystemAPI.GetBufferLookup<DungeonNavigationCollisionWord>(true),
        }.ScheduleParallel(movementHandle);
    }
}

[BurstCompile]
public partial struct SkillProjectileMovementJob : IJobEntity
{
    public float DeltaTime;
    [ReadOnly] public ComponentLookup<ClientPredictedProjectileComponent> Predictions;

    private void Execute(
        Entity entity, ref SkillProjectileComponent projectile,
        ref LocalTransform transform, in SkillProjectilePayloadComponent payload)
    {
        if (Predictions.TryGetComponent(entity, out ClientPredictedProjectileComponent prediction) && prediction.HasPredictedEnd != 0)
            return;
        projectile.PreviousPosition = transform.Position;
        float moveDistance = projectile.Speed * DeltaTime;
        if (projectile.MaxRange > 0f)
            moveDistance = math.min(moveDistance, math.max(0f, projectile.MaxRange - projectile.TraveledDistance));
        transform.Position += projectile.Direction * moveDistance;
        float2 planar = math.normalizesafe(projectile.Direction.xy, new float2(1f, 0f));
        transform.Rotation = quaternion.RotateZ(math.atan2(planar.y, planar.x));
        projectile.TraveledDistance += math.abs(moveDistance);
        projectile.NetworkDirty = 1;
    }
}

[BurstCompile]
public partial struct SkillProjectileCollisionJob : IJobEntity
{
    public float DeltaTime;
    public Entity TerrainEntity;
    public DungeonNavigationMapComponent TerrainMap;
    [ReadOnly] public BufferLookup<DungeonNavigationCollisionWord> TerrainWords;
    [ReadOnly] public ComponentLookup<ClientPredictedProjectileComponent> Predictions;

    [ReadOnly]
    public UnitQueryTree Tree;

    [ReadOnly]
    public ComponentLookup<UnitVariableComponent> Variables;

    [ReadOnly]
    public UnitSourceDispatcher Sources;

    private void Execute(
        Entity entity,
        in SkillProjectileComponent projectile,
        in LocalTransform transform,
        in SkillProjectilePayloadComponent payload,
        ref SkillProjectileFrameResultComponent frameResult,
        ref DynamicBuffer<SkillProjectileHitEntityElement> hitEntities,
        in DynamicBuffer<SkillProjectileConditionInstructionElement> conditionInstructions,
        in DynamicBuffer<SkillProjectileConditionLiteralElement> conditionLiterals)
    {
        frameResult = default;
        if (Predictions.TryGetComponent(entity, out ClientPredictedProjectileComponent prediction) && prediction.HasPredictedEnd != 0)
            return;

        if (projectile.RepeatHitIntervalSeconds > 0f)
            SkillProjectileHitHistory.Advance(ref hitEntities, DeltaTime);

        float terrainFraction = float.MaxValue;
        bool hitTerrain = TerrainEntity != Entity.Null &&
            TerrainWords.TryGetBuffer(TerrainEntity, out DynamicBuffer<DungeonNavigationCollisionWord> terrainWords) &&
            ProjectileTerrainCollisionUtility.TryCast(in TerrainMap, terrainWords.AsNativeArray(),
                projectile.PreviousPosition.xy, transform.Position.xy, projectile.HitRadius, out terrainFraction);

        if (TryFindHitEntity(
                entity,
                in projectile,
                in payload,
                ref hitEntities,
                in conditionInstructions,
                in conditionLiterals,
                transform.Position,
                terrainFraction,
                out Entity hitEntity,
                out float3 hitPosition))
        {
            SkillProjectileHitHistory.Record(ref hitEntities, hitEntity, projectile.RepeatHitIntervalSeconds);
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

        // Piercing applies to units, never to solid terrain. A piercing shot may
        // hit a unit before the wall in this frame, then end at the wall itself.
        if (hitTerrain)
        {
            frameResult.HasTerrainHit = 1;
            frameResult.TerrainHitPosition = math.lerp(projectile.PreviousPosition, transform.Position, terrainFraction);
            frameResult.ShouldDestroy = 1;
            frameResult.TriggerDestroyEffects = 1;
            return;
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
        float terrainFraction,
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

        ProjectileHitVisitor visitor = new()
        {
            ProjectileEntity = projectileEntity,
            ProjectilePosition = projectilePosition,
            PreviousPosition = projectile.PreviousPosition,
            HitRadius = projectile.HitRadius,
            Payload = payload,
            HitEntities = hitEntities,
            AllowRepeatHits = projectile.RepeatHitIntervalSeconds > 0f,
            ConditionInstructions = instructions,
            ConditionLiterals = literals,
            Variables = Variables,
            Sources = Sources,
            BestFraction = terrainFraction,
        };
        float3 queryEnd = math.lerp(projectile.PreviousPosition, projectilePosition, math.min(1f, terrainFraction));
        float3 center = (queryEnd + projectile.PreviousPosition) * 0.5f;
        UnitQueryShape shape = UnitQueryShape.Circle(center,
            math.distance(queryEnd.xy, projectile.PreviousPosition.xy) * 0.5f + projectile.HitRadius);
        Tree.Query(in shape, UnitFactionMask.Combatants, ref visitor);
        hitEntity = visitor.HitEntity;
        hitPosition = visitor.HitPosition;
        return hitEntity != Entity.Null;
    }

    private struct ProjectileHitVisitor : IUnitQueryVisitor
    {
        public Entity ProjectileEntity;
        public float3 ProjectilePosition;
        public float3 PreviousPosition;
        public float HitRadius;
        public SkillProjectilePayloadComponent Payload;
        public DynamicBuffer<SkillProjectileHitEntityElement> HitEntities;
        public bool AllowRepeatHits;

        [ReadOnly]
        public NativeArray<ExpressionInstruction> ConditionInstructions;

        [ReadOnly]
        public NativeArray<UnitSourceValue> ConditionLiterals;

        [ReadOnly]
        public ComponentLookup<UnitVariableComponent> Variables;

        [ReadOnly]
        public UnitSourceDispatcher Sources;

        public float BestFraction;
        public Entity HitEntity;
        public float3 HitPosition;

        public bool Visit(in UnitQueryEntry entry)
        {
            if (entry.Entity == ProjectileEntity ||
                Payload.Context.HasOriginEntity != 0 && entry.Entity == Payload.Context.OriginEntity ||
                HasHitEntity(entry.Entity) ||
                !PassesConditions(entry.Entity))
            {
                return true;
            }

            if (!ProjectileTerrainCollisionUtility.TryHitCircle(PreviousPosition.xy, ProjectilePosition.xy,
                    entry.Position.xy, HitRadius, out float fraction))
                return true;
            if (fraction > BestFraction ||
                fraction == BestFraction && (HitEntity == Entity.Null || !IsEntityBefore(entry.Entity, HitEntity)))
            {
                return true;
            }

            BestFraction = fraction;
            HitEntity = entry.Entity;
            HitPosition = new float3(entry.Position.x, entry.Position.y, ProjectilePosition.z);
            return true;
        }

        private bool PassesConditions(Entity evaluatedEntity)
        {
            if (Payload.CollisionConditionState == SkillProjectileConditionState.None)
                return true;

            Entity other = Payload.Context.HasOtherEntity != 0
                ? Payload.Context.OtherEntity
                : Variables.TryGetComponent(evaluatedEntity, out UnitVariableComponent variables)
                    ? variables.Other
                    : Entity.Null;
            UnitSourceContext context = new(evaluatedEntity, other);
            return CompiledExpressionEvaluator.TryEvaluateConditions(
                ConditionInstructions,
                ConditionLiterals,
                in context,
                in Sources);
        }

        private bool HasHitEntity(Entity entity)
        {
            return SkillProjectileHitHistory.Blocks(in HitEntities, entity, AllowRepeatHits);
        }

        private static bool IsEntityBefore(Entity candidate, Entity current)
        {
            return current == Entity.Null ||
                   candidate.Index < current.Index ||
                   candidate.Index == current.Index && candidate.Version < current.Version;
        }
    }
}
