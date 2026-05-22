using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;

public class UnitCastSkillPayloadComponent : IComponentData
{
    public List<ResolvedSkillData> ResolvedSkills = new();
}
