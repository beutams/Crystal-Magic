using CrystalMagic.Game.OpenField;
using NUnit.Framework;

namespace CrystalMagic.Tests.Editor
{
    public sealed class OpenFieldVisualThemeTests
    {
        [Test]
        public void Generate_AssignsExpectedHeightForEveryTerrainCategory()
        {
            OpenFieldDungeonTerrainConfig config = new()
            {
                Width = 48,
                Height = 48,
                LowToGroundThreshold = 0.40f,
                GroundToObstacleThreshold = 0.55f,
                MinimumObstacleHeight = 2,
                MaximumObstacleHeight = 5,
            };

            OpenFieldDungeonLayout layout = OpenFieldDungeonTerrainGenerator.Generate(712367, config);
            int voidCount = 0;
            int groundCount = 0;
            int obstacleCount = 0;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    int height = layout.GetHeightSteps(x, y);
                    switch (layout.GetTerrainCell(x, y))
                    {
                        case OpenFieldTerrainCell.Void:
                            voidCount++;
                            Assert.That(height, Is.EqualTo(-1));
                            break;
                        case OpenFieldTerrainCell.Ground:
                            groundCount++;
                            Assert.That(height, Is.EqualTo(0));
                            break;
                        case OpenFieldTerrainCell.Obstacle:
                            obstacleCount++;
                            Assert.That(height, Is.InRange(2, 5));
                            break;
                    }
                }
            }

            Assert.That(voidCount, Is.GreaterThan(0));
            Assert.That(groundCount, Is.GreaterThan(0));
            Assert.That(obstacleCount, Is.GreaterThan(0));
        }

        [Test]
        public void CloneValidated_NormalizesAndCopiesObstacleHeightRange()
        {
            OpenFieldDungeonTerrainConfig config = new()
            {
                MinimumObstacleHeight = 0,
                MaximumObstacleHeight = -5,
            };

            OpenFieldDungeonTerrainConfig validated = config.CloneValidated();

            Assert.That(validated.MinimumObstacleHeight, Is.EqualTo(1));
            Assert.That(validated.MaximumObstacleHeight, Is.EqualTo(1));
        }

        [Test]
        public void SetTerrain_EnforcesTerrainCategoryHeights()
        {
            OpenFieldDungeonLayout layout = new(3, 1, 0);

            layout.SetTerrain(0, 0, 0.2f, OpenFieldTerrainCell.Void, 99);
            layout.SetTerrain(1, 0, 0.5f, OpenFieldTerrainCell.Ground, -99);
            layout.SetTerrain(2, 0, 0.8f, OpenFieldTerrainCell.Obstacle, -99);

            Assert.That(layout.GetHeightSteps(0, 0), Is.EqualTo(-1));
            Assert.That(layout.GetHeightSteps(1, 0), Is.EqualTo(0));
            Assert.That(layout.GetHeightSteps(2, 0), Is.EqualTo(1));
        }

        [Test]
        public void Generate_PreservesLargeConfiguredObstacleHeight()
        {
            const int height = 16_777_217;
            OpenFieldDungeonTerrainConfig config = new()
            {
                Width = 48,
                Height = 48,
                LowToGroundThreshold = 0.40f,
                GroundToObstacleThreshold = 0.55f,
                MinimumObstacleHeight = height,
                MaximumObstacleHeight = height,
            };

            OpenFieldDungeonLayout layout = OpenFieldDungeonTerrainGenerator.Generate(712367, config);
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (layout.GetTerrainCell(x, y) == OpenFieldTerrainCell.Obstacle)
                        Assert.That(layout.GetHeightSteps(x, y), Is.EqualTo(height));
                }
            }
        }
    }
}
