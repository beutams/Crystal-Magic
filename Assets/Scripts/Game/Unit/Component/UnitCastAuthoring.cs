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

public struct UnitCastComponent : IComponentData
{
    /// <summary>
    /// 当前单位是否正在执行技能释放流程。
    /// </summary>
    public bool IsCasting;

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

    /// <summary>
    /// 当前施法阶段。
    /// </summary>
    public SkillCastPhase Phase;

    /// <summary>
    /// 当前施法阶段已经经过的时间。
    /// </summary>
    public float PhaseElapsed;
}
