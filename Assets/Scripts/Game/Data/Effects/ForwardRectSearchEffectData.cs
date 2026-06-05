using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 从施法者位置起，朝目标方向进行前向矩形搜索。
    /// </summary>
    [System.Serializable]
    public sealed class ForwardRectSearchEffectData : EffectData
    {
        [EditorLabel("矩形长度")]
        public float Length = 1f;

        [EditorLabel("矩形宽度")]
        public float Width = 1f;

        [EditorLabel("目标条件")]
        public List<ConditionConfig> TargetConditions = new();

        [EditorLabel("搜索后效果")]
        public EffectData[] OnAfterSearch = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            ForwardRectSearchEffectData copy = (ForwardRectSearchEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Length = Length < 0f ? 0f : Length;
            copy.Width = Width < 0f ? 0f : Width;
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers, elementComponent);
            return copy;
        }
    }
}
