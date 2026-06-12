using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(PersistentBeamEffectSystem))]
partial class EffectExecutionSystem : SystemBase
{
    private readonly SkillContent _context = new();

    protected override void OnCreate()
    {
        PendingEffectExecutionQueueUtility.GetOrCreate(EntityManager);
        RequireForUpdate<PendingEffectExecutionQueueComponent>();
    }

    protected override void OnUpdate()
    {
        PendingEffectExecutionQueueComponent queue = PendingEffectExecutionQueueUtility.GetOrCreate(EntityManager);
        if (queue.Entries.Count <= 0)
            return;

        for (int i = 0; i < queue.Entries.Count; i++)
        {
            PendingEffectExecutionEntry entry = queue.Entries[i];
            if (entry?.Effects == null || entry.Effects.Length == 0)
                continue;

            PopulateContext(entry);
            int repeatCount = Mathf.Max(1, entry.RepeatCount);
            for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                SkillExecutor.ExecuteEffects(entry.Effects, _context);
        }

        queue.Entries.Clear();
    }

    private void PopulateContext(PendingEffectExecutionEntry entry)
    {
        _context.EntityManager = EntityManager;
        _context.TriggerSource = entry.TriggerSource;
        _context.HookType = entry.HookType;
        _context.HasOtherEntity = entry.HasOtherEntity;
        _context.OtherEntity = entry.OtherEntity;
        _context.TriggerValue = entry.TriggerValue;
        _context.HasOriginEntity = entry.HasOriginEntity;
        _context.OriginEntity = entry.OriginEntity;
        _context.SourceSkillId = entry.SourceSkillId;
        _context.HasTargetEntity = entry.HasTargetEntity;
        _context.TargetEntity = entry.TargetEntity;
        _context.HasTarget = false;
        _context.Target = null;
        _context.Origin = null;
        _context.HasPosition = entry.HasPosition;
        _context.Position = entry.Position;
        _context.RuntimeModifiers = null;
    }
}

public sealed class PendingEffectExecutionQueueComponent : IComponentData
{
    public List<PendingEffectExecutionEntry> Entries = new();

    public void Enqueue(PendingEffectExecutionEntry entry)
    {
        if (entry == null || entry.Effects == null || entry.Effects.Length == 0)
            return;

        Entries.Add(entry);
    }
}

public static class PendingEffectExecutionQueueUtility
{
    public static PendingEffectExecutionQueueComponent GetOrCreate(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PendingEffectExecutionQueueComponent>());
        if (!query.IsEmptyIgnoreFilter)
        {
            Entity singletonEntity = query.GetSingletonEntity();
            return entityManager.GetComponentObject<PendingEffectExecutionQueueComponent>(singletonEntity);
        }

        Entity entity = entityManager.CreateEntity();
        PendingEffectExecutionQueueComponent queue = new();
        entityManager.AddComponentObject(entity, queue);
        return queue;
    }
}

public sealed class PendingEffectExecutionEntry
{
    public EffectData[] Effects = Array.Empty<EffectData>();
    public SkillTriggerSource TriggerSource;
    public SkillHookType HookType;
    public bool HasOriginEntity;
    public Entity OriginEntity = Entity.Null;
    public int SourceSkillId = -1;
    public bool HasTargetEntity;
    public Entity TargetEntity = Entity.Null;
    public bool HasOtherEntity;
    public Entity OtherEntity = Entity.Null;
    public bool HasPosition;
    public Vector3 Position = Vector3.zero;
    public float TriggerValue;
    public int RepeatCount = 1;
}
