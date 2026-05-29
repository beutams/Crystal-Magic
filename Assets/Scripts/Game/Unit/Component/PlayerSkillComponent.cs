using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct PlayerSkillComponent : IComponentData
{
    public FixedList64Bytes<int> SkillIds;
    public FixedList64Bytes<int> SkillAdditionIds;
    public bool HasActiveChain;
    public bool HasPendingCast;
    public int CurrentSkillIndex;
    public bool HasLockedTarget;
    public float2 LockedTargetPosition;

    public void Clear()
    {
        SkillIds = default;
        SkillAdditionIds = default;
        HasActiveChain = false;
        HasPendingCast = false;
        CurrentSkillIndex = -1;
        HasLockedTarget = false;
        LockedTargetPosition = float2.zero;
    }
}
