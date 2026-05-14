using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Data.Effects
{
    [Serializable]
    public sealed class ReadBuffStackEffectData : EffectData
    {
        [EditorLabel("BuffId")]
        public int BuffId = -1;

        [EditorLabel("每层修正")]
        public List<SkillModifierEntry> PerStackModifiers = new();

        [EditorLabel("读取后效果")]
        public EffectData[] OnAfterRead = Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, Func<EffectData, float> elementBonusResolver = null)
        {
            ReadBuffStackEffectData copy = (ReadBuffStackEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.PerStackModifiers = PerStackModifiers == null ? new List<SkillModifierEntry>() : new List<SkillModifierEntry>(PerStackModifiers);
            copy.OnAfterRead = CreateRuntimeCopies(OnAfterRead, modifiers, elementBonusResolver);
            return copy;
        }
    }

    [Serializable]
    public sealed class RemoveBuffEffectData : EffectData
    {
        [EditorLabel("BuffId")]
        public int BuffId = -1;

        [EditorLabel("清除全部层数")]
        public bool RemoveAllStacks = true;

        [EditorLabel("清除层数")]
        public int RemoveStackCount = 1;
    }
}
