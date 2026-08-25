using System;
using UnityEngine;

namespace CrystalMagic.Game.OpenField
{
    public static class OpenFieldDungeonTerrainGenerator
    {
        public static OpenFieldDungeonLayout Generate(int seed, OpenFieldDungeonTerrainConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            OpenFieldDungeonTerrainConfig validatedConfig = config.CloneValidated();
            OpenFieldDungeonLayout layout = new(validatedConfig.Width, validatedConfig.Height, seed);
            System.Random random = new(seed);
            float offsetX = random.Next(-10000, 10001);
            float offsetY = random.Next(-10000, 10001);
            float baseScale = Mathf.Clamp(
                Mathf.Min(layout.Width, layout.Height) * validatedConfig.BaseScaleMultiplier,
                validatedConfig.MinimumBaseScale,
                validatedConfig.MaximumBaseScale);

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    float value = SampleFbmPerlin(x, y, offsetX, offsetY, baseScale, validatedConfig);
                    layout.SetTerrain(x, y, value, Classify(value, validatedConfig));
                }
            }

            return layout;
        }

        private static float SampleFbmPerlin(
            float x,
            float y,
            float offsetX,
            float offsetY,
            float baseScale,
            OpenFieldDungeonTerrainConfig config)
        {
            float macro = Mathf.PerlinNoise((x + offsetX) / baseScale, (y + offsetY) / baseScale);
            float medium = Mathf.PerlinNoise(
                (x + offsetX) * config.MediumFrequencyMultiplier / baseScale,
                (y + offsetY) * config.MediumFrequencyMultiplier / baseScale);
            float detail = Mathf.PerlinNoise(
                (x + offsetX) * config.DetailFrequencyMultiplier / baseScale,
                (y + offsetY) * config.DetailFrequencyMultiplier / baseScale);
            return (macro + medium * config.MediumAmplitude + detail * config.DetailAmplitude) /
                   (1f + config.MediumAmplitude + config.DetailAmplitude);
        }

        private static OpenFieldTerrainCell Classify(float value, OpenFieldDungeonTerrainConfig config)
        {
            if (value < config.LowToGroundThreshold)
                return OpenFieldTerrainCell.Void;
            if (value < config.GroundToObstacleThreshold)
                return OpenFieldTerrainCell.Ground;

            return OpenFieldTerrainCell.Obstacle;
        }
    }
}