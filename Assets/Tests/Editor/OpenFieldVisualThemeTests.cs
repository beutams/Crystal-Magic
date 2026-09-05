using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
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
                                    SpriteUv = new OpenFieldSpriteUvData(0.1f, 0.2f, 0.3f, 0.4f),
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
                                VisualSortAnchor = new OpenFieldVector2Data(0.25f, -0.5f),
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
            OpenFieldObstacleSpriteCellData copiedTree = copy.GroundStyles[0].Obstacles[0].SpriteLayers[0].Cells[0];
            Assert.That(copiedTree.Sprite.SpriteName, Is.EqualTo("Tree_01"));
            Assert.That(copiedTree.Sprite.SpriteUv.ToVector4(), Is.EqualTo(new Vector4(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.That(copiedTree.UseObstacleCenter, Is.True);
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

        [Test]
        public void VisualLayout_AssignsStylesDecorationsAndObstacleClearance()
        {
            const int mapSize = 48;
            OpenFieldDungeonLayout layout = new(mapSize, mapSize, 934217);
            for (int y = 0; y < mapSize; y++)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    OpenFieldTerrainCell terrain = x == 0
                        ? OpenFieldTerrainCell.Void
                        : x == mapSize - 1
                            ? OpenFieldTerrainCell.Obstacle
                            : OpenFieldTerrainCell.Ground;
                    layout.SetTerrain(x, y, 0.5f, terrain, terrain == OpenFieldTerrainCell.Obstacle ? 2 : 0);
                }
            }

            OpenFieldObstacleData obstacle = new()
            {
                Name = "TwoByTwo",
                Sprite = new OpenFieldSpriteReferenceData { AssetPath = "Assets/Res/Sprites/TwoByTwo.png" },
                FootprintWidth = 2,
                FootprintHeight = 2,
                CollisionMask = new List<bool> { true, false, true, true },
                Weight = 1,
                MinimumSpacing = 3f,
                                         MaximumCount = 2,
                                         AllowRotation = true,
                                         AllowFlipX = true,
                                         VisualSortAnchor = new OpenFieldVector2Data(0.25f, -0.5f),
            };
            OpenFieldDungeonVisualData visual = new()
            {
                GroundCellsPerStyleSeed = 360,
                VoidVisual = new OpenFieldVoidVisualData
                {
                    AbyssRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/Abyss.asset" },
                    WallRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/VoidWall.asset" },
                    TransitionRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/VoidTransition.asset" },
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
                        Name = "Meadow",
                        BaseRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/Meadow.asset" },
                        Decorations = new List<OpenFieldDecorationData>
                        {
                            new()
                            {
                                Name = "MeadowPebbles",
                                RuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/MeadowPebbles.asset" },
                                Radius = 5f,
                                MaximumSpread = 6,
                            },
                        },
                        Obstacles = new List<OpenFieldObstacleData> { obstacle },
                    },
                    new()
                    {
                        Name = "Forest",
                        BaseRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/Forest.asset" },
                        Decorations = new List<OpenFieldDecorationData>
                        {
                            new()
                            {
                                Name = "ForestGrass",
                                RuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/ForestGrass.asset" },
                                Radius = 5f,
                                MaximumSpread = 6,
                            },
                        },
                        Obstacles = new List<OpenFieldObstacleData> { obstacle },
                    },
                },
            };
            HashSet<Vector2Int> protectedCells = new()
            {
                new Vector2Int(5, 5),
                new Vector2Int(24, 24),
                new Vector2Int(40, 40),
            };

            OpenFieldDungeonVisualLayout result = OpenFieldDungeonVisualLayoutBuilder.Build(layout, visual, protectedCells);
            OpenFieldDungeonVisualLayout repeated = OpenFieldDungeonVisualLayoutBuilder.Build(layout, visual, protectedCells);

            AssertLayoutsAreIdentical(layout, result, repeated);

            for (int y = 0; y < mapSize; y++)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    if (layout.GetTerrainCell(x, y) == OpenFieldTerrainCell.Ground)
                        Assert.That(result.GetGroundStyleIndex(x, y), Is.GreaterThanOrEqualTo(0));
                }
            }

            List<OpenFieldRuleTilePlacement> decorations = result.RuleTilePlacements
                .Where(placement => placement.Role == OpenFieldRuleTileRole.Decoration)
                .ToList();
            Assert.That(decorations, Is.Not.Empty);
            foreach (OpenFieldRuleTilePlacement placement in decorations)
                Assert.That(result.IsStyleInterior(placement.Cell, placement.GroundStyleIndex), Is.True);

            Assert.That(result.Obstacles, Is.Not.Empty);
            Assert.That(result.Obstacles, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(result.Obstacles, Has.Count.EqualTo(repeated.Obstacles.Count));
            HashSet<Vector2Int> collisionCells = new();
            foreach (OpenFieldObstaclePlacement placement in result.Obstacles)
            {
                foreach (Vector2Int cell in placement.CollisionCells)
                {
                    Assert.That(layout.GetTerrainCell(cell.x, cell.y), Is.EqualTo(OpenFieldTerrainCell.Ground));
                    Assert.That(protectedCells, Has.No.Member(cell));
                    Assert.That(collisionCells.Add(cell), Is.True, $"Collision cell {cell} was claimed twice.");
                    for (int deltaY = -1; deltaY <= 1; deltaY++)
                    {
                        for (int deltaX = -1; deltaX <= 1; deltaX++)
                        {
                            if (deltaX == 0 && deltaY == 0)
                                continue;

                            int neighbourX = cell.x + deltaX;
                            int neighbourY = cell.y + deltaY;
                            Assert.That(layout.IsInside(neighbourX, neighbourY), Is.True);
                            Assert.That(layout.GetTerrainCell(neighbourX, neighbourY), Is.EqualTo(OpenFieldTerrainCell.Ground));
                        }
                    }
                }
            }

            for (int i = 0; i < result.Obstacles.Count; i++)
            {
                OpenFieldObstaclePlacement first = result.Obstacles[i];
                OpenFieldObstaclePlacement repeatedFirst = repeated.Obstacles[i];
                Assert.That(first.Origin, Is.EqualTo(repeatedFirst.Origin));
                Assert.That(first.RotationQuarterTurns, Is.EqualTo(repeatedFirst.RotationQuarterTurns));
                Assert.That(first.FlippedX, Is.EqualTo(repeatedFirst.FlippedX));

                for (int j = i + 1; j < result.Obstacles.Count; j++)
                {
                    OpenFieldObstaclePlacement second = result.Obstacles[j];
                    float minimumSpacing = Mathf.Max(first.MinimumSpacing, second.MinimumSpacing);
                    foreach (Vector2Int firstCell in first.OccupiedCells)
                    {
                        foreach (Vector2Int secondCell in second.OccupiedCells)
                        {
                            float deltaX = firstCell.x - secondCell.x;
                            float deltaY = firstCell.y - secondCell.y;
                            Assert.That(deltaX * deltaX + deltaY * deltaY,
                                Is.GreaterThanOrEqualTo(minimumSpacing * minimumSpacing));
                        }
                    }
                }
            }
        }

        [Test]
        public void VisualLayout_AssignsBothStylesInsideEachVoidSeparatedGroundRegion()
        {
            const int regionWidth = 20;
            const int regionHeight = 24;
            const int voidColumn = regionWidth;
            const int cellsPerStyleSeed = 64;
            OpenFieldDungeonLayout layout = new(regionWidth * 2 + 1, regionHeight, 618731);
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    OpenFieldTerrainCell terrain = x == voidColumn
                        ? OpenFieldTerrainCell.Void
                        : OpenFieldTerrainCell.Ground;
                    layout.SetTerrain(x, y, 0.5f, terrain, terrain == OpenFieldTerrainCell.Void ? -1 : 0);
                }
            }

            OpenFieldDungeonVisualData visual = new()
            {
                GroundCellsPerStyleSeed = cellsPerStyleSeed,
                GroundStyles = new List<OpenFieldGroundStyleData>
                {
                    new() { Name = "Meadow" },
                    new() { Name = "Forest" },
                },
            };

            OpenFieldDungeonVisualLayout result = OpenFieldDungeonVisualLayoutBuilder.Build(
                layout,
                visual,
                new HashSet<Vector2Int>());

            int regionArea = regionWidth * regionHeight;
            int seedCountPerRegion = (regionArea + cellsPerStyleSeed - 1) / cellsPerStyleSeed;
            Assert.That(seedCountPerRegion, Is.GreaterThan(visual.GroundStyles.Count));

            AssertGroundRegionHasBothStyles(result, 0, regionWidth - 1, regionHeight);
            AssertGroundRegionHasBothStyles(result, voidColumn + 1, layout.Width - 1, regionHeight);
        }

        [Test]
        public void VisualLayout_UsesActualRotationAndFlipForEachObstacleCollisionMask()
        {
            OpenFieldObstacleData hook = new()
            {
                Name = "Hook",
                Sprite = new OpenFieldSpriteReferenceData { AssetPath = "Assets/Res/Sprites/Hook.png" },
                FootprintWidth = 2,
                FootprintHeight = 3,
                CollisionMask = new List<bool> { true, false, false, true, true, false },
                Weight = 1,
                MaximumCount = 4,
                AllowRotation = true,
                AllowFlipX = true,
            };
            OpenFieldObstacleData stair = new()
            {
                Name = "Stair",
                Sprite = new OpenFieldSpriteReferenceData { AssetPath = "Assets/Res/Sprites/Stair.png" },
                FootprintWidth = 3,
                FootprintHeight = 2,
                CollisionMask = new List<bool> { false, true, true, true, false, false },
                Weight = 5,
                MaximumCount = 4,
                AllowRotation = true,
                AllowFlipX = true,
            };
            OpenFieldDungeonVisualData visual = new()
            {
                GroundStyles = new List<OpenFieldGroundStyleData>
                {
                    new()
                    {
                        Name = "Ground",
                        Obstacles = new List<OpenFieldObstacleData> { hook, stair },
                    },
                },
            };

            int[] layoutSeeds = { 71, 193, 307, 449, 587, 733, 887, 991 };
            bool sawHook = false;
            bool sawStair = false;
            bool sawRotation = false;
            bool sawFlip = false;
            foreach (int layoutSeed in layoutSeeds)
            {
                OpenFieldDungeonLayout layout = CreateGroundLayout(48, 48, layoutSeed);
                OpenFieldDungeonVisualLayout result = OpenFieldDungeonVisualLayoutBuilder.Build(
                    layout,
                    visual,
                    new HashSet<Vector2Int>());

                Assert.That(result.Obstacles, Is.Not.Empty, $"Seed {layoutSeed} produced no obstacles.");
                foreach (OpenFieldObstaclePlacement placement in result.Obstacles)
                {
                    OpenFieldObstacleData obstacle = visual.GroundStyles[placement.GroundStyleIndex]
                        .Obstacles[placement.ObstacleIndex];
                    List<Vector2Int> expectedFootprint = GetExpectedObstacleCells(
                        obstacle,
                        placement.Origin,
                        placement.RotationQuarterTurns,
                        placement.FlippedX,
                        false);
                    List<Vector2Int> expectedCollisionCells = GetExpectedObstacleCells(
                        obstacle,
                        placement.Origin,
                        placement.RotationQuarterTurns,
                        placement.FlippedX,
                        true);

                    Assert.That(placement.OccupiedCells, Has.Count.EqualTo(obstacle.FootprintWidth * obstacle.FootprintHeight));
                    Assert.That(placement.CollisionCells,
                        Has.Count.EqualTo(obstacle.CollisionMask.Count(isCollision => isCollision)));
                    Assert.That(placement.OccupiedCells, Is.EquivalentTo(expectedFootprint));
                    Assert.That(placement.CollisionCells, Is.EquivalentTo(expectedCollisionCells));

                    sawHook |= placement.ObstacleIndex == 0;
                    sawStair |= placement.ObstacleIndex == 1;
                    sawRotation |= placement.RotationQuarterTurns != 0;
                    sawFlip |= placement.FlippedX;
                }
            }

            Assert.That(sawHook, Is.True, "The lower-weight asymmetric obstacle was never exercised.");
            Assert.That(sawStair, Is.True, "The higher-weight asymmetric obstacle was never exercised.");
            Assert.That(sawRotation, Is.True, "No rotated placement was exercised.");
            Assert.That(sawFlip, Is.True, "No horizontally flipped placement was exercised.");
        }

        [Test]
        public void VisualLayout_UsesWallsAtVoidEdgesAndTransitionsOnlyOnGround()
        {
            OpenFieldDungeonLayout layout = new(4, 3, 27);
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                    layout.SetTerrain(x, y, 0.5f, OpenFieldTerrainCell.Ground, 0);
            }

            layout.SetTerrain(0, 0, 0.1f, OpenFieldTerrainCell.Void, -1);
            layout.SetTerrain(1, 1, 0.1f, OpenFieldTerrainCell.Void, -1);
            layout.SetTerrain(2, 1, 0.1f, OpenFieldTerrainCell.Void, -1);
            layout.SetTerrain(2, 0, 0.9f, OpenFieldTerrainCell.Obstacle, 2);
            layout.SetTerrain(3, 1, 0.9f, OpenFieldTerrainCell.Obstacle, 2);

            OpenFieldDungeonVisualData visual = new()
            {
                GroundStyles = new List<OpenFieldGroundStyleData> { new() { Name = "Ground" } },
            };

            OpenFieldDungeonVisualLayout result = OpenFieldDungeonVisualLayoutBuilder.Build(
                layout,
                visual,
                new HashSet<Vector2Int>());

            Assert.That(result.RuleTilePlacements.Any(placement =>
                placement.Role == OpenFieldRuleTileRole.VoidWall && placement.Cell == new Vector2Int(0, 0)), Is.True);
            Assert.That(result.RuleTilePlacements.Any(placement =>
                placement.Role == OpenFieldRuleTileRole.VoidTransition && placement.Cell == new Vector2Int(1, 0)), Is.True);
            Assert.That(result.RuleTilePlacements.Any(placement =>
                placement.Role == OpenFieldRuleTileRole.VoidTransition && placement.Cell == new Vector2Int(2, 0)), Is.False);
            Assert.That(result.RuleTilePlacements.Any(placement =>
                placement.Role == OpenFieldRuleTileRole.ObstacleTransition && placement.Cell == new Vector2Int(3, 0)), Is.True);
        }

        [Test]
        public void SceneData_EmitsTerrainVisualsAndKeepsObstacleCollisionsAwayFromProtectedContent()
        {
            OpenFieldDungeonLayout layout = CreateGroundLayout(48, 48, 481516);
            layout.SetTerrain(0, 0, 0.1f, OpenFieldTerrainCell.Void, -1);
            layout.SetTerrain(47, 47, 0.9f, OpenFieldTerrainCell.Obstacle, 2);
            layout.SetEntrance(new OpenFieldGridPosition(5, 5), 1);
            layout.AddInterestPoint(OpenFieldInterestSize.Large, new OpenFieldGridPosition(24, 24), 3);
            layout.SetExitInterestPoint(layout.InterestPoints[0]);
            layout.AddContent(OpenFieldContentType.Chest, new OpenFieldGridPosition(40, 40), layout.ExitInterestPoint.EncounterId, 0);

            DungeonThemeData theme = new()
            {
                Id = 1,
                ThemeKey = "SceneDataTest",
                OpenField = new OpenFieldDungeonThemeData
                {
                    Visual = new OpenFieldDungeonVisualData
                    {
                        VoidVisual = new OpenFieldVoidVisualData
                        {
                            AbyssRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/TestAbyss.asset" },
                            WallRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/TestVoidWall.asset" },
                            TransitionRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/TestVoidTransition.asset" },
                        },
                        ObstacleVisual = new OpenFieldObstacleVisualData
                        {
                            TopRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/TestObstacleTop.asset" },
                            WallRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/TestObstacleWall.asset" },
                            TransitionRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/TestObstacleTransition.asset" },
                        },
                        GroundStyles = new List<OpenFieldGroundStyleData>
                        {
                            new()
                            {
                                Name = "TestGround",
                                BaseRuleTile = new OpenFieldRuleTileReferenceData { AssetPath = "Assets/Res/Tile/TestGround.asset" },
                                Obstacles = new List<OpenFieldObstacleData>
                                {
                                    new()
                                    {
                                        Name = "PartialCollisionObstacle",
                                        Sprite = new OpenFieldSpriteReferenceData
                                        {
                                            AssetPath = "Assets/Res/Sprites/TestObstacle.png",
                                            SpriteName = "TestObstacle_01",
                                            SpriteUv = new OpenFieldSpriteUvData(0f, 0f, 0.5f, 0.5f),
                                            HasSpriteUv = true,
                                        },
                                        FootprintWidth = 2,
                                        FootprintHeight = 2,
                                        CollisionMask = new List<bool> { true, false, false, false },
                                        Weight = 1,
                                         MaximumCount = 2,
                                         AllowRotation = true,
                                         AllowFlipX = true,
                                         VisualSortAnchor = new OpenFieldVector2Data(0.25f, -0.5f),
                                    },
                                },
                            },
                        },
                    },
                },
            };

            RuntimeDungeonSceneData scene = OpenFieldDungeonSceneDataBuilder.Build(
                layout,
                theme,
                new CrystalMagic.Game.Config.DungeonConfig(),
                1,
                false);

            HashSet<Vector2Int> protectedCells = new()
            {
                new Vector2Int(layout.Entrance.X, layout.Entrance.Y),
                new Vector2Int(layout.ExitInterestPoint.Center.X, layout.ExitInterestPoint.Center.Y),
                new Vector2Int(40, 40),
            };
            OpenFieldDungeonVisualLayout sourceVisual = OpenFieldDungeonVisualLayoutBuilder.Build(
                layout,
                theme.OpenField.Visual,
                protectedCells);

            Assert.That(scene.TerrainVisual.Placements.Count, Is.GreaterThan(0));
            Assert.That(scene.TerrainVisual.Placements.Any(placement =>
                placement.Layer == RuntimeDungeonTilemapLayer.Void &&
                placement.RuleTilePath == "Assets/Res/Tile/TestVoidWall.asset" &&
                placement.HeightSteps == -1), Is.True);
            Assert.That(scene.TerrainVisual.Placements.Any(placement =>
                placement.Layer == RuntimeDungeonTilemapLayer.Obstacle &&
                placement.RuleTilePath == "Assets/Res/Tile/TestObstacleWall.asset" &&
                placement.HeightSteps == 2), Is.True);
            Assert.That(scene.TerrainVisual.Placements, Has.Count.EqualTo(sourceVisual.RuleTilePlacements.Count));
            for (int index = 0; index < sourceVisual.RuleTilePlacements.Count; index++)
            {
                OpenFieldRuleTilePlacement source = sourceVisual.RuleTilePlacements[index];
                RuntimeDungeonRuleTilePlacement output = scene.TerrainVisual.Placements[index];
                Assert.That(output.Layer, Is.EqualTo((RuntimeDungeonTilemapLayer)source.Layer));
                Assert.That(output.RuleTilePath, Is.EqualTo(source.RuleTile.AssetPath));
                Assert.That(output.Cell, Is.EqualTo(source.Cell));
                Assert.That(output.HeightSteps, Is.EqualTo(source.HeightSteps));
            }

            Assert.That(scene.ObstacleSpawns, Is.Not.Empty);
            Assert.That(scene.ObstacleSpawns, Has.Count.EqualTo(sourceVisual.Obstacles.Count));
            Assert.That(scene.ObstacleSpawns.All(spawn => spawn.CollisionCells.Count == 1), Is.True);
            for (int index = 0; index < sourceVisual.Obstacles.Count; index++)
            {
                OpenFieldObstaclePlacement source = sourceVisual.Obstacles[index];
                RuntimeDungeonObstacleSpawnData output = scene.ObstacleSpawns[index];
                Assert.That(output.CollisionCells, Is.EqualTo(source.CollisionCells));
                Assert.That(output.Visuals, Has.Count.EqualTo(source.VisualSprites.Count));
                for (int visualIndex = 0; visualIndex < source.VisualSprites.Count; visualIndex++)
                {
                    OpenFieldObstacleVisualSpritePlacement sourceVisualSprite = source.VisualSprites[visualIndex];
                    RuntimeDungeonObstacleVisualSpawnData outputVisual = output.Visuals[visualIndex];
                    Assert.That(outputVisual.SpritePath, Is.EqualTo(sourceVisualSprite.Sprite.AssetPath));
                    Assert.That(outputVisual.SpriteName, Is.EqualTo(sourceVisualSprite.Sprite.SpriteName));
                    Assert.That(outputVisual.SortAnchorWorldY,
                        Is.EqualTo(outputVisual.WorldPosition.y + source.VisualSortAnchor.y));
                    Assert.That(outputVisual.RotationQuarterTurns, Is.EqualTo(source.RotationQuarterTurns));
                    Assert.That(outputVisual.FlippedX, Is.EqualTo(source.FlippedX));
                    Assert.That(outputVisual.LayerIndex, Is.EqualTo(sourceVisualSprite.LayerIndex));
                    Assert.That(outputVisual.WorldPosition,
                        Is.EqualTo(GetExpectedObstacleWorldPosition(layout, source.OccupiedCells)));
                }
            }

            Assert.That(scene.ObstacleSpawns.SelectMany(spawn => spawn.CollisionCells),
                Has.None.Matches<Vector2Int>(protectedCells.Contains));
            Assert.That(scene.SceneObjects.Any(spawn => spawn.ObjectType == RuntimeDungeonSceneObjectType.Exit), Is.True);
            Assert.That(scene.SceneObjects.Any(spawn => spawn.ObjectType == RuntimeDungeonSceneObjectType.Treasure), Is.True);
        }

        private static OpenFieldDungeonLayout CreateGroundLayout(int width, int height, int seed)
        {
            OpenFieldDungeonLayout layout = new(width, height, seed);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    layout.SetTerrain(x, y, 0.5f, OpenFieldTerrainCell.Ground, 0);
            }

            return layout;
        }

        private static Vector3 GetExpectedObstacleWorldPosition(
            OpenFieldDungeonLayout layout,
            IReadOnlyList<Vector2Int> occupiedCells)
        {
            int minimumX = occupiedCells.Min(cell => cell.x);
            int maximumX = occupiedCells.Max(cell => cell.x);
            int minimumY = occupiedCells.Min(cell => cell.y);
            int maximumY = occupiedCells.Max(cell => cell.y);
            float width = maximumX - minimumX + 1;
            float height = maximumY - minimumY + 1;
            return new Vector3(
                minimumX + width * 0.5f - layout.Width * 0.5f,
                minimumY + height * 0.5f - layout.Height * 0.5f,
                0f);
        }

        private static void AssertGroundRegionHasBothStyles(
            OpenFieldDungeonVisualLayout layout,
            int minimumX,
            int maximumX,
            int height)
        {
            HashSet<int> assignedStyles = new();
            for (int y = 0; y < height; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    int styleIndex = layout.GetGroundStyleIndex(x, y);
                    Assert.That(styleIndex, Is.GreaterThanOrEqualTo(0), $"Ground cell ({x}, {y}) was not assigned a style.");
                    assignedStyles.Add(styleIndex);
                }
            }

            Assert.That(assignedStyles, Is.EquivalentTo(new[] { 0, 1 }));
        }

        private static List<Vector2Int> GetExpectedObstacleCells(
            OpenFieldObstacleData obstacle,
            Vector2Int origin,
            int rotationQuarterTurns,
            bool flippedX,
            bool collisionOnly)
        {
            List<Vector2Int> cells = new();
            int turns = ((rotationQuarterTurns % 4) + 4) % 4;
            for (int sourceY = 0; sourceY < obstacle.FootprintHeight; sourceY++)
            {
                for (int sourceX = 0; sourceX < obstacle.FootprintWidth; sourceX++)
                {
                    int maskIndex = sourceY * obstacle.FootprintWidth + sourceX;
                    if (collisionOnly && !obstacle.CollisionMask[maskIndex])
                        continue;

                    int localX = flippedX ? obstacle.FootprintWidth - 1 - sourceX : sourceX;
                    Vector2Int rotatedCell = turns switch
                    {
                        0 => new Vector2Int(localX, sourceY),
                        1 => new Vector2Int(obstacle.FootprintHeight - 1 - sourceY, localX),
                        2 => new Vector2Int(obstacle.FootprintWidth - 1 - localX, obstacle.FootprintHeight - 1 - sourceY),
                        3 => new Vector2Int(sourceY, obstacle.FootprintWidth - 1 - localX),
                        _ => throw new System.ArgumentOutOfRangeException(nameof(rotationQuarterTurns)),
                    };
                    cells.Add(origin + rotatedCell);
                }
            }

            return cells;
        }

        private static void AssertLayoutsAreIdentical(
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualLayout expected,
            OpenFieldDungeonVisualLayout actual)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (layout.GetTerrainCell(x, y) == OpenFieldTerrainCell.Ground)
                    {
                        Assert.That(actual.GetGroundStyleIndex(x, y),
                            Is.EqualTo(expected.GetGroundStyleIndex(x, y)),
                            $"Ground style differed at ({x}, {y}).");
                    }
                }
            }

            Assert.That(actual.RuleTilePlacements, Has.Count.EqualTo(expected.RuleTilePlacements.Count));
            for (int index = 0; index < expected.RuleTilePlacements.Count; index++)
            {
                OpenFieldRuleTilePlacement expectedPlacement = expected.RuleTilePlacements[index];
                OpenFieldRuleTilePlacement actualPlacement = actual.RuleTilePlacements[index];
                Assert.That(actualPlacement.Role, Is.EqualTo(expectedPlacement.Role), $"Rule tile role differed at index {index}.");
                Assert.That(actualPlacement.Layer, Is.EqualTo(expectedPlacement.Layer), $"Rule tile layer differed at index {index}.");
                Assert.That(actualPlacement.Cell, Is.EqualTo(expectedPlacement.Cell), $"Rule tile cell differed at index {index}.");
                Assert.That(actualPlacement.HeightSteps, Is.EqualTo(expectedPlacement.HeightSteps), $"Rule tile height differed at index {index}.");
                Assert.That(actualPlacement.GroundStyleIndex, Is.EqualTo(expectedPlacement.GroundStyleIndex), $"Rule tile style differed at index {index}.");
                Assert.That(actualPlacement.DecorationIndex, Is.EqualTo(expectedPlacement.DecorationIndex), $"Rule tile decoration differed at index {index}.");
            }

            Assert.That(actual.Obstacles, Has.Count.EqualTo(expected.Obstacles.Count));
            for (int index = 0; index < expected.Obstacles.Count; index++)
            {
                OpenFieldObstaclePlacement expectedPlacement = expected.Obstacles[index];
                OpenFieldObstaclePlacement actualPlacement = actual.Obstacles[index];
                Assert.That(actualPlacement.GroundStyleIndex, Is.EqualTo(expectedPlacement.GroundStyleIndex), $"Obstacle style differed at index {index}.");
                Assert.That(actualPlacement.ObstacleIndex, Is.EqualTo(expectedPlacement.ObstacleIndex), $"Obstacle definition differed at index {index}.");
                Assert.That(actualPlacement.Origin, Is.EqualTo(expectedPlacement.Origin), $"Obstacle origin differed at index {index}.");
                Assert.That(actualPlacement.RotationQuarterTurns, Is.EqualTo(expectedPlacement.RotationQuarterTurns), $"Obstacle rotation differed at index {index}.");
                Assert.That(actualPlacement.FlippedX, Is.EqualTo(expectedPlacement.FlippedX), $"Obstacle flip differed at index {index}.");
                Assert.That(actualPlacement.OccupiedCells, Is.EqualTo(expectedPlacement.OccupiedCells), $"Obstacle footprint differed at index {index}.");
                Assert.That(actualPlacement.CollisionCells, Is.EqualTo(expectedPlacement.CollisionCells), $"Obstacle collision cells differed at index {index}.");
            }
        }
    }
}
