using System.Collections.Generic;
using System.IO;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace CrystalMagic.Tests.Editor
{
    public sealed class OpenFieldVisualThemeTests
    {
        private sealed class DungeonThemeTableWrapper
        {
            public List<DungeonThemeData> Rows = new();
        }

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

        [Test]
        public void VisualEnsureValid_InitializesAllListsAndClampsObstacleMasks()
        {
            OpenFieldDungeonVisualData visual = new()
            {
                GroundCellsPerStyleSeed = 0,
                GroundStyles = new List<OpenFieldGroundStyleData>
                {
                    new()
                    {
                        Decorations = null,
                        Obstacles = new List<OpenFieldObstacleData>
                        {
                            new()
                            {
                                FootprintWidth = 2,
                                FootprintHeight = 3,
                                CollisionMask = new List<bool> { true, false, true, false, true, false, true },
                            },
                        },
                    },
                },
            };

            visual.EnsureValid();

            Assert.That(visual.GroundStyles, Is.Not.Null);
            Assert.That(visual.VoidVisual, Is.Not.Null);
            Assert.That(visual.ObstacleVisual, Is.Not.Null);
            Assert.That(visual.GroundCellsPerStyleSeed, Is.EqualTo(1));
            Assert.That(visual.GroundStyles[0].Decorations, Is.Not.Null);
            Assert.That(visual.GroundStyles[0].Obstacles[0].CollisionMask, Has.Count.EqualTo(6));
            Assert.That(visual.GroundStyles[0].Obstacles[0].CollisionMask,
                Is.EqualTo(new[] { true, false, true, false, true, false }));
        }

        [Test]
        public void VisualJson_RoundTripsOnlyResourceReferenceData()
        {
            OpenFieldDungeonVisualData visual = new()
            {
                VoidVisual = new OpenFieldVoidVisualData
                {
                    AbyssRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/Abyss.asset" },
                    WallRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/AbyssWall.asset" },
                    TransitionRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/AbyssTransition.asset" },
                },
                ObstacleVisual = new OpenFieldObstacleVisualData
                {
                    TopRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/ObstacleTop.asset" },
                    WallRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/ObstacleWall.asset" },
                    TransitionRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/ObstacleTransition.asset" },
                },
                GroundStyles = new List<OpenFieldGroundStyleData>
                {
                    new()
                    {
                        Name = "Moss",
                        BaseRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/Moss.asset" },
                        Decorations = new List<OpenFieldDecorationData>
                        {
                            new()
                            {
                                Name = "Pebbles",
                                RuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/Pebbles.asset" },
                                Radius = 4.5f,
                                MaximumSpread = 3,
                            },
                        },
                        Obstacles = new List<OpenFieldObstacleData>
                        {
                            new()
                            {
                                Name = "Tree",
                                Sprite = new OpenFieldSpriteReferenceData
                                {
                                    AssetPath = "Assets/Res/Sprites/Tree.png",
                                    SpriteName = "Tree_01",
                                    SpriteUv = new Vector4(0.1f, 0.2f, 0.3f, 0.4f),
                                    HasSpriteUv = true,
                                },
                                FootprintWidth = 2,
                                FootprintHeight = 2,
                                CollisionMask = new List<bool> { true, false, true, false },
                                Weight = 3,
                                MinimumSpacing = 2.5f,
                                MaximumCount = 7,
                                AllowRotation = true,
                                AllowFlipX = true,
                                VisualSortAnchor = new Vector2(0.25f, -0.5f),
                            },
                        },
                    },
                },
            };

            string json = JsonConvert.SerializeObject(visual);
            OpenFieldDungeonVisualData copy = JsonConvert.DeserializeObject<OpenFieldDungeonVisualData>(json);
            copy.EnsureValid();

            Assert.That(json, Does.Not.Contain("UnityEngine.Object"));
            Assert.That(copy.GroundStyles[0].BaseRuleTile.AssetPath, Is.EqualTo("Assets/Res/Tile/Moss.asset"));
            Assert.That(copy.GroundStyles[0].Decorations[0].RuleTile.AssetPath, Is.EqualTo("Assets/Res/Tile/Pebbles.asset"));
            Assert.That(copy.GroundStyles[0].Obstacles[0].Sprite.SpriteName, Is.EqualTo("Tree_01"));
            Assert.That(copy.GroundStyles[0].Obstacles[0].Sprite.SpriteUv, Is.EqualTo(new Vector4(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.That(copy.GroundStyles[0].Obstacles[0].CollisionMask,
                Is.EqualTo(new[] { true, false, true, false }));
        }

        [Test]
        public void DungeonThemeTable_IsAnEmptyRowsTable()
        {
            string json = File.ReadAllText("Assets/Res/Data/DungeonThemeDataTable.json");
            DungeonThemeTableWrapper table = JsonConvert.DeserializeObject<DungeonThemeTableWrapper>(json);

            Assert.That(table, Is.Not.Null);
            Assert.That(table.Rows, Is.Empty);
        }
    }
}
