using System;
using System.Collections.Generic;
using Unity.Entities;

[Serializable]
public sealed class NetworkBuffStateData : NetworkStateData
{
    public List<NetworkBuffEntryStateData> buffs = new();

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        DynamicBuffer<UnitBuffElement> runtime = UnitBuffUtility.GetOrCreateRuntimeBuffer(context.EntityManager, entity);
        runtime.Clear();
        BuffEffectRegistryUtility.TryGet(
            context.EntityManager,
            out BlobAssetReference<BuffEffectRegistryBlob> registry);
        for (int index = 0; index < buffs.Count; index++)
        {
            NetworkBuffEntryStateData state = buffs[index];
            bool hasOriginEntity = context.TryGetEntity(state.originUnitId, out Entity originEntity);
            runtime.Add(new UnitBuffElement
            {
                BuffId = state.buffId,
                DefinitionIndex = registry.IsCreated
                    ? BuffEffectRegistryUtility.FindBuffIndex(in registry, state.buffId)
                    : -1,
                RemainingTime = context.GetRemainingSeconds(state.endFrame),
                StackCount = state.stackCount,
                OriginEntity = hasOriginEntity ? originEntity : Entity.Null,
                SourceSkillId = state.sourceSkillId,
            });
        }

        UnitBuffComponent component = context.EntityManager.GetComponentData<UnitBuffComponent>(entity);
        component.NetworkDirty = 0;
        component.ModifierDirty = 1;
        context.EntityManager.SetComponentData(entity, component);

        if (context.IsClient)
            ApplyPresentationState(context, entity);
    }

    private void ApplyPresentationState(NetworkStateApplyContext context, Entity entity)
    {
        Dictionary<(int BuffId, int SourceSkillId, Entity Origin), Entity> existingVisuals = new();
        DynamicBuffer<ClientBuffPresentationElement> presentation;
        if (context.EntityManager.HasBuffer<ClientBuffPresentationElement>(entity))
        {
            presentation = context.EntityManager.GetBuffer<ClientBuffPresentationElement>(entity);
            for (int index = 0; index < presentation.Length; index++)
            {
                ClientBuffPresentationElement entry = presentation[index];
                if (entry.VisualEntity != Entity.Null)
                    existingVisuals[(entry.BuffId, entry.SourceSkillId, entry.OriginEntity)] = entry.VisualEntity;
            }
            presentation.Clear();
        }
        else
        {
            presentation = context.EntityManager.AddBuffer<ClientBuffPresentationElement>(entity);
        }

        for (int index = 0; index < buffs.Count; index++)
        {
            NetworkBuffEntryStateData state = buffs[index];
            float remainingTime = context.GetRemainingSeconds(state.endFrame);
            if (remainingTime == 0f)
                continue;

            context.TryGetEntity(state.originUnitId, out Entity originEntity);
            (int BuffId, int SourceSkillId, Entity Origin) key =
                (state.buffId, state.sourceSkillId, originEntity);
            existingVisuals.TryGetValue(key, out Entity visualEntity);
            existingVisuals.Remove(key);
            presentation.Add(new ClientBuffPresentationElement
            {
                BuffId = state.buffId,
                SourceSkillId = state.sourceSkillId,
                OriginEntity = originEntity,
                EndFrame = state.endFrame,
                StackCount = state.stackCount,
                DisplayRemainingTime = remainingTime,
                VisualEntity = visualEntity,
            });
        }

        foreach (Entity visualEntity in existingVisuals.Values)
            EndVisual(context.EntityManager, visualEntity);
    }

    private static void EndVisual(EntityManager entityManager, Entity visualEntity)
    {
        if (visualEntity == Entity.Null || !entityManager.Exists(visualEntity))
            return;

        if (entityManager.HasComponent<SpriteEffectAnimationComponent>(visualEntity))
        {
            SpriteEffectAnimationSystem.RequestEnd(entityManager, visualEntity);
            return;
        }

        if (!entityManager.HasComponent<DestroyEntityFlag>(visualEntity))
            entityManager.AddComponent<DestroyEntityFlag>(visualEntity);
        entityManager.SetComponentEnabled<DestroyEntityFlag>(visualEntity, true);
    }
}

[Serializable]
public sealed class NetworkBuffEntryStateData
{
    public int buffId;
    public uint endFrame;
    public int stackCount;
    public Guid originUnitId;
    public int sourceSkillId;
}
