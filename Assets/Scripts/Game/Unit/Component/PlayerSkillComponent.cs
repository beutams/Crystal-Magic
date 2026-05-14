using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct PlayerSkillComponent : IComponentData
{
    public FixedList64Bytes<int> SkillIds;
    public FixedList64Bytes<int> SkillAdditionIds;
    public bool HasPendingCast;
    public bool HasLockedTarget;
    public float2 LockedTargetPosition;
    public int ChainIndex;

    public void Clear()
    {
        SkillIds = default;
        SkillAdditionIds = default;
        HasPendingCast = false;
        HasLockedTarget = false;
        LockedTargetPosition = float2.zero;
        ChainIndex = -1;
    }
}
