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
