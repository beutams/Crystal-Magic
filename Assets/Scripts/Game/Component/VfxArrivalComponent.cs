using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

public sealed class VfxArrivalComponent : IComponentData
{
    public float3 StartPosition;
    public float3 EndPosition;
    public float Duration;
    public float Elapsed;
    public SkillContent ArrivalContext;
    public EffectData[] OnArrivalEffects;
}
