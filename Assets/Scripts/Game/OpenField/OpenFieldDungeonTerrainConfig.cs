using System;
using UnityEngine;

namespace CrystalMagic.Game.OpenField
{
    [Serializable]
    public sealed class OpenFieldDungeonTerrainConfig
    {
        private const int MinimumMapSide = 8;

        public int Width = 200;
        public int Height = 200;
        public float LowToGroundThreshold = 0.42f;
        public float GroundToObstacleThreshold = 0.58f;
        public float BaseScaleMultiplier = 0.65f;
        public float MinimumBaseScale = 30f;
        public float MaximumBaseScale = 64f;
        public float MediumFrequencyMultiplier = 2f;
        public float MediumAmplitude = 0.5f;
        public float DetailFrequencyMultiplier = 8f;
        public float DetailAmplitude = 0.65f;

        public OpenFieldDungeonTerrainConfig CloneValidated()
        {
            OpenFieldDungeonTerrainConfig copy = new()
            {
                Width = Width,
                Height = Height,
                LowToGroundThreshold = LowToGroundThreshold,
                GroundToObstacleThreshold = GroundToObstacleThreshold,
                BaseScaleMultiplier = BaseScaleMultiplier,
                MinimumBaseScale = MinimumBaseScale,
                MaximumBaseScale = MaximumBaseScale,
                MediumFrequencyMultiplier = MediumFrequencyMultiplier,
                MediumAmplitude = MediumAmplitude,
                DetailFrequencyMultiplier = DetailFrequencyMultiplier,
                DetailAmplitude = DetailAmplitude,
            };
            copy.EnsureValid();
            return copy;
        }

        public void EnsureValid()
        {
            Width = Mathf.Max(MinimumMapSide, Width);
            Height = Mathf.Max(MinimumMapSide, Height);
            LowToGroundThreshold = Mathf.Clamp(LowToGroundThreshold, 0.01f, 0.98f);
            GroundToObstacleThreshold = Mathf.Clamp(GroundToObstacleThreshold, LowToGroundThreshold + 0.01f, 0.99f);
            BaseScaleMultiplier = Mathf.Max(0.01f, BaseScaleMultiplier);
            MinimumBaseScale = Mathf.Max(1f, MinimumBaseScale);
            MaximumBaseScale = Mathf.Max(MinimumBaseScale, MaximumBaseScale);
            MediumFrequencyMultiplier = Mathf.Max(0.01f, MediumFrequencyMultiplier);
            MediumAmplitude = Mathf.Max(0f, MediumAmplitude);
            DetailFrequencyMultiplier = Mathf.Max(0.01f, DetailFrequencyMultiplier);
            DetailAmplitude = Mathf.Max(0f, DetailAmplitude);
        }
    }
}