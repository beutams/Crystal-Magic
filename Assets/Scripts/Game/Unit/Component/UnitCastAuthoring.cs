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
    public SkillFollowupConsumeRuleType ConsumeRuleType;
    public int ConsumeRuleStateInt0;
    public float ConsumeRuleStateFloat0;
    public SkillFollowupModifierRuleType ModifierRuleType;
    public int ModifierRuleStateInt0;
    public float ModifierRuleStateFloat0;
    public SkillFollowupFilterType FilterType;
    public int SkillId;
    public SkillType SkillType;
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
    public FixedList64Bytes<int> SkillIds;
    public FixedList64Bytes<int> SkillAdditionIds;

    /// <summary>
    /// 当前单位是否正在执行技能释放流程。
    /// </summary>
    public bool IsCasting;
    public bool StartedThisFrame;

    /// <summary>
    /// 是否请求强制打断当前施法。
    /// </summary>
    public bool ForceInterrupt;

    /// <summary>
    /// 本次施法开始时是否已经锁定目标位置。
    /// </summary>
    public bool HasLockedTarget;

    /// <summary>
    /// 本次施法锁定的目标位置。
    /// </summary>
    public float2 LockedTargetPosition;

    /// <summary>
    /// 当前正在释放的技能链索引。
    /// </summary>
    public int CurrentChainIndex;

    /// <summary>
    /// 当前正在释放的技能在技能链中的索引。
    /// </summary>
    public int CurrentSkillIndex;

    /// <summary>
    /// 当前正在释放的技能 ID。
    /// </summary>
    public int CurrentSkillId;

    public int ExecutionSerialCounter;
    public int CurrentExecutionToken;

    /// <summary>
    /// 当前施法阶段。
    /// </summary>
    public SkillCastPhase Phase;

    /// <summary>
    /// 当前施法阶段已经经过的时间。
    /// </summary>
    public float PhaseElapsed;
    public float PhaseDuration;
    public bool IsWaitingHook;
    public SkillCastHookPoint WaitingHookPoint;
    public SkillCastHookContinuation HookContinuation;
}
