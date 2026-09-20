using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Marks the world-level effect request queue. Effect producers may also attach an
/// <see cref="EffectEntry"/> buffer directly to an entity when jobs own that queue.
/// </summary>
public struct EffectComponent : IComponentData
{
}

/// <summary>
/// Unmanaged effect execution request. EffectData itself stays behind EffectDataListId.
/// </summary>
public struct EffectEntry : IBufferElementData
{
    public EffectDataListId EffectListId;
    public EffectRequestContext Context;
    public int RepeatCount;
    public EffectCompletionType Completion;
    public byte ReleaseEffectListAfterExecution;
    public byte ReleaseManagedContextAfterExecution;
}

public struct EffectReleaseEntry : IBufferElementData
{
    public EffectDataListId EffectListId;
}

public struct EffectRequestContext
{
    public SkillTriggerSource TriggerSource;
    public SkillHookType HookType;
    public Entity OriginEntity;
    public Entity TargetEntity;
    public Entity OtherEntity;
    public int SourceSkillId;
    public float3 Position;
    public float3 OriginPositionSnapshot;
    public float TriggerValue;
    public SkillModifierSet RuntimeModifiers;
    public EffectManagedContextId ManagedContextId;
    public byte HasOriginEntity;
    public byte HasTargetEntity;
    public byte HasOtherEntity;
    public byte HasPosition;
    public byte HasOriginPositionSnapshot;
}

public enum EffectCompletionType : byte
{
    None = 0,
    SkillCastComplete = 1,
}
