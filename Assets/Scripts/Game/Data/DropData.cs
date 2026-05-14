using System.Collections.Generic;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    public enum DropRewardType
    {
        Item = 0,
        Money = 1,
    }

    [ReadOnlyData]
    [System.Serializable]
    public sealed class DropData : DataRow
    {
        public string Name;
        public string Description;
        public List<DropEntryData> Entries = new();

        public void EnsureValid()
        {
            Entries ??= new List<DropEntryData>();
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i] ??= new DropEntryData();
                Entries[i].Chance = UnityEngine.Mathf.Clamp01(Entries[i].Chance);
                Entries[i].MinQuantity = UnityEngine.Mathf.Max(0, Entries[i].MinQuantity);
                Entries[i].MaxQuantity = UnityEngine.Mathf.Max(Entries[i].MinQuantity, Entries[i].MaxQuantity);
            }
        }
    }

    [System.Serializable]
    public sealed class DropEntryData
    {
        public DropRewardType DropType;
        public int ItemId = -1;
        public float Chance = 1f;
        public int MinQuantity = 1;
        public int MaxQuantity = 1;
    }
}
