using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitIntentAuthoring : MonoBehaviour
{
    class UnitIntentBaker : Baker<UnitIntentAuthoring>
    {
        public override void Bake(UnitIntentAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitIntentComponent());
            AddComponent(entity, new UnitCastComponent());
        }
    }
}

/// <summary>
/// 单位意图组件——所有输入源（玩家 Input / AI 行为树）的统一写入目标。
/// 状态机读取此组件决定行为，不直接读取原始输入。
/// 后续扩展攻击、施法等意图也加在这里。
/// </summary>
public struct UnitIntentComponent : IComponentData
{
    /// <summary>移动方向</summary>
    public float2 MoveDirection;
    /// <summary>想要释放技能</summary>
    public bool WantToCast;
    public bool HasCastTarget;
    public float2 CastTargetPosition;
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
    public bool IsCasting;
    public bool ForceInterrupt;
    public bool HasLockedTarget;
    public float2 LockedTargetPosition;
    public int CurrentChainIndex;
    public int CurrentSkillIndex;
    public int CurrentSkillId;
    public SkillCastPhase Phase;
    public float PhaseElapsed;
}
