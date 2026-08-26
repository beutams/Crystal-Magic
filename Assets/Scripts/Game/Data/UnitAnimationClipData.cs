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

    public enum UnitAnimationDirectionMode
    {
        FourDirections = 0,
        TwoDirections = 1,
    }

    [System.Serializable]
    public sealed class UnitAnimationEntryData
    {
        public string Name;
        public UnitAnimationDirectionMode DirectionMode;
        public string FrontClipPath;
        public string BackClipPath;
        public string LeftClipPath;
        public string RightClipPath;

        public void Normalize()
        {
            if (DirectionMode != UnitAnimationDirectionMode.FourDirections &&
                DirectionMode != UnitAnimationDirectionMode.TwoDirections)
            {
                DirectionMode = UnitAnimationDirectionMode.FourDirections;
            }

            Name ??= string.Empty;
            FrontClipPath ??= string.Empty;
            BackClipPath ??= string.Empty;
            LeftClipPath ??= string.Empty;
            RightClipPath ??= string.Empty;
        }

        public string GetClipPath(UnitAnimationDirection direction)
        {
            return direction switch
            {
                UnitAnimationDirection.Front => FrontClipPath,
                UnitAnimationDirection.Back => BackClipPath,
                UnitAnimationDirection.Left => LeftClipPath,
                UnitAnimationDirection.Right => RightClipPath,
                _ => string.Empty,
            };
        }
    }

    [ReadOnlyData]
    [System.Serializable]
    public sealed class UnitAnimationProfileData : DataRow
    {
        public int UnitDataId = -1;
        public string UnitName;
        public List<UnitAnimationEntryData> Animations = new();

        public void Normalize()
        {
            UnitName ??= string.Empty;
            Animations ??= new List<UnitAnimationEntryData>();
            for (int i = 0; i < Animations.Count; i++)
            {
                Animations[i] ??= new UnitAnimationEntryData();
                Animations[i].Normalize();
            }
        }
    }
}
