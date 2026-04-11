using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    public enum BuffCategory
    {
        PropertyModifier = 0,
        Effect = 1,
    }

    [System.Serializable]
    public abstract class BuffData : DataRow
    {
        public string Name;
        public float  Duration;
        public bool   CanStack;
        public int    MaxStacks = 1;
        public abstract BuffCategory Category { get; }
    }

    /// <summary>
    /// 纯属性修饰类 Buff——字段按 Component 分组，
    /// 只填有影响的字段，其余保持 0 即可。
    /// </summary>
    [ReadOnlyData]
    [System.Serializable]
    public class PropertyBuffData : BuffData
    {
        // ── Move ─────────────────────────────────
        public float MoveSpeedFactor;
        public float MoveSpeedBonus;

        // ── Vitality（Health + Defense）──────────
        public float MaxHealthFactor;
        public float MaxHealthBonus;
        public float DefenseFactor;
        public float DefenseBonus;

        // ── Attack ───────────────────────────────
        public float AttackPowerFactor;
        public float AttackPowerBonus;
        public float SkillRangeFactor;
        public float SkillRangeBonus;

        // ── Mana ─────────────────────────────────
        public float MaxMpFactor;
        public float MaxMpBonus;

        public override BuffCategory Category => BuffCategory.PropertyModifier;
    }

    [ReadOnlyData]
    [System.Serializable]
    public class EffectBuffData : BuffData
    {
        public override BuffCategory Category => BuffCategory.Effect;

        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }
}
