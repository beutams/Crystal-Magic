using CrystalMagic.Game.Data;
using Newtonsoft.Json;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 浼ゅ鏁堟灉鐨勯厤缃暟鎹?
    /// </summary>
    [System.Serializable]
    public sealed class DamageEffectData : EffectData, IElementalEffectData
    {
        /// <summary>浼ゅ鍊嶇巼锛堢浉瀵规敾鍑诲姏鐨勭郴鏁帮級</summary>
        public float DamageCoefficient;

        /// <summary>鍥哄畾浼ゅ鍔犵畻</summary>
        public float FlatDamageBonus;

        /// <summary>浼ゅ / 鍏冪礌绫诲瀷</summary>
        public ElementType Element = ElementType.None;

        /// <summary>鍏冪礌灞炴€у璇ユ晥鏋滅殑褰卞搷鏂瑰紡</summary>
        public ElementAffectMode ElementAffect = ElementAffectMode.Magnitude;

        /// <summary>鏄惁鍏佽鏆村嚮</summary>
        public bool AllowCritical = true;

        /// <summary>鏆村嚮浼ゅ棰濆鍊嶇巼鍔犳垚锛堝 0.5 = +50%锛?/summary>
        public float CriticalBonus;

        /// <summary>鍑婚€€鍔涘害</summary>
        public float KnockbackForce;

        /// <summary>纭洿鏃堕棿锛堢锛?/summary>
        public float HitStunSeconds;

        /// <summary>浼ゅ娴姩闅忔満绉嶅瓙鍋忕Щ</summary>
        public int DamageVarianceSeed;

        [JsonProperty("DamageTypeId")]
        private ElementType LegacyDamageTypeId
        {
            set => Element = value;
        }

        ElementType IElementalEffectData.Element => Element;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            SkillModifierSet runtimeModifiers = CreateCombinedModifiers(modifiers, elementBonus, AppendElementModifiers);
            DamageEffectData copy = (DamageEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementBonus, elementBonusResolver);
            copy.DamageCoefficient = ApplyModifier(runtimeModifiers, SkillModifierChannel.Damage, DamageCoefficient);
            copy.FlatDamageBonus = ApplyModifier(runtimeModifiers, SkillModifierChannel.FlatDamage, FlatDamageBonus);
            copy.CriticalBonus = ApplyModifier(runtimeModifiers, SkillModifierChannel.CriticalBonus, CriticalBonus);
            copy.KnockbackForce = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.KnockbackForce, KnockbackForce);
            copy.HitStunSeconds = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.HitStunSeconds, HitStunSeconds);
            return copy;
        }

        private void AppendElementModifiers(float elementBonus, SkillModifierSet modifiers)
        {
            if (Element == ElementType.None || !AffectsMagnitude(ElementAffect))
                return;

            modifiers.Add(new SkillModifierEntry
            {
                Channel = SkillModifierChannel.Damage,
                Factor = elementBonus,
            });
            modifiers.Add(new SkillModifierEntry
            {
                Channel = SkillModifierChannel.FlatDamage,
                Factor = elementBonus,
            });
        }
    }
}
