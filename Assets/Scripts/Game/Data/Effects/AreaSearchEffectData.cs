using System.Collections.Generic;
using UnityEngine;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 范围搜索效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class AreaSearchEffectData : EffectData
    {
        /// <summary>搜索半径（世界单位）</summary>
        public float Radius;

        /// <summary>搜索中心相对施法者的偏移</summary>
        public Vector3 CenterOffset;

        /// <summary>目标过滤条件，候选单位全部通过后才会执行 OnAfterSearch。</summary>
        public List<ConditionConfig> TargetConditions = new();

        public EffectData[] OnAfterSearch;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            AreaSearchEffectData copy = (AreaSearchEffectData)base.CreateRuntimeCopy(modifiers);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, Radius);
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers);
            return copy;
        }
    }
}
