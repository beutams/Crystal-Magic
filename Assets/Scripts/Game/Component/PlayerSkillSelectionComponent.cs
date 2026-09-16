using CrystalMagic.Core;
using Unity.Entities;

public struct PlayerSkillSelectionComponent : IComponentData
{
    public const string ChangedEventName = "Player.Skill.Selection.Changed";
    public int CurrentChainIndex;
    public byte NetworkDirty;
}

public static class PlayerSkillSelectionUtility
{
    public static int GetCurrentChainIndex()
    {
        return GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager entityManager, out Entity player) &&
               entityManager.HasComponent<PlayerSkillSelectionComponent>(player)
            ? entityManager.GetComponentData<PlayerSkillSelectionComponent>(player).CurrentChainIndex
            : 0;
    }
}
