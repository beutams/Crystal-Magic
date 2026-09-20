using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill
{
    public static class SkillChainResolver
    {
        public static SkillData GetSkillDataBySkillStoneItemId(int skillStoneItemId)
        {
            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null)
                return null;

            ItemData skillStoneItemData = dataComponent.Get<ItemData>(skillStoneItemId);
            if (skillStoneItemData == null || skillStoneItemData.ItemType != ItemType.SkillStone || skillStoneItemData.ExtraId < 0)
                return null;

            return dataComponent.Get<SkillData>(skillStoneItemData.ExtraId);
        }

        public static SkillData GetSkillData(SkillChainSlotData slotData)
        {
            return slotData == null ? null : GetSkillDataBySkillStoneItemId(slotData.SkillStoneItemId);
        }

    }

    public static class SkillResolver
    {
        public static ResolvedSkillData Resolve(SkillData skillData, SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            if (skillData == null)
                return null;

            return new ResolvedSkillData
            {
                Source = skillData,
                Id = skillData.Id,
                Name = skillData.DisplayName,
                RuntimeType = skillData.EffectiveRuntimeType,
                MpCost = GetModifiedMpCost(modifiers, skillData.MpCost),
                EffectChain = EffectData.CreateRuntimeCopies(skillData.EffectChain, modifiers, elementComponent),
            };
        }

        private static int GetModifiedMpCost(SkillModifierSet modifiers, float baseMpCost)
        {
            if (!math.isfinite(baseMpCost))
                return 0;

            float modifiedMpCost = modifiers.Apply(SkillModifierChannel.MpCost, baseMpCost);
            return math.max(0, (int)math.round(modifiedMpCost));
        }
    }
}
