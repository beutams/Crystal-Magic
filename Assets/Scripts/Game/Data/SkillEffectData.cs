using System.Collections.Generic;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    [System.Serializable]
    [ReadOnlyData]
    public class SkillEffectData : DataRow
    {
        public string Name;
        public string Description;
        public string IconPath;
        public List<SkillModifierEntry> Modifiers = new();
    }
}
