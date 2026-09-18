using System;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public sealed class PlayerCurrentSkillAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<PlayerCurrentSkillAuthoring>
    {
        public override void Bake(PlayerCurrentSkillAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new PlayerCurrentSkillComponent());
        }
    }
}

public sealed class PlayerCurrentSkillComponent : IComponentData
{
    public int CurrentChainId = -1;
    public int CurrentSlotIndex = -1;
    public SkillModifierSet PendingExtraModifiers = new();
}

[UnitSourceProvider(typeof(PlayerCurrentSkillComponent), typeof(PlayerCurrentSkillAuthoring))]
public static class PlayerCurrentSkillSource
{
    [UnitSourceGet(0, "player.skill.currentChainId", UnitValueCategory.Number)]
    [UnitSourceGet(1, "player.skill.currentSlotIndex", UnitValueCategory.Number)]
    [UnitSourceGet(2, "player.skill.currentSkillId", UnitValueCategory.Number)]
    [UnitSourceGet(3, "player.skill.currentInputType", UnitValueCategory.Number)]
    [UnitSourceGet(4, "player.skill.currentAdditionId", UnitValueCategory.Number)]
    [UnitSourceGet(5, "player.skill.hasCurrentSkill", UnitValueCategory.Bool)]
    public static bool TryGet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<PlayerCurrentSkillComponent>(entity))
            return false;

        PlayerCurrentSkillComponent component = entityManager.GetComponentObject<PlayerCurrentSkillComponent>(entity);
        if (component == null)
            return false;

        switch (operation)
        {
            case 0:
                result = UnitSourceValue.FromInt(component.CurrentChainId);
                return true;
            case 1:
                result = UnitSourceValue.FromInt(component.CurrentSlotIndex);
                return true;
            case 2 when PlayerCurrentSkillUtility.TryGetCurrentSkillId(entityManager, entity, out int skillId):
                result = UnitSourceValue.FromInt(skillId);
                return true;
            case 3 when PlayerCurrentSkillUtility.TryGetCurrentInputType(entityManager, entity, out SkillInputType inputType):
                result = UnitSourceValue.FromInt((int)inputType);
                return true;
            case 4 when PlayerCurrentSkillUtility.TryGetCurrentAdditionId(entityManager, entity, out int additionId):
                result = UnitSourceValue.FromInt(additionId);
                return true;
            case 5:
                result = UnitSourceValue.FromBool(
                    PlayerCurrentSkillUtility.TryGetCurrentSkillId(entityManager, entity, out _));
                return true;
            default:
                return false;
        }
    }

    [UnitSourceSet(0, "player.skill.currentChainSlot.set", UnitValueCategory.Number, UnitValueCategory.Number,
        ParameterNames = new[] { "ChainId", "SlotIndex" })]
    [UnitSourceSet(1, "player.skill.currentChainSlot.clear", UnitValueCategory.Bool,
        ParameterNames = new[] { "Clear" })]
    [UnitSourceSet(2, "player.skill.pendingExtraModifiers.add", UnitValueCategory.Number, UnitValueCategory.Number,
        UnitValueCategory.Number, ParameterNames = new[] { "Channel", "Factor", "Bonus" })]
    public static bool TrySet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments)
    {
        switch (operation)
        {
            case 0 when arguments.TryGetInt(0, out int chainId) &&
                             arguments.TryGetInt(1, out int slotIndex):
                return PlayerCurrentSkillUtility.TrySetCurrentChainSlot(
                    entityManager, entity, chainId, slotIndex);
            case 1 when arguments.TryGetBool(0, out bool clear) && clear:
                PlayerCurrentSkillUtility.ClearCurrentChainSlot(entityManager, entity);
                return true;
            case 2 when arguments.TryGetInt(0, out int rawChannel) &&
                             arguments.TryGetNumber(1, out float factor) &&
                             arguments.TryGetNumber(2, out float bonus) &&
                             Enum.IsDefined(typeof(SkillModifierChannel), rawChannel):
                return PlayerCurrentSkillUtility.AddPendingExtraModifier(
                    entityManager,
                    entity,
                    new SkillModifierEntry
                    {
                        Channel = (SkillModifierChannel)rawChannel,
                        Factor = factor,
                        Bonus = bonus,
                    });
            default:
                return false;
        }
    }
}
