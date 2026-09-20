using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSystem))]
partial class PersistentEffectSystem : SystemBase
{
    private readonly List<PersistentEffectInstance> _instances = new();
    private readonly List<PersistentEffectInstance> _pendingInstances = new();
    private bool _isUpdating;
    private Entity _queueEntity;

    protected override void OnCreate()
    {
        _queueEntity = PersistentEffectUtility.GetOrCreateEntity(EntityManager);
        RequireForUpdate<PersistentEffectQueueComponent>();
    }

    protected override void OnUpdate()
    {
        ConsumePendingRequests();
        AppendPendingInstances();

        float deltaTime = SystemAPI.Time.DeltaTime;
        _isUpdating = true;
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            PersistentEffectInstance instance = _instances[i];
            instance.Elapsed += deltaTime;

            while (instance.NextTickTime <= instance.Elapsed &&
                   instance.NextTickTime <= instance.TotalDuration)
            {
                SkillContent tickContext = instance.Context.Clone();
                tickContext.EntityManager = EntityManager;
                tickContext.PersistentEffectAppliedBuffTargets = instance.AppliedBuffTargets;
                EffectUtility.Enqueue(EntityManager, instance.OnTickEffectListId, tickContext);
                instance.NextTickTime += instance.TickIntervalSeconds;
            }

            if (instance.Elapsed >= instance.TotalDuration)
            {
                if (instance.OnEndEffectListId.IsValid)
                {
                    SkillContent endContext = instance.Context.Clone();
                    endContext.EntityManager = EntityManager;
                    EffectUtility.Enqueue(EntityManager, instance.OnEndEffectListId, endContext);
                }

                EffectUtility.ReleaseAfterExecution(EntityManager, instance.OnTickEffectListId);
                EffectUtility.ReleaseAfterExecution(EntityManager, instance.OnEndEffectListId);

                _instances.RemoveAt(i);
            }
        }
        _isUpdating = false;

        AppendPendingInstances();
    }

    private void ConsumePendingRequests()
    {
        DynamicBuffer<PersistentEffectRequest> requests =
            EntityManager.GetBuffer<PersistentEffectRequest>(_queueEntity);
        if (requests.IsEmpty)
            return;

        for (int i = 0; i < requests.Length; i++)
        {
            PersistentEffectRequest request = requests[i];
            if (EffectDataBridgeUtility.TryGet(
                    EntityManager,
                    request.PersistentDataId,
                    out EffectDataList dataList) &&
                dataList.Effects.Length > 0 &&
                dataList.Effects[0] is PersistentEffectData data)
            {
                SkillContent sourceContext = EffectUtility.CreateContext(
                    EntityManager,
                    in request.SourceContext);
                AddEffectInternal(
                    data,
                    sourceContext,
                    new Vector3(
                        request.ReleasePosition.x,
                        request.ReleasePosition.y,
                        request.ReleasePosition.z));
            }

            EffectDataBridgeUtility.Unregister(EntityManager, request.PersistentDataId);
            if (request.ReleaseManagedContextAfterConsumption != 0)
            {
                EffectDataBridgeUtility.UnregisterManagedContext(
                    EntityManager,
                    request.SourceContext.ManagedContextId);
            }
        }

        requests.Clear();
    }

    private void AddEffectInternal(PersistentEffectData data, SkillContent sourceContext, Vector3 releasePosition)
    {
        SkillContent context = sourceContext.Clone();
        context.EntityManager = EntityManager;
        context.HasPosition = true;
        context.Position = releasePosition;
        context.HasTargetEntity = false;
        context.TargetEntity = Entity.Null;

        EffectUtility.Enqueue(EntityManager, data.OnStartEffects, context);

        bool hasTickEffects = data.TickIntervalSeconds > 0f && data.OnTickEffects != null && data.OnTickEffects.Length > 0;
        bool hasEndEffects = data.OnEndEffects != null && data.OnEndEffects.Length > 0;
        if (data.TotalDuration <= 0f || (!hasTickEffects && !hasEndEffects))
            return;

        PersistentEffectInstance instance = new()
        {
            TotalDuration = data.TotalDuration,
            TickIntervalSeconds = data.TickIntervalSeconds,
            NextTickTime = hasTickEffects ? data.TickIntervalSeconds : float.MaxValue,
            Context = context,
            OnTickEffectListId = hasTickEffects
                ? EffectDataBridgeUtility.Register(EntityManager, data.OnTickEffects)
                : default,
            OnEndEffectListId = hasEndEffects
                ? EffectDataBridgeUtility.Register(EntityManager, data.OnEndEffects)
                : default,
        };

        if (_isUpdating)
            _pendingInstances.Add(instance);
        else
            _instances.Add(instance);
    }

    private void AppendPendingInstances()
    {
        if (_pendingInstances.Count == 0)
            return;

        _instances.AddRange(_pendingInstances);
        _pendingInstances.Clear();
    }

    private sealed class PersistentEffectInstance
    {
        public float TotalDuration;
        public float TickIntervalSeconds;
        public float Elapsed;
        public float NextTickTime;
        public SkillContent Context;
        public EffectDataListId OnTickEffectListId;
        public EffectDataListId OnEndEffectListId;
        public Dictionary<int, HashSet<Entity>> AppliedBuffTargets = new();
    }
}
