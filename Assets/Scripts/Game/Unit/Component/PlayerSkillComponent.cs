using Unity.Collections;
using Unity.Entities;
public struct PlayerSkillComponent : IComponentData
{
    public FixedList64Bytes<int> SkillIds;
    public FixedList64Bytes<int> SkillAdditionIds;
    public bool HasActiveChain;
    public bool HasPendingCast;
    public int ActiveChainIndex;
    public int CurrentSkillIndex;

    public void Clear()
    {
        SkillIds = default;
        SkillAdditionIds = default;
        HasActiveChain = false;
        HasPendingCast = false;
        ActiveChainIndex = -1;
        CurrentSkillIndex = -1;
    }
}
