using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public abstract class SkillAdditionActionData
    {
    }

    [Serializable]
    [FactoryKey("ModifyCurrentSkill", 0, "Modify Current Skill")]
    public sealed class ModifyCurrentSkillAdditionActionData : SkillAdditionActionData
    {
        public List<SkillAdditionModifierExpressionData> Modifiers = new();
    }

    [Serializable]
    public sealed class SkillAdditionModifierExpressionData
    {
        public SkillModifierChannel Channel;
        public ValueExpression Factor = CreateDefaultNumberExpression();
        public ValueExpression Bonus = CreateDefaultNumberExpression();

        private static ValueExpression CreateDefaultNumberExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromFloat(0f) };
        }
    }

    [Serializable]
    [FactoryKey("SetSourceValue", 10, "Set Source Value")]
    public sealed class SetSourceValueSkillAdditionActionData : SkillAdditionActionData
    {
        public string SetterKey = string.Empty;
        public string Key = string.Empty;
        public List<ValueExpression> Values = new();
    }

    [Serializable]
    [FactoryKey("ExecuteEffects", 20, "Execute Effects")]
    public sealed class ExecuteEffectsSkillAdditionActionData : SkillAdditionActionData
    {
        [SerializeReference]
        public EffectData[] Effects = Array.Empty<EffectData>();
    }

    [Serializable]
    [FactoryKey("ReplayCurrentSkill", 30, "Replay Current Skill")]
    public sealed class ReplayCurrentSkillAdditionActionData : SkillAdditionActionData
    {
    }
}
