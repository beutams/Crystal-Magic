using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 鎸佺画鎬ф晥鏋滐紙Buff / 鍦哄湴鏁堟灉锛夌殑閰嶇疆鏁版嵁
    /// </summary>
    [System.Serializable]
    public sealed class PersistentEffectData : EffectData, IElementalEffectData
    {
        public ElementType Element = ElementType.None;
        public ElementAffectMode ElementAffect = ElementAffectMode.Duration;

        /// <summary>鎬绘寔缁椂闂达紙绉掞級</summary>
        public float TotalDuration;

        /// <summary>鍛ㄦ湡鎬цЕ鍙戦棿闅旓紙绉掞級锛? = 涓嶆寜 Tick 閲嶅</summary>
        public float TickIntervalSeconds;

        /// <summary>寮€濮嬫椂绔嬪嵆瑙﹀彂鐨勬晥鏋滈摼</summary>
        [UnityEngine.SerializeReference]
        public EffectData[] OnStartEffects = System.Array.Empty<EffectData>();

        /// <summary>姣忔鍛ㄦ湡瑙﹀彂鏃舵墽琛岀殑鏁堟灉閾?/summary>
        [UnityEngine.SerializeReference]
        public EffectData[] OnTickEffects = System.Array.Empty<EffectData>();

        ElementType IElementalEffectData.Element => Element;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            SkillModifierSet runtimeModifiers = CreateCombinedModifiers(modifiers, elementBonus, AppendElementModifiers);
            PersistentEffectData copy = (PersistentEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementBonus, elementBonusResolver);
            copy.TotalDuration = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.EffectDuration, TotalDuration);
            copy.TotalDuration = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.BuffDuration, copy.TotalDuration);
            copy.TickIntervalSeconds = ApplyModifierNonNegative(runtimeModifiers, SkillModifierChannel.TickInterval, TickIntervalSeconds);
            copy.OnStartEffects = CreateRuntimeCopies(OnStartEffects, modifiers, elementBonusResolver);
            copy.OnTickEffects = CreateRuntimeCopies(OnTickEffects, modifiers, elementBonusResolver);
            return copy;
        }

        private void AppendElementModifiers(float elementBonus, SkillModifierSet modifiers)
        {
            if (Element == ElementType.None || !AffectsDuration(ElementAffect))
                return;

            modifiers.Add(new SkillModifierEntry
            {
                Channel = SkillModifierChannel.BuffDuration,
                Factor = elementBonus,
            });
        }
    }
}
