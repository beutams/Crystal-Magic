using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
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
        BufferLookup<UnitQueryNode> nodes = SystemAPI.GetBufferLookup<UnitQueryNode>(true);
        BufferLookup<UnitQueryEntry> entries = SystemAPI.GetBufferLookup<UnitQueryEntry>(true);
        if (!nodes.TryGetBuffer(query.TreeEntity, out DynamicBuffer<UnitQueryNode> treeNodes) ||
            !entries.TryGetBuffer(query.TreeEntity, out DynamicBuffer<UnitQueryEntry> treeEntries))
        {
            return;
        }

        _sources.Update(ref state);
        state.Dependency = new SkillProjectileSimulationJob
        {
            Tree = new UnitQueryTree(treeNodes.AsNativeArray(), treeEntries.AsNativeArray()),
            Variables = SystemAPI.GetComponentLookup<UnitVariableComponent>(true),
            Sources = _sources,
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct SkillProjectileSimulationJob : IJobEntity
{
    [ReadOnly]
    public UnitQueryTree Tree;

    [ReadOnly]
    public ComponentLookup<UnitVariableComponent> Variables;

    [ReadOnly]
    public UnitSourceDispatcher Sources;

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

        ProjectileHitVisitor visitor = new()
        {
            ProjectileEntity = projectileEntity,
            ProjectilePosition = projectilePosition,
            Payload = payload,
            HitEntities = hitEntities,
            ConditionInstructions = instructions,
            ConditionLiterals = literals,
            Variables = Variables,
            Sources = Sources,
            BestDistanceSq = float.MaxValue,
        };
        UnitQueryShape shape = UnitQueryShape.Circle(projectilePosition, projectile.HitRadius);
        Tree.Query(in shape, UnitFactionMask.Combatants, ref visitor);
        hitEntity = visitor.HitEntity;
        hitPosition = visitor.HitPosition;
        return hitEntity != Entity.Null;
    }

    private struct ProjectileHitVisitor : IUnitQueryVisitor
    {
        public Entity ProjectileEntity;
        public float3 ProjectilePosition;
        public SkillProjectilePayloadComponent Payload;
        public DynamicBuffer<SkillProjectileHitEntityElement> HitEntities;

        [ReadOnly]
        public NativeArray<ExpressionInstruction> ConditionInstructions;

        [ReadOnly]
        public NativeArray<UnitSourceValue> ConditionLiterals;

        [ReadOnly]
        public ComponentLookup<UnitVariableComponent> Variables;

        [ReadOnly]
        public UnitSourceDispatcher Sources;

        public float BestDistanceSq;
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

            float distanceSq = math.lengthsq(entry.Position.xy - ProjectilePosition.xy);
            if (distanceSq > BestDistanceSq ||
                distanceSq == BestDistanceSq && !IsEntityBefore(entry.Entity, HitEntity))
            {
                return true;
            }

            BestDistanceSq = distanceSq;
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
            for (int index = 0; index < HitEntities.Length; index++)
            {
                if (HitEntities[index].Value == entity)
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
}
