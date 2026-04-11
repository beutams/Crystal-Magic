namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 伤害效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class DamageEffectData : EffectData
    {
        /// <summary>伤害倍率（相对攻击力的系数）</summary>
        public float DamageCoefficient;

        /// <summary>固定伤害加算</summary>
        public float FlatDamageBonus;

        /// <summary>伤害 / 元素类型 Id</summary>
        public int DamageTypeId;

        /// <summary>是否允许暴击</summary>
        public bool AllowCritical = true;

        /// <summary>暴击伤害额外倍率加成（如 0.5 = +50%）</summary>
        public float CriticalBonus;

        /// <summary>击退力度</summary>
        public float KnockbackForce;

        /// <summary>硬直时间（秒）</summary>
        public float HitStunSeconds;

        /// <summary>伤害浮动随机种子偏移</summary>
        public int DamageVarianceSeed;
    }
}
