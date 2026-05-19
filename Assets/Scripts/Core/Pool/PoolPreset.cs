using UnityEngine;

namespace CrystalMagic.Core
{
    public enum PoolPresetTier
    {
        Single = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
    }

    public sealed class PoolPreset : MonoBehaviour
    {
        public PoolPresetTier Tier = PoolPresetTier.Medium;
    }
}
