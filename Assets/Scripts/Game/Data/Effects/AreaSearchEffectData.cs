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
        [EditorLabel("搜索半径")]
        public float Radius;

        /// <summary>搜索中心相对施法者的偏移</summary>
        [EditorLabel("中心偏移")]
        public Vector3 CenterOffset;

        /// <summary>目标过滤条件</summary>
        [EditorLabel("目标条件")]
        public List<ConditionConfig> TargetConditions = new();

        [EditorLabel("搜索后效果")]
        public EffectData[] OnAfterSearch;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            AreaSearchEffectData copy = (AreaSearchEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, Radius);
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers, elementComponent);
            return copy;
        }
    }
}
