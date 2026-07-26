using System.Collections.Generic;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    public enum UnitAnimationDirection
    {
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3,
    }

    [System.Serializable]
    public sealed class UnitAnimationEntryData
    {
        public string StateName;
        public string AnimationName;
        public string SpriteClipPath;

        public void Normalize()
        {
            StateName ??= string.Empty;
            AnimationName ??= string.Empty;
            SpriteClipPath ??= string.Empty;
        }
    }

    [ReadOnlyData]
    [System.Serializable]
    public sealed class UnitAnimationProfileData : DataRow
    {
        public int UnitDataId = -1;
        public string UnitName;
        public float PlaybackSpeed = 1f;
        public List<UnitAnimationEntryData> Animations = new();

        public void Normalize()
        {
            PlaybackSpeed = UnityEngine.Mathf.Max(0.01f, PlaybackSpeed);
            Animations ??= new List<UnitAnimationEntryData>();
            for (int i = 0; i < Animations.Count; i++)
            {
                Animations[i] ??= new UnitAnimationEntryData();
                Animations[i].Normalize();
            }
        }
    }
}
