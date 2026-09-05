using System;
using System.Collections.Generic;
using System.IO;

using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal static class OpenFieldDungeonSceneDataBuilder
    {
        private const float CellWorldSize = 1f;

        public static RuntimeDungeonSceneData Build(OpenFieldDungeonLayout layout, DungeonThemeData theme, DungeonConfig dungeonConfig, int floor, bool isBossFloor)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            dungeonConfig ??= new DungeonConfig();
            dungeonConfig.EnsureValid();

            theme.EnsureValid();
            RuntimeDungeonSceneData scene = new()
            {
                ThemeId = theme.Id,
                ThemeKey = theme.ThemeKey,
                IsBossFloor = isBossFloor,
                CellWorldSize = CellWorldSize,
                DisplayWidth = layout.Width,
                DisplayHeight = layout.Height,
                PlayerSpawnWorldPosition = ToWorld(layout, layout.Entrance),
            };
            scene.TerrainVisual.CellWorldSize = CellWorldSize;
            scene.TerrainVisual.WorldOrigin = new Vector2(
                -layout.Width * CellWorldSize * 0.5f,
                -layout.Height * CellWorldSize * 0.5f);

            HashSet<Vector2Int> protectedCells = BuildProtectedCells(layout);
            OpenFieldDungeonVisualLayout visualLayout = OpenFieldDungeonVisualLayoutBuilder.Build(
                layout,
                theme.OpenField.Visual,
                protectedCells);
            AddTerrainVisual(scene, visualLayout);
            AddObstacleSpawns(scene, layout, visualLayout);

            bool[,] collisionMask = new bool[layout.Width, layout.Height];
            for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
                AddTerrain(layout, x, y, collisionMask);
            AddMergedTerrainColliders(scene, layout, collisionMask);

            if (layout.ExitInterestPoint != null)
            {
                OpenFieldInterestPoint exitPoint = layout.ExitInterestPoint;
                scene.SceneObjects.Add(new RuntimeDungeonSceneObjectSpawnData
                {
                    ObjectType = RuntimeDungeonSceneObjectType.Exit,
                    PrefabName = "Exit",
                    RegionId = exitPoint.EncounterId,
                    SourceCoordinate = ToVector2Int(exitPoint.Center),
                    DisplayCoordinate = ToVector2Int(exitPoint.Center),
                    WorldPosition = ToWorld(layout, exitPoint.Center),
                    RequiresRoomClear = true,
                    ApplyCollider = false,
                    TargetFloor = floor + 1,
                });
            }

            foreach (OpenFieldContentPlacement placement in layout.ContentPlacements)
            {
                if (placement.Type != OpenFieldContentType.Chest)
                    continue;

                scene.SceneObjects.Add(new RuntimeDungeonSceneObjectSpawnData
                {
                    ObjectType = RuntimeDungeonSceneObjectType.Treasure,
                    PrefabName = "Treasure",
                    RegionId = placement.EncounterId,
                    InterestSize = ResolveInterestSize(layout, placement.EncounterId),
                    RandomSeed = DeriveChestSeed(layout.Seed, placement.Cell),
                    SourceCoordinate = ToVector2Int(placement.Cell),
                    DisplayCoordinate = ToVector2Int(placement.Cell),
                    WorldPosition = ToWorld(layout, placement.Cell),
                });
            }

            HashSet<Vector2Int> occupiedCells = new(protectedCells);
            foreach (RuntimeDungeonObstacleSpawnData obstacle in scene.ObstacleSpawns)
            {
                foreach (Vector2Int collisionCell in obstacle.CollisionCells)
                    occupiedCells.Add(collisionCell);
            }

            AddLandmarks(scene, layout, theme.OpenField, occupiedCells);
            AddSquads(scene, layout, theme, isBossFloor, occupiedCells);
            ConfigureChestCandidates(scene, theme.OpenField.TreasureItemIds);
            return scene;
        }

        private static void AddTerrainVisual(
            RuntimeDungeonSceneData scene,
            OpenFieldDungeonVisualLayout visualLayout)
        {
            foreach (OpenFieldRuleTilePlacement placement in visualLayout.RuleTilePlacements)
            {
                scene.TerrainVisual.Placements.Add(new RuntimeDungeonRuleTilePlacement
                {
                    Layer = ToRuntimeTilemapLayer(placement.Layer),
                    Role = ToRuntimeTilemapRole(placement.Role),
                    RuleTilePath = placement.RuleTile?.AssetPath ?? string.Empty,
                    Cell = placement.Cell,
                    HeightSteps = placement.HeightSteps,
                });
            }
        }

        private static void AddObstacleSpawns(
            RuntimeDungeonSceneData scene,
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualLayout visualLayout)
        {
            foreach (OpenFieldObstaclePlacement placement in visualLayout.Obstacles)
            {
                OpenFieldSpriteReferenceData sprite = placement.Sprite;
                Vector3 worldPosition = ToWorldRectangle(layout, placement.OccupiedCells);
                scene.ObstacleSpawns.Add(new RuntimeDungeonObstacleSpawnData
                {
                    SpritePath = sprite?.AssetPath ?? string.Empty,
                    SpriteName = sprite?.SpriteName ?? string.Empty,
                    SpriteUv = sprite?.SpriteUv ?? default,
                    HasSpriteUv = sprite?.HasSpriteUv ?? false,
                    WorldPosition = worldPosition,
                    VisualSortAnchor = placement.VisualSortAnchor,
                    SortAnchorWorldY = worldPosition.y + placement.VisualSortAnchor.y,
                    RotationQuarterTurns = placement.RotationQuarterTurns,
                    FlippedX = placement.FlippedX,
                    CollisionCells = new List<Vector2Int>(placement.CollisionCells),
                });
            }
        }

        private static RuntimeDungeonTilemapLayer ToRuntimeTilemapLayer(OpenFieldRuleTileLayer layer)
        {
            return layer switch
            {
                OpenFieldRuleTileLayer.Void => RuntimeDungeonTilemapLayer.Void,
                OpenFieldRuleTileLayer.Ground => RuntimeDungeonTilemapLayer.Ground,
                OpenFieldRuleTileLayer.Decoration => RuntimeDungeonTilemapLayer.Decoration,
                OpenFieldRuleTileLayer.Obstacle => RuntimeDungeonTilemapLayer.Obstacle,
                _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
            };
        }

        private static RuntimeDungeonTilemapRole ToRuntimeTilemapRole(OpenFieldRuleTileRole role)
        {
            return role switch
            {
                OpenFieldRuleTileRole.Abyss => RuntimeDungeonTilemapRole.Abyss,
                OpenFieldRuleTileRole.VoidWall => RuntimeDungeonTilemapRole.VoidWall,
                OpenFieldRuleTileRole.VoidTransition => RuntimeDungeonTilemapRole.VoidTransition,
                OpenFieldRuleTileRole.GroundBase => RuntimeDungeonTilemapRole.GroundBase,
                OpenFieldRuleTileRole.Decoration => RuntimeDungeonTilemapRole.Decoration,
                OpenFieldRuleTileRole.ObstacleTop => RuntimeDungeonTilemapRole.ObstacleTop,
                OpenFieldRuleTileRole.ObstacleWall => RuntimeDungeonTilemapRole.ObstacleWall,
                OpenFieldRuleTileRole.ObstacleTransition => RuntimeDungeonTilemapRole.ObstacleTransition,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
            };
        }

        private static void AddTerrain(OpenFieldDungeonLayout layout, int x, int y, bool[,] collisionMask)
        {
            collisionMask[x, y] = layout.GetTerrainCell(x, y) != OpenFieldTerrainCell.Ground;
        }

        private static void AddMergedTerrainColliders(
            RuntimeDungeonSceneData scene,
            OpenFieldDungeonLayout layout,
            bool[,] collisionMask)
        {
            bool[,] used = new bool[layout.Width, layout.Height];
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (!collisionMask[x, y] || used[x, y])
                        continue;

                    int width = 0;
                    while (x + width < layout.Width && collisionMask[x + width, y] && !used[x + width, y])
                        width++;

                    int height = 1;
                    bool canGrow = true;
                    while (y + height < layout.Height && canGrow)
                    {
                        for (int offsetX = 0; offsetX < width; offsetX++)
                        {
                            if (!collisionMask[x + offsetX, y + height] || used[x + offsetX, y + height])
                            {
                                canGrow = false;
                                break;
                            }
                        }

                        if (canGrow)
                            height++;
                    }

                    for (int offsetY = 0; offsetY < height; offsetY++)
                    for (int offsetX = 0; offsetX < width; offsetX++)
                        used[x + offsetX, y + offsetY] = true;

                    scene.EnvironmentSpawns.Add(new RuntimeDungeonEnvironmentSpawnData
                    {
                        PrefabName = "Collider",
                        WorldPosition = ToWorldRectangle(layout, x, y, width, height),
                        Size = new Vector3(width * CellWorldSize, height * CellWorldSize, 1.6f),
                        ApplyCollider = true,
                        HideVisual = true,
                    });
                }
            }
        }
        private static void AddSquads(RuntimeDungeonSceneData scene, OpenFieldDungeonLayout layout, DungeonThemeData theme, bool isBossFloor, HashSet<Vector2Int> occupiedCells)
        {
            foreach (OpenFieldContentPlacement placement in layout.ContentPlacements)
            {
                if (placement.Type != OpenFieldContentType.InterestSquad && placement.Type != OpenFieldContentType.WildSquad)
                    continue;

                OpenFieldInterestPoint point = FindInterestPoint(layout, placement.EncounterId);
                OpenFieldInterestSizeData size = point == null
                    ? OpenFieldInterestSizeData.Small
                    : (OpenFieldInterestSizeData)point.Size;
                bool requireBoss = point != null && point.IsExitInterestPoint && isBossFloor;
                OpenFieldDungeonSquadData squad = SelectSquad(theme.OpenField, size, requireBoss, layout.Seed, placement.SquadId);
                if (squad == null)
                    continue;

                System.Random random = new(layout.Seed ^ (placement.SquadId * 486187739));
                int localIndex = 0;
                foreach (OpenFieldDungeonSquadMemberData member in squad.Members)
                {
                    UnitData unit = DataComponent.Instance?.Find<UnitData>(row => row.Name == member.UnitName);
                    if (unit == null || string.IsNullOrWhiteSpace(unit.PrefabPath))
                        continue;

                    UnitDungeonFootprintModuleData footprint = unit.GetModule<UnitDungeonFootprintModuleData>();
                    int footprintWidth = Mathf.Max(1, footprint?.Width ?? 1);
                    int footprintHeight = Mathf.Max(1, footprint?.Height ?? 1);
                    int count = Mathf.Max(1, member.Count);
                    for (int i = 0; i < count; i++)
                    {
                        if (!TryReserveMemberPosition(
                                layout,
                                placement.Cell,
                                squad,
                                footprintWidth,
                                footprintHeight,
                                occupiedCells,
                                random,
                                out OpenFieldGridPosition cell))
                        {
                            Debug.LogWarning($"[OpenFieldDungeonSceneDataBuilder] Squad '{squad.Name}' cannot fit all configured members in its {squad.Width}x{squad.Height} deployment area.");
                            continue;
                        }

                        Vector3 worldPosition = ToWorld(layout, cell) + new Vector3(
                            (footprintWidth - 1) * CellWorldSize * 0.5f,
                            (footprintHeight - 1) * CellWorldSize * 0.5f,
                            0f);
                        scene.MonsterSpawns.Add(new RuntimeDungeonMonsterSpawnData
                        {
                            RegionId = placement.EncounterId,
                            SquadId = placement.SquadId,
                            TileIndex = localIndex++,
                            Level = squad.MonsterLevel,
                            IsBoss = squad.IsBossSquad,
                            PrefabName = Path.GetFileNameWithoutExtension(unit.PrefabPath),
                            SourceCoordinate = ToVector2Int(cell),
                            DisplayCoordinate = ToVector2Int(cell),
                            WorldPosition = worldPosition,
                        });
                    }
                }
            }
        }

        private static void AddLandmarks(
            RuntimeDungeonSceneData scene,
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonThemeData data,
            HashSet<Vector2Int> occupiedCells)
        {
            if (data.Landmarks.Count == 0)
                return;

            System.Random random = new(layout.Seed ^ 0x7A90311D);
            foreach (OpenFieldInterestPoint point in layout.InterestPoints)
            {
                OpenFieldDungeonLandmarkEntryData entry = SelectLandmark(data.Landmarks, random);
                if (entry == null || string.IsNullOrWhiteSpace(entry.PrefabName))
                    continue;

                int count = random.Next(entry.MinInstances, entry.MaxInstances + 1);
                for (int index = 0; index < count; index++)
                {
                    if (!TryReserveInterestPointPosition(
                            layout,
                            point,
                            entry.FootprintWidth,
                            entry.FootprintHeight,
                            occupiedCells,
                            random,
                            out OpenFieldGridPosition position))
                    {
                        break;
                    }

                    Vector3 worldPosition = ToWorld(layout, position) + new Vector3(
                        (entry.FootprintWidth - 1) * CellWorldSize * 0.5f,
                        (entry.FootprintHeight - 1) * CellWorldSize * 0.5f,
                        0f);
                    scene.EnvironmentSpawns.Add(new RuntimeDungeonEnvironmentSpawnData
                    {
                        PrefabName = entry.PrefabName,
                        WorldPosition = worldPosition,
                        Size = new Vector3(entry.FootprintWidth, entry.FootprintHeight, 1f),
                        ApplyCollider = entry.ApplyCollider,
                    });
                }
            }
        }

        private static OpenFieldDungeonLandmarkEntryData SelectLandmark(
            List<OpenFieldDungeonLandmarkEntryData> entries,
            System.Random random)
        {
            int totalWeight = 0;
            foreach (OpenFieldDungeonLandmarkEntryData entry in entries)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.PrefabName))
                    totalWeight += Mathf.Max(1, entry.Weight);

            if (totalWeight == 0)
                return null;

            int roll = random.Next(totalWeight);
            foreach (OpenFieldDungeonLandmarkEntryData entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.PrefabName))
                    continue;

                roll -= Mathf.Max(1, entry.Weight);
                if (roll < 0)
                    return entry;
            }

            return null;
        }

        private static bool TryReserveInterestPointPosition(
            OpenFieldDungeonLayout layout,
            OpenFieldInterestPoint point,
            int width,
            int height,
            HashSet<Vector2Int> occupiedCells,
            System.Random random,
            out OpenFieldGridPosition position)
        {
            int radius = Mathf.Max(1, point.Radius - 1);
            for (int attempt = 0; attempt < 128; attempt++)
            {
                OpenFieldGridPosition candidate = new(
                    random.Next(point.Center.X - radius, point.Center.X + radius + 1),
                    random.Next(point.Center.Y - radius, point.Center.Y + radius + 1));
                int offsetX = candidate.X - point.Center.X;
                int offsetY = candidate.Y - point.Center.Y;
                if (offsetX * offsetX + offsetY * offsetY > radius * radius
                    || !CanReserveFootprint(layout, candidate, width, height, occupiedCells))
                {
                    continue;
                }

                ReserveFootprint(candidate, width, height, occupiedCells);
                position = candidate;
                return true;
            }

            position = default;
            return false;
        }

        private static OpenFieldInterestPoint FindInterestPoint(OpenFieldDungeonLayout layout, int encounterId)
        {
            foreach (OpenFieldInterestPoint point in layout.InterestPoints)
                if (point.EncounterId == encounterId)
                    return point;
            return null;
        }

        private static HashSet<Vector2Int> BuildProtectedCells(OpenFieldDungeonLayout layout)
        {
            HashSet<Vector2Int> occupied = new();
            if (layout.HasEntrance)
                occupied.Add(ToVector2Int(layout.Entrance));
            if (layout.ExitInterestPoint != null)
                occupied.Add(ToVector2Int(layout.ExitInterestPoint.Center));

            foreach (OpenFieldContentPlacement placement in layout.ContentPlacements)
                if (placement.Type == OpenFieldContentType.Chest)
                    occupied.Add(ToVector2Int(placement.Cell));
            return occupied;
        }

        private static bool TryReserveMemberPosition(
            OpenFieldDungeonLayout layout,
            OpenFieldGridPosition squadCenter,
            OpenFieldDungeonSquadData squad,
            int footprintWidth,
            int footprintHeight,
            HashSet<Vector2Int> occupiedCells,
            System.Random random,
            out OpenFieldGridPosition position)
        {
            int minX = squadCenter.X - squad.Width / 2;
            int minY = squadCenter.Y - squad.Height / 2;
            int maxX = minX + squad.Width - footprintWidth;
            int maxY = minY + squad.Height - footprintHeight;
            if (maxX < minX || maxY < minY)
            {
                position = default;
                return false;
            }

            const int placementAttempts = 128;
            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                OpenFieldGridPosition candidate = new(
                    random.Next(minX, maxX + 1),
                    random.Next(minY, maxY + 1));
                if (!CanReserveFootprint(layout, candidate, footprintWidth, footprintHeight, occupiedCells))
                    continue;

                ReserveFootprint(candidate, footprintWidth, footprintHeight, occupiedCells);
                position = candidate;
                return true;
            }

            position = default;
            return false;
        }

        private static bool CanReserveFootprint(
            OpenFieldDungeonLayout layout,
            OpenFieldGridPosition position,
            int width,
            int height,
            HashSet<Vector2Int> occupiedCells)
        {
            for (int y = position.Y; y < position.Y + height; y++)
            for (int x = position.X; x < position.X + width; x++)
            {
                Vector2Int cell = new(x, y);
                if (!layout.IsWalkable(x, y) || occupiedCells.Contains(cell))
                    return false;
            }

            return true;
        }

        private static void ReserveFootprint(OpenFieldGridPosition position, int width, int height, HashSet<Vector2Int> occupiedCells)
        {
            for (int y = position.Y; y < position.Y + height; y++)
            for (int x = position.X; x < position.X + width; x++)
                occupiedCells.Add(new Vector2Int(x, y));
        }

        private static OpenFieldDungeonSquadData SelectSquad(
            OpenFieldDungeonThemeData data,
            OpenFieldInterestSizeData size,
            bool requireBoss,
            int seed,
            int squadId)
        {
            OpenFieldDungeonEncounterPoolData pool = null;
            foreach (OpenFieldDungeonEncounterPoolData candidate in data.EncounterPools)
            {
                if (candidate != null && candidate.InterestSize == size)
                {
                    pool = candidate;
                    break;
                }
            }

            if (pool == null)
                return null;

            List<OpenFieldDungeonSquadData> choices = new();
            int totalWeight = 0;
            foreach (OpenFieldDungeonSquadData squad in pool.Squads)
            {
                if (squad == null || (requireBoss ? !squad.IsBossSquad : squad.IsBossSquad))
                    continue;

                choices.Add(squad);
                totalWeight += Mathf.Max(1, squad.Weight);
            }

            if (choices.Count == 0)
                return null;

            int roll = new System.Random(seed ^ squadId).Next(totalWeight);
            foreach (OpenFieldDungeonSquadData squad in choices)
            {
                roll -= Mathf.Max(1, squad.Weight);
                if (roll < 0)
                    return squad;
            }

            return choices[0];
        }

        private static void ConfigureChestCandidates(
            RuntimeDungeonSceneData scene,
            List<int> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
                return;

            foreach (RuntimeDungeonSceneObjectSpawnData chest in scene.SceneObjects)
            {
                if (chest.ObjectType != RuntimeDungeonSceneObjectType.Treasure)
                    continue;

                foreach (int itemId in itemIds)
                {
                    if (itemId >= 0 && !chest.TreasureCandidateItemIds.Contains(itemId))
                        chest.TreasureCandidateItemIds.Add(itemId);
                }
            }
        }

        private static byte ResolveInterestSize(OpenFieldDungeonLayout layout, int encounterId)
        {
            foreach (OpenFieldInterestPoint point in layout.InterestPoints)
            {
                if (point.EncounterId == encounterId)
                    return (byte)point.Size;
            }

            return (byte)OpenFieldInterestSize.Small;
        }

        private static uint DeriveChestSeed(int layoutSeed, OpenFieldGridPosition cell)
        {
            unchecked
            {
                uint seed = (uint)layoutSeed;
                seed ^= (uint)(cell.X * 73856093);
                seed ^= (uint)(cell.Y * 19349663);
                return seed == 0 ? 1u : seed;
            }
        }
        private static Vector3 ToWorldRectangle(OpenFieldDungeonLayout layout, int x, int y, int width, int height)
        {
            return new Vector3(
                (x + width * 0.5f - layout.Width * 0.5f) * CellWorldSize,
                (y + height * 0.5f - layout.Height * 0.5f) * CellWorldSize,
                0f);
        }

        private static Vector3 ToWorldRectangle(
            OpenFieldDungeonLayout layout,
            IReadOnlyList<Vector2Int> occupiedCells)
        {
            if (occupiedCells == null || occupiedCells.Count == 0)
                return Vector3.zero;

            int minimumX = occupiedCells[0].x;
            int maximumX = occupiedCells[0].x;
            int minimumY = occupiedCells[0].y;
            int maximumY = occupiedCells[0].y;
            for (int index = 1; index < occupiedCells.Count; index++)
            {
                Vector2Int cell = occupiedCells[index];
                minimumX = Mathf.Min(minimumX, cell.x);
                maximumX = Mathf.Max(maximumX, cell.x);
                minimumY = Mathf.Min(minimumY, cell.y);
                maximumY = Mathf.Max(maximumY, cell.y);
            }

            return ToWorldRectangle(
                layout,
                minimumX,
                minimumY,
                maximumX - minimumX + 1,
                maximumY - minimumY + 1);
        }

        private static Vector3 ToWorld(OpenFieldDungeonLayout layout, OpenFieldGridPosition position)
        {
            return new Vector3(
                (position.X + 0.5f - layout.Width * 0.5f) * CellWorldSize,
                (position.Y + 0.5f - layout.Height * 0.5f) * CellWorldSize,
                0f);
        }
        private static Vector2Int ToVector2Int(OpenFieldGridPosition position) => new(position.X, position.Y);
    }
}
