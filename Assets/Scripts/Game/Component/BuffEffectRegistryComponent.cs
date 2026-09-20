using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

public enum BuffEffectType : byte
{
    PropertyModifier = 0,
    SkillModifier = 1,
    TriggeredEffect = 2,
}

public struct BuffEffectReference
{
    public BuffEffectType Type;
    public int Id;
}

public struct BuffDefinitionBlob
{
    public int BuffId;
    public byte CanStack;
    public int MaxStacks;
    public BlobArray<BuffEffectReference> Effects;
}

public struct BuffTriggeredEffectBlob
{
    public BuffTriggerType TriggerType;
    public SkillHookType HookType;
    public float TickIntervalSeconds;
    public byte ConsumeStackOnTrigger;
    public EffectDataListId EffectListId;
}

public struct PropertyModifierRuntimeEntry
{
    public PropertyModifierEntry Value;
    public float MinimumFactor;
}

public struct SkillModifierRuntimeEntry
{
    public SkillModifierEntry Value;
    public float MinimumFactor;
}

public struct BuffEffectRegistryBlob
{
    public BlobArray<BuffDefinitionBlob> Buffs;
    public BlobArray<PropertyModifierRuntimeEntry> PropertyModifiers;
    public BlobArray<SkillModifierRuntimeEntry> SkillModifiers;
    public BlobArray<BuffTriggeredEffectBlob> TriggeredEffects;
}

public struct BuffEffectRegistryComponent : IComponentData
{
    public BlobAssetReference<BuffEffectRegistryBlob> Value;
}
