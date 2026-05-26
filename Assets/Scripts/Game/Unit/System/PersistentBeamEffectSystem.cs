using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial class PersistentBeamEffectSystem : SystemBase
{
    private static ComparatorFactory s_comparatorFactory;
    private readonly List<PersistentBeamInstance> _instances = new();
    private readonly List<PersistentBeamInstance> _pendingInstances = new();
    private readonly List<UnitQueryHit> _hits = new();
    private bool _isUpdating;

    public static PersistentBeamEffectSystem Default =>
        World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<PersistentBeamEffectSystem>();

    public void AddEffect(PersistentBeamEffectData data, SkillContent sourceContext)
    {
        if (data == null || sourceContext == null)
            return;

        SkillContent context = sourceContext.Clone();
        context.EntityManager = EntityManager;

        if (TryCreateBeamContext(data, context, false, out SkillContent startContext))
            SkillExecutor.ExecuteEffects(data.OnStartEffects, startContext);

        bool hasTickEffects = data.OnHitEffects != null && data.OnHitEffects.Length > 0 && data.TotalDuration >= 0f;
        bool hasEndEffects = data.OnEndEffects != null && data.OnEndEffects.Length > 0;
        if (data.TotalDuration <= 0f && !hasEndEffects)
            return;

        PersistentBeamInstance instance = new()
        {
            Data = data,
            Context = context,
            TotalDuration = math.max(0f, data.TotalDuration),
            TickIntervalSeconds = math.max(0f, data.TickIntervalSeconds),
            NextTickTime = hasTickEffects ? 0f : float.MaxValue,
            HasTickEffects = hasTickEffects,
            HasEndEffects = hasEndEffects,
        };

        if (_isUpdating)
            _pendingInstances.Add(instance);
        else
            _instances.Add(instance);
    }

    protected override void OnUpdate()
    {
        AppendPendingInstances();

        float deltaTime = SystemAPI.Time.DeltaTime;
        _isUpdating = true;
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            PersistentBeamInstance instance = _instances[i];
            instance.Elapsed += deltaTime;

            if (instance.HasTickEffects)
            {
                while (instance.NextTickTime <= instance.Elapsed &&
                       instance.NextTickTime <= instance.TotalDuration)
                {
                    ExecuteBeamTick(instance);
                    if (instance.TickIntervalSeconds > 0f)
                        instance.NextTickTime += instance.TickIntervalSeconds;
                    else
                        instance.NextTickTime = float.MaxValue;
                }
            }

            if (instance.Elapsed >= instance.TotalDuration)
            {
                if (instance.HasEndEffects && TryCreateBeamContext(instance.Data, instance.Context, true, out SkillContent endContext))
                    SkillExecutor.ExecuteEffects(instance.Data.OnEndEffects, endContext);

                _instances.RemoveAt(i);
            }
        }
        _isUpdating = false;

        AppendPendingInstances();
    }

    private void ExecuteBeamTick(PersistentBeamInstance instance)
    {
        if (!TryGetBeamOriginAndFacing(instance.Data, instance.Context, out float3 origin, out float2 forward))
            return;

        if (!UnitQueryUtility.TryQueryForwardRect(EntityManager, origin, forward, instance.Data.Length, instance.Data.Width, _hits))
            return;

        for (int i = 0; i < _hits.Count; i++)
        {
            UnitQueryHit hit = _hits[i];
            if (!PassTargetConditions(
                    instance.Data.TargetConditions,
                    hit.Entity,
                    EntityManager,
                    instance.Context.OriginEntity,
                    instance.Context.HasOriginEntity))
            {
                continue;
            }

            Vector3 hitPosition = new(hit.Position.x, hit.Position.y, hit.Position.z);
            SkillContent targetContext = instance.Context.CloneForTarget(hit.Entity, hitPosition);
            targetContext.EntityManager = EntityManager;
            SkillExecutor.ExecuteEffects(instance.Data.OnHitEffects, targetContext);
        }
    }

    private bool TryCreateBeamContext(PersistentBeamEffectData data, SkillContent context, bool useBeamEnd, out SkillContent beamContext)
    {
        beamContext = null;
        if (!TryGetBeamOriginAndFacing(data, context, out float3 origin, out float2 forward))
            return false;

        float3 position = useBeamEnd
            ? origin + new float3(forward.x, forward.y, 0f) * data.Length
            : origin;

        beamContext = context.Clone();
        beamContext.EntityManager = EntityManager;
        beamContext.HasPosition = true;
        beamContext.Position = new Vector3(position.x, position.y, position.z);
        return true;
    }

    private static bool TryGetBeamOriginAndFacing(PersistentBeamEffectData data, SkillContent context, out float3 origin, out float2 forward)
    {
        EntityManager entityManager = context.EntityManager;
        if (context.HasOriginEntity &&
            context.OriginEntity != Entity.Null &&
            entityManager.Exists(context.OriginEntity) &&
            entityManager.HasComponent<LocalTransform>(context.OriginEntity))
        {
            origin = entityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
            if (!UnitFacingUtility.TryGetFacing(entityManager, context.OriginEntity, out forward))
            {
                if (context.HasPosition)
                {
                    float2 targetDirection = new float2(context.Position.x - origin.x, context.Position.y - origin.y);
                    forward = math.normalizesafe(targetDirection, new float2(1f, 0f));
                }
                else
                {
                    forward = new float2(1f, 0f);
                }
            }

            origin += new float3(forward.x, forward.y, 0f) * data.OriginOffsetDistance;
            return true;
        }

        if (context.HasPosition)
        {
            origin = new float3(context.Position.x, context.Position.y, context.Position.z);
            forward = new float2(1f, 0f);
            return true;
        }

        origin = float3.zero;
        forward = new float2(1f, 0f);
        return false;
    }

    private static bool PassTargetConditions(
        List<ConditionConfig> conditions,
        Entity target,
        EntityManager entityManager,
        Entity originEntity,
        bool hasOriginEntity)
    {
        if (conditions == null || conditions.Count == 0)
            return true;

        Comparator comparator = GetComparatorFactory().BuildComparator(
            conditions,
            target,
            entityManager,
            originEntity,
            hasOriginEntity);
        return comparator.GetResult();
    }

    private static ComparatorFactory GetComparatorFactory()
    {
        if (s_comparatorFactory != null)
            return s_comparatorFactory;

        s_comparatorFactory = new ComparatorFactory();
        ComparatorRegistry.RegisterAll(s_comparatorFactory);
        return s_comparatorFactory;
    }

    private void AppendPendingInstances()
    {
        if (_pendingInstances.Count == 0)
            return;

        _instances.AddRange(_pendingInstances);
        _pendingInstances.Clear();
    }

    private sealed class PersistentBeamInstance
    {
        public PersistentBeamEffectData Data;
        public SkillContent Context;
        public float TotalDuration;
        public float TickIntervalSeconds;
        public float Elapsed;
        public float NextTickTime;
        public bool HasTickEffects;
        public bool HasEndEffects;
    }
}
