using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

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
