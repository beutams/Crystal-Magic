using System;
using System.Collections;
using System.Collections.Generic;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal static class DungeonGenerationService
    {
        private const int DefaultSeed = 19088743;

        public static IEnumerator GenerateForTransition(LoadGameContext context, string targetSceneName)
        {
            int dungeonFloor = DungeonState.PrepareDungeonRun(context);
            DungeonRunData runData = SaveDataComponent.Instance?.GetDungeonRunData();
            if (runData == null)
                yield break;

            DungeonThemeData theme = ResolveThemeData(dungeonFloor);
            if (theme == null)
            {
                string error = $"No open-field dungeon theme is configured for floor {dungeonFloor}.";
                PublishProgress(targetSceneName, 0.35f, "Open field configuration invalid", error);
                throw new InvalidOperationException(error);
            }

            yield return GenerateOpenFieldForTransitionCoroutine(
                runData,
                dungeonFloor,
                theme,
                GetDungeonConfig(),
                targetSceneName);
        }

        private static IEnumerator GenerateOpenFieldForTransitionCoroutine(
            DungeonRunData runData,
            int dungeonFloor,
            DungeonThemeData theme,
            DungeonConfig dungeonConfig,
            string targetSceneName)
        {
            theme.EnsureValid();
            bool isBossFloor = IsBossFloor(dungeonFloor, dungeonConfig);
            if (!HasConfiguredExitSquad(theme.OpenField, isBossFloor))
            {
                string error = $"Open field theme '{theme.Name}' has no valid {(isBossFloor ? "boss" : "normal")} large squad for the exit interest point.";
                PublishProgress(targetSceneName, 0.35f, "Open field configuration invalid", error);
                throw new InvalidOperationException(error);
            }

            bool rebuildSavedLayout = runData.CurrentFloor == dungeonFloor && runData.Seed != 0;
            int masterSeed = rebuildSavedLayout ? runData.Seed : DeriveMasterSeed(runData, dungeonFloor);
            OpenFieldDungeonTerrainConfig terrainConfig = theme.OpenField.Terrain.CloneValidated();
            terrainConfig.Width = Mathf.Max(8, dungeonConfig?.MapWidth ?? terrainConfig.Width);
            terrainConfig.Height = Mathf.Max(8, dungeonConfig?.MapHeight ?? terrainConfig.Height);
            terrainConfig.EnsureValid();

            for (int attemptIndex = 0; ; attemptIndex++)
            {
                int candidateSeed = rebuildSavedLayout && attemptIndex == 0
                    ? masterSeed
                    : DeriveCandidateSeed(masterSeed, attemptIndex);
                PublishProgress(
                    targetSceneName,
                    0.35f,
                    "Generating open field terrain",
                    $"Floor {dungeonFloor} Attempt {attemptIndex + 1} Seed {candidateSeed}");

                OpenFieldDungeonLayout layout = OpenFieldDungeonTerrainGenerator.Generate(candidateSeed, terrainConfig);
                yield return null;

                PublishProgress(targetSceneName, 0.52f, "Placing open field anchors", $"Attempt {attemptIndex + 1}");
                if (!OpenFieldDungeonAnchorGenerator.TryPlace(layout, candidateSeed, theme.OpenField.Anchors))
                {
                    yield return null;
                    continue;
                }

                PublishProgress(targetSceneName, 0.68f, "Placing open field content", $"Attempt {attemptIndex + 1}");
                if (!OpenFieldDungeonContentGenerator.TryPlace(layout, candidateSeed, theme.OpenField.Content))
                {
                    yield return null;
                    continue;
                }

                RuntimeDungeonSceneData sceneData = OpenFieldDungeonSceneDataBuilder.Build(
                    layout,
                    theme,
                    dungeonConfig,
                    dungeonFloor,
                    isBossFloor);
                if (!HasValidExitGuard(sceneData, layout.ExitInterestPoint, isBossFloor))
                {
                    PublishProgress(
                        targetSceneName,
                        0.76f,
                        "Open field candidate rejected",
                        $"Attempt {attemptIndex + 1} cannot deploy the exit guard squad; continuing with the next seed.");
                    yield return null;
                    continue;
                }

                runData.Seed = candidateSeed;
                runData.CurrentFloor = dungeonFloor;
                RuntimeDataComponent.Instance.SetCurrentOpenFieldDungeonLayout(
                    layout,
                    sceneData,
                    dungeonFloor,
                    candidateSeed,
                    attemptIndex + 1);
                PublishProgress(targetSceneName, 0.84f, "Open field layout ready", $"Accepted Attempt {attemptIndex + 1} Seed {candidateSeed}");
                yield return DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine(
                    targetSceneName,
                    (progress, title, detail) => PublishProgress(targetSceneName, progress, title, detail));
                PublishProgress(targetSceneName, 0.999f, "Open field ready", $"Floor {dungeonFloor} Seed {candidateSeed}");
                yield break;
            }
        }

        private static bool HasConfiguredExitSquad(OpenFieldDungeonThemeData data, bool requiresBoss)
        {
            foreach (OpenFieldDungeonEncounterPoolData pool in data.EncounterPools)
            {
                if (pool == null || pool.InterestSize != OpenFieldInterestSizeData.Large)
                    continue;

                foreach (OpenFieldDungeonSquadData squad in pool.Squads)
                {
                    if (squad == null || squad.IsBossSquad != requiresBoss || squad.Members == null || squad.Members.Count == 0)
                        continue;

                    foreach (OpenFieldDungeonSquadMemberData member in squad.Members)
                    {
                        if (member != null && !string.IsNullOrWhiteSpace(member.UnitName) && member.Count > 0)
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool HasValidExitGuard(
            RuntimeDungeonSceneData sceneData,
            OpenFieldInterestPoint exitInterestPoint,
            bool requiresBoss)
        {
            if (sceneData == null || exitInterestPoint == null)
                return false;

            foreach (RuntimeDungeonMonsterSpawnData spawn in sceneData.MonsterSpawns)
            {
                if (spawn != null && spawn.RegionId == exitInterestPoint.EncounterId && spawn.IsBoss == requiresBoss)
                    return true;
            }

            return false;
        }

        private static DungeonThemeData ResolveThemeData(int dungeonFloor)
        {
            IEnumerable<DungeonThemeData> themes = DataComponent.Instance?.FindAll<DungeonThemeData>(static _ => true);
            DungeonThemeData nearestTheme = null;
            int nearestDistance = int.MaxValue;
            if (themes != null)
            {
                foreach (DungeonThemeData theme in themes)
                {
                    if (theme == null)
                        continue;

                    theme.EnsureValid();
                    if (dungeonFloor >= theme.FloorStart && dungeonFloor <= theme.FloorEnd)
                        return theme;

                    int distance = dungeonFloor < theme.FloorStart
                        ? theme.FloorStart - dungeonFloor
                        : dungeonFloor - theme.FloorEnd;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestTheme = theme;
                    }
                }
            }

            return nearestTheme;
        }

        private static DungeonConfig GetDungeonConfig()
        {
            return ConfigComponent.Instance.Get<DungeonConfig>();
        }


        private static bool IsBossFloor(int dungeonFloor, DungeonConfig config)
        {
            int bossFloorInterval = Mathf.Max(1, config?.BossFloorInterval ?? 10);
            return Mathf.Max(1, dungeonFloor) % bossFloorInterval == 0;
        }

        private static void PublishProgress(string targetSceneName, float progress, string title, string detail)
        {
            EventComponent.Instance?.Publish(new TransitionLoadProgressChangedEvent(
                targetSceneName,
                Mathf.Clamp01(progress),
                title ?? string.Empty,
                detail ?? string.Empty));
        }

        private static int DeriveMasterSeed(DungeonRunData runData, int dungeonFloor)
        {
            unchecked
            {
                uint baseSeed = (uint)(runData?.BaseSeed == 0 ? DefaultSeed : runData.BaseSeed);
                uint floor = (uint)Mathf.Max(1, dungeonFloor);
                uint mixed = baseSeed ^ (floor * 3266489917u) ^ 2246822519u;
                mixed ^= mixed >> 16;
                mixed *= 2246822519u;
                mixed ^= mixed >> 13;
                mixed *= 3266489917u;
                mixed ^= mixed >> 16;
                return (int)(mixed == 0 ? (uint)DefaultSeed : mixed);
            }
        }

        private static int DeriveCandidateSeed(int masterSeed, int attemptIndex)
        {
            if (attemptIndex <= 0)
                return masterSeed;

            unchecked
            {
                uint x = (uint)masterSeed;
                uint y = (uint)attemptIndex * 747796405u + 2891336453u;
                uint z = x + 0x9E3779B9u + (y << 6) + (y >> 2);
                z ^= z >> 15;
                z *= 2246822519u;
                z ^= z >> 13;
                z *= 3266489917u;
                z ^= z >> 16;
                return (int)(z == 0 ? (uint)DefaultSeed : z);
            }
        }
    }
}