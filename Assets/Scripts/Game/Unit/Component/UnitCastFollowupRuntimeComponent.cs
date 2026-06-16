using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using Unity.Entities;

public class UnitCastFollowupRuntimeComponent : IComponentData
{
    public List<SkillFollowupRuntime> Followups = new();
}
