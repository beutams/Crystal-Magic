using CrystalMagic.Game.Data;
using Unity.Entities;

public class UnitSkillModifierRuntimeComponent : IComponentData
{
    public SkillModifierSet Modifiers = new();
}
