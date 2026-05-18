using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

public class UnitCastTaskPayloadComponent : IComponentData
{
    public int ExecutionToken = -1;
    public int InitializedHookMask;
    public List<SkillCastTaskRuntime> ActiveTasks = new();
}
