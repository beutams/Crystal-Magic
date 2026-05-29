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
        public string FrontAtlasTexturePath;
        public string BackAtlasTexturePath;
        public string LeftAtlasTexturePath;
        public float FramesPerSecond = 12f;
        public bool Loop = true;
        public int GridColumns = 4;
        public int GridRows = 4;
        public int FrameCount = 16;

        public void Normalize()
        {
            StateName ??= string.Empty;
            AnimationName ??= string.Empty;
            FrontAtlasTexturePath ??= string.Empty;
            BackAtlasTexturePath ??= string.Empty;
            LeftAtlasTexturePath ??= string.Empty;
            FramesPerSecond = UnityEngine.Mathf.Max(0.01f, FramesPerSecond);
            GridColumns = UnityEngine.Mathf.Max(1, GridColumns);
            GridRows = UnityEngine.Mathf.Max(1, GridRows);
            FrameCount = UnityEngine.Mathf.Clamp(FrameCount, 1, GridColumns * GridRows);
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
