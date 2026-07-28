using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    [System.Serializable]
    public sealed class LocalizationData : DataRow
    {
        public string Key;
        public string ChineseSimplified;
        public string English;
    }
}
