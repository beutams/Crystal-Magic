using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Collections;
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
            AddBuffer<UnitCastFollowupEffectElement>(entity);
            AddComponentObject(entity, new UnitCastTaskPayloadComponent());
            AddComponentObject(entity, new UnitCastSkillPayloadComponent());
        }
    }
}

public struct UnitCastFollowupEffectElement : IBufferElementData
{
    public int SourceSkillId;
    public int SourceSkillAdditionId;
    public FixedString64Bytes ConsumeRuleKey;
    public int ConsumeRuleStateInt0;
    public float ConsumeRuleStateFloat0;
    public FixedString64Bytes ModifierRuleKey;
    public int ModifierRuleStateInt0;
    public float ModifierRuleStateFloat0;
    public SkillFollowupFilterType FilterType;
    public int SkillId;
    public FixedString64Bytes RuntimeType;
    public ElementType Element;
    public int SkillAdditionId;
    public FixedList4096Bytes<SkillModifierEntry> ModifierEntries;
    public FixedList128Bytes<SkillFollowupModifierSlice> ModifierSlices;
}

public struct SkillFollowupModifierSlice
{
    public int StartIndex;
    public int Length;
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
    public bool HasLockedTarget;
    public float2 LockedTargetPosition;
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
