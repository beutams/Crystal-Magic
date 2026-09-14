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

[UnitSourceAuthoring(typeof(PlayerCurrentSkillAuthoring))]
public sealed class PlayerCurrentSkillSource : UnitManagedComponentSource<PlayerCurrentSkillComponent>
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = Array.Empty<ComparatorParameterDefinition>();
    private static readonly ComparatorParameterDefinition[] s_chainSlotParameters =
    {
        new ComparatorParameterDefinition("ChainId", UnitValueCategory.Number),
        new ComparatorParameterDefinition("SlotIndex", UnitValueCategory.Number),
    };
    private static readonly ComparatorParameterDefinition[] s_clearParameters =
    {
        new ComparatorParameterDefinition("Clear", UnitValueCategory.Bool),
    };
    private static readonly ComparatorParameterDefinition[] s_extraModifierParameters =
    {
        new ComparatorParameterDefinition("Channel", UnitValueCategory.Number),
        new ComparatorParameterDefinition("Factor", UnitValueCategory.Number),
        new ComparatorParameterDefinition("Bonus", UnitValueCategory.Number),
    };

    protected override void Define(UnitSourceDefinitionBuilder<PlayerCurrentSkillComponent> builder)
    {
        builder.AddGet("player.skill.currentChainId", UnitValueCategory.Number,
            (in PlayerCurrentSkillComponent component) => UnitValue.FromInt(component.CurrentChainId));
        builder.AddGet("player.skill.currentSlotIndex", UnitValueCategory.Number,
            (in PlayerCurrentSkillComponent component) => UnitValue.FromInt(component.CurrentSlotIndex));
        builder.AddContextGet("player.skill.currentSkillId", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in PlayerCurrentSkillComponent _, UnitValue[] _) =>
                PlayerCurrentSkillUtility.TryGetCurrentSkillId(context.EntityManager, context.Entity, out int skillId)
                    ? UnitValue.FromInt(skillId)
                    : UnitValue.None);
        builder.AddContextGet("player.skill.currentInputType", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in PlayerCurrentSkillComponent _, UnitValue[] _) =>
                PlayerCurrentSkillUtility.TryGetCurrentInputType(context.EntityManager, context.Entity, out SkillInputType inputType)
                    ? UnitValue.FromInt((int)inputType)
                    : UnitValue.None);
        builder.AddContextGet("player.skill.currentAdditionId", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in PlayerCurrentSkillComponent _, UnitValue[] _) =>
                PlayerCurrentSkillUtility.TryGetCurrentAdditionId(context.EntityManager, context.Entity, out int additionId)
                    ? UnitValue.FromInt(additionId)
                    : UnitValue.None);
        builder.AddContextGet("player.skill.hasCurrentSkill", UnitValueCategory.Bool, s_noParameters,
            (in UnitSourceBindingContext context, in PlayerCurrentSkillComponent _, UnitValue[] _) =>
                UnitValue.FromBool(PlayerCurrentSkillUtility.TryGetCurrentSkillId(context.EntityManager, context.Entity, out _)));

        builder.AddContextSet("player.skill.currentChainSlot.set", s_chainSlotParameters,
            (in UnitSourceBindingContext context, ref PlayerCurrentSkillComponent _, UnitValue[] values) =>
                TryGetInt(values, 0, out int chainId) &&
                TryGetInt(values, 1, out int slotIndex) &&
                PlayerCurrentSkillUtility.TrySetCurrentChainSlot(context.EntityManager, context.Entity, chainId, slotIndex));
        builder.AddContextSet("player.skill.currentChainSlot.clear", s_clearParameters,
            (in UnitSourceBindingContext context, ref PlayerCurrentSkillComponent _, UnitValue[] values) =>
            {
                if (values.Length != 1 || values[0].Type != UnitValueType.Bool || !values[0].Bool)
                    return false;

                PlayerCurrentSkillUtility.ClearCurrentChainSlot(context.EntityManager, context.Entity);
                return true;
            });
        builder.AddContextSet("player.skill.pendingExtraModifiers.add", s_extraModifierParameters,
            (in UnitSourceBindingContext context, ref PlayerCurrentSkillComponent _, UnitValue[] values) =>
                TryGetInt(values, 0, out int rawChannel) &&
                values[1].TryGetNumber(out float factor) &&
                values[2].TryGetNumber(out float bonus) &&
                Enum.IsDefined(typeof(SkillModifierChannel), rawChannel) &&
                PlayerCurrentSkillUtility.AddPendingExtraModifier(context.EntityManager, context.Entity, new SkillModifierEntry
                {
                    Channel = (SkillModifierChannel)rawChannel,
                    Factor = factor,
                    Bonus = bonus,
                }));
    }

    private static bool TryGetInt(UnitValue[] values, int index, out int result)
    {
        result = 0;
        if (values == null ||
            index < 0 ||
            index >= values.Length ||
            !values[index].TryGetNumber(out float value) ||
            float.IsNaN(value) ||
            float.IsInfinity(value) ||
            Mathf.Abs(value - Mathf.Round(value)) > 0.0001f)
        {
            return false;
        }

        result = Mathf.RoundToInt(value);
        return true;
    }
}
