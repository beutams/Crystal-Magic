using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitCastAuthoring : MonoBehaviour
{
    class UnitCastBaker : Baker<UnitCastAuthoring>
    {
        public override void Bake(UnitCastAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitCastComponent());
            AddComponentObject(entity, new UnitCastFollowupRuntimeComponent());
            AddComponentObject(entity, new UnitCastTaskPayloadComponent());
            AddComponentObject(entity, new UnitCastSkillPayloadComponent());
        }
    }
}

public enum SkillCastPhase : byte
{
    None = 0,
    Windup = 1,
    Chanting = 2,
    Recovery = 3,
}

public enum SkillCastHookContinuation : byte
{
    None = 0,
    StartWindup = 1,
    ScheduleBeforeExecute = 2,
    ExecutePrimarySkill = 3,
    StartRecovery = 4,
    FinishSkill = 5,
}

public struct UnitCastComponent : IComponentData
{
    public bool HasPreparedCast;
    public bool IsCasting;
    public bool StartedThisFrame;
    public bool ForceInterrupt;
    public int CurrentSkillId;
    public int CurrentSkillAdditionId;
    public int ExecutionSerialCounter;
    public int CurrentExecutionToken;
    public SkillCastPhase Phase;
    public float PhaseElapsed;
    public float PhaseDuration;
    public bool IsWaitingHook;
    public SkillCastHookPoint WaitingHookPoint;
    public SkillCastHookContinuation HookContinuation;
}
