using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class EffectUtility
{
    public static Entity GetOrCreateEntity(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EffectComponent>());
        if (!query.IsEmptyIgnoreFilter)
            return query.GetSingletonEntity();

        Entity entity = entityManager.CreateEntity();
        entityManager.AddComponent<EffectComponent>(entity);
        entityManager.AddBuffer<EffectEntry>(entity);
        entityManager.AddBuffer<EffectReleaseEntry>(entity);
        return entity;
    }

    public static void Enqueue(
        EntityManager entityManager,
        EffectData[] effects,
        SkillContent context,
        int repeatCount = 1,
        EffectCompletionType completion = EffectCompletionType.None)
    {
        EffectDataListId effectListId = EffectDataBridgeUtility.Register(entityManager, effects);
        Enqueue(
            entityManager,
            effectListId,
            context,
            repeatCount,
            completion,
            releaseEffectListAfterExecution: effectListId.IsValid);
    }

    public static void Enqueue(
        EntityManager entityManager,
        EffectDataListId effectListId,
        SkillContent context,
        int repeatCount = 1,
        EffectCompletionType completion = EffectCompletionType.None,
        bool releaseEffectListAfterExecution = false)
    {
        if (context == null || (!effectListId.IsValid && completion == EffectCompletionType.None))
            return;

        EffectRequestContext requestContext = CaptureContext(entityManager, context, out bool ownsManagedContext);
        Entity queueEntity = GetOrCreateEntity(entityManager);
        entityManager.GetBuffer<EffectEntry>(queueEntity).Add(new EffectEntry
        {
            EffectListId = effectListId,
            Context = requestContext,
            RepeatCount = math.max(1, repeatCount),
            Completion = completion,
            ReleaseEffectListAfterExecution = releaseEffectListAfterExecution ? (byte)1 : (byte)0,
            ReleaseManagedContextAfterExecution = ownsManagedContext ? (byte)1 : (byte)0,
        });
    }

    public static void ReleaseAfterExecution(EntityManager entityManager, EffectDataListId effectListId)
    {
        if (!effectListId.IsValid)
            return;

        Entity queueEntity = GetOrCreateEntity(entityManager);
        entityManager.GetBuffer<EffectReleaseEntry>(queueEntity).Add(new EffectReleaseEntry
        {
            EffectListId = effectListId,
        });
    }

    public static EffectRequestContext CaptureContext(
        EntityManager entityManager,
        SkillContent source,
        out bool ownsManagedContext)
    {
        if (source == null)
        {
            ownsManagedContext = false;
            return default;
        }

        EffectManagedContextId managedContextId =
            EffectDataBridgeUtility.RegisterManagedContext(entityManager, source);
        ownsManagedContext = managedContextId.IsValid;
        return new EffectRequestContext
        {
            TriggerSource = source.TriggerSource,
            HookType = source.HookType,
            HasOriginEntity = source.HasOriginEntity ? (byte)1 : (byte)0,
            OriginEntity = source.OriginEntity,
            HasTargetEntity = source.HasTargetEntity ? (byte)1 : (byte)0,
            TargetEntity = source.TargetEntity,
            HasOtherEntity = source.HasOtherEntity ? (byte)1 : (byte)0,
            OtherEntity = source.OtherEntity,
            SourceSkillId = source.SourceSkillId,
            HasPosition = source.HasPosition ? (byte)1 : (byte)0,
            Position = new float3(source.Position.x, source.Position.y, source.Position.z),
            HasOriginPositionSnapshot = source.HasOriginPositionSnapshot ? (byte)1 : (byte)0,
            OriginPositionSnapshot = new float3(
                source.OriginPositionSnapshot.x,
                source.OriginPositionSnapshot.y,
                source.OriginPositionSnapshot.z),
            TriggerValue = source.TriggerValue,
            RuntimeModifiers = source.RuntimeModifiers,
            ManagedContextId = managedContextId,
        };
    }

    public static SkillContent CreateContext(EntityManager entityManager, in EffectRequestContext source)
    {
        SkillContent context = new()
        {
            EntityManager = entityManager,
            TriggerSource = source.TriggerSource,
            HookType = source.HookType,
            HasOriginEntity = source.HasOriginEntity != 0,
            OriginEntity = source.OriginEntity,
            HasTargetEntity = source.HasTargetEntity != 0,
            TargetEntity = source.TargetEntity,
            HasOtherEntity = source.HasOtherEntity != 0,
            OtherEntity = source.OtherEntity,
            SourceSkillId = source.SourceSkillId,
            HasPosition = source.HasPosition != 0,
            Position = new Vector3(source.Position.x, source.Position.y, source.Position.z),
            HasOriginPositionSnapshot = source.HasOriginPositionSnapshot != 0,
            OriginPositionSnapshot = new Vector3(
                source.OriginPositionSnapshot.x,
                source.OriginPositionSnapshot.y,
                source.OriginPositionSnapshot.z),
            TriggerValue = source.TriggerValue,
            RuntimeModifiers = source.RuntimeModifiers,
        };

        if (EffectDataBridgeUtility.TryGetManagedContext(
                entityManager,
                source.ManagedContextId,
                out EffectManagedContextState managedState))
        {
            context.HasTarget = managedState.HasTarget;
            context.Target = managedState.Target;
            context.Origin = managedState.Origin;
            context.PersistentEffectAppliedBuffTargets = managedState.PersistentEffectAppliedBuffTargets;
        }

        return context;
    }
}
