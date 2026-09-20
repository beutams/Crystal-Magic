using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(PersistentEffectSystem))]
partial class EffectExecutionSystem : SystemBase
{
    private EntityQuery _effectRequestQuery;
    private Entity _worldQueueEntity;
    private readonly List<EffectEntry> _pendingEntries = new();

    protected override void OnCreate()
    {
        _worldQueueEntity = EffectUtility.GetOrCreateEntity(EntityManager);
        EffectDataBridgeUtility.GetOrCreate(EntityManager);
        _effectRequestQuery = GetEntityQuery(ComponentType.ReadWrite<EffectEntry>());
        RequireForUpdate<EffectComponent>();
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _effectRequestQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities[entityIndex];
            if (entity == _worldQueueEntity)
                continue;

            DrainAndExecute(entity);
        }

        // Managed effects may enqueue follow-up effects. Reacquiring the buffer after each
        // batch keeps it valid even when an effect performs structural changes.
        while (EntityManager.GetBuffer<EffectEntry>(_worldQueueEntity, true).Length > 0)
            DrainAndExecute(_worldQueueEntity);

        DynamicBuffer<EffectReleaseEntry> releases =
            EntityManager.GetBuffer<EffectReleaseEntry>(_worldQueueEntity);
        for (int i = 0; i < releases.Length; i++)
            EffectDataBridgeUtility.Unregister(EntityManager, releases[i].EffectListId);
        releases.Clear();
    }

    private void DrainAndExecute(Entity queueEntity)
    {
        DynamicBuffer<EffectEntry> requests = EntityManager.GetBuffer<EffectEntry>(queueEntity);
        _pendingEntries.Clear();
        for (int requestIndex = 0; requestIndex < requests.Length; requestIndex++)
            _pendingEntries.Add(requests[requestIndex]);
        requests.Clear();

        for (int requestIndex = 0; requestIndex < _pendingEntries.Count; requestIndex++)
        {
            EffectEntry request = _pendingEntries[requestIndex];
            SkillContent context = EffectUtility.CreateContext(EntityManager, in request.Context);
            Execute(request.EffectListId, context, request.RepeatCount, request.Completion);

            if (request.ReleaseEffectListAfterExecution != 0)
                EffectDataBridgeUtility.Unregister(EntityManager, request.EffectListId);
            if (request.ReleaseManagedContextAfterExecution != 0)
            {
                EffectDataBridgeUtility.UnregisterManagedContext(
                    EntityManager,
                    request.Context.ManagedContextId);
            }
        }
    }

    private void Execute(
        EffectDataListId effectListId,
        SkillContent context,
        int repeatCount,
        EffectCompletionType completion)
    {
        if (EffectDataBridgeUtility.TryGet(EntityManager, effectListId, out EffectDataList effectDataList))
        {
            int resolvedRepeatCount = Mathf.Max(1, repeatCount);
            for (int repeatIndex = 0; repeatIndex < resolvedRepeatCount; repeatIndex++)
                SkillExecutor.ExecuteEffects(effectDataList.Effects, context);
        }

        Complete(completion, context);
    }

    private void Complete(EffectCompletionType completion, SkillContent context)
    {
        if (completion != EffectCompletionType.SkillCastComplete)
            return;

        UnitBuffHookUtility.Dispatch(
            EntityManager,
            context.OriginEntity,
            SkillHookType.OnCastComplete,
            SkillTriggerSource.ActiveCast,
            hasOriginEntity: context.HasOriginEntity,
            originEntity: context.OriginEntity,
            sourceSkillId: context.SourceSkillId,
            hasOtherEntity: context.HasTargetEntity,
            otherEntity: context.TargetEntity,
            hasPosition: context.HasPosition,
            position: context.Position);
    }
}
