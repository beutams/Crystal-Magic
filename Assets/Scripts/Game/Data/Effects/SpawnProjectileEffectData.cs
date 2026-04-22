using UnityEngine;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 创建投射物效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class SpawnProjectileEffectData : EffectData
    {
        /// <summary>投射物配置</summary>
        public GameObject Projectile;

        /// <summary>投射物飞行速度（世界单位/秒）</summary>
        public float Speed;

        /// <summary>最大飞行距离（世界单位），0 = 不限</summary>
        public float MaxRange;

        /// <summary>相对施法者的生成偏移</summary>
        public Vector3 SpawnOffset;

        /// <summary>投射物缩放倍率，1 = 原始大小</summary>
        public float Scale = 1f;

        /// <summary>穿透目标数量，0 = 不穿透</summary>
        public bool CanPierce;

        /// <summary>追踪强度，0 = 直线飞行，>0 = 追踪目标</summary>
        public bool TriggerDestroyEffectsOnMaxRange;

        [SerializeReference]
        public EffectData[] OnCollisionEffects;
        [SerializeReference]
        [JsonProperty("OnDestoryEffects")]
        public EffectData[] OnDestroyEffects;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            SpawnProjectileEffectData copy = (SpawnProjectileEffectData)base.CreateRuntimeCopy(modifiers);
            copy.Speed = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileSpeed, Speed);
            copy.MaxRange = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileRange, MaxRange);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileScale, Scale);
            copy.OnCollisionEffects = CreateRuntimeCopies(OnCollisionEffects, modifiers);
            copy.OnDestroyEffects = CreateRuntimeCopies(OnDestroyEffects, modifiers);
            return copy;
        }
    }
}
