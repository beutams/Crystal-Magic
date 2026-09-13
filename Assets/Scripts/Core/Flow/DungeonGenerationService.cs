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
        private const int MaxGenerationAttempts = 32;

        public static IEnumerator GenerateForTransition(LoadGameContext context, string targetSceneName)
        {
            DungeonFlowTiming.BeginStage(7, "准备 DungeonRun 运行数据");
            int dungeonFloor = DungeonState.PrepareDungeonRun(context);
            DungeonRunData runData = SaveDataComponent.Instance?.GetDungeonRunData();
            if (runData == null)
            {
                DungeonFlowTiming.Fail("DungeonRunData 为空");
                yield break;
            }

            DungeonFlowTiming.EndStage(7, $"Floor={dungeonFloor}");
            DungeonFlowTiming.BeginStage(8, "读取主题、配置并确定 Seed");

            DungeonThemeData theme = ResolveThemeData(runData.ThemeId);
            if (theme == null)
            {
                string error = $"No open-field dungeon theme is configured for theme id {runData.ThemeId}.";
                PublishProgress(targetSceneName, 0.35f, "Open field configuration invalid", error);
                DungeonFlowTiming.Fail(error);
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
            bool isBossFloor = IsBossFloor(dungeonFloor);
            if (!HasConfiguredExitSquad(theme.OpenField, isBossFloor))
            {
                string error = $"Open field theme '{theme.Name}' has no valid {(isBossFloor ? "boss" : "normal")} large squad for the exit interest point.";
                PublishProgress(targetSceneName, 0.35f, "Open field configuration invalid", error);
                DungeonFlowTiming.Fail(error);
                throw new InvalidOperationException(error);
            }

            bool rebuildSavedLayout = runData.CurrentFloor == dungeonFloor && runData.Seed != 0;
            int masterSeed = rebuildSavedLayout ? runData.Seed : DeriveMasterSeed(runData, theme.Id, dungeonFloor);
            OpenFieldDungeonTerrainConfig terrainConfig = theme.OpenField.Terrain.CloneValidated();
            terrainConfig.Width = Mathf.Max(8, dungeonConfig?.MapWidth ?? terrainConfig.Width);
            terrainConfig.Height = Mathf.Max(8, dungeonConfig?.MapHeight ?? terrainConfig.Height);
            terrainConfig.EnsureValid();
            DungeonFlowTiming.EndStage(8, $"Theme={theme.Name} MasterSeed={masterSeed} Size={terrainConfig.Width}x{terrainConfig.Height}");

            for (int attemptIndex = 0; attemptIndex < MaxGenerationAttempts; attemptIndex++)
            {
                int candidateSeed = rebuildSavedLayout && attemptIndex == 0
                    ? masterSeed
                    : DeriveCandidateSeed(masterSeed, attemptIndex);
                PublishProgress(
                    targetSceneName,
                    0.35f,
                    "Generating open field terrain",
                    $"{theme.Name} Level {dungeonFloor} Attempt {attemptIndex + 1} Seed {candidateSeed}");

                DungeonFlowTiming.BeginStage(9, "生成地形", $"Attempt={attemptIndex + 1} Seed={candidateSeed}");
                OpenFieldDungeonLayout layout = OpenFieldDungeonTerrainGenerator.Generate(candidateSeed, terrainConfig);
                DungeonFlowTiming.EndStage(9, $"Cells={layout.Width}x{layout.Height}");
                yield return null;

                PublishProgress(targetSceneName, 0.52f, "Placing open field anchors", $"Attempt {attemptIndex + 1}");
                DungeonFlowTiming.BeginStage(10, "放置地牢锚点", $"Attempt={attemptIndex + 1}");
                bool anchorsPlaced = OpenFieldDungeonAnchorGenerator.TryPlace(layout, candidateSeed, theme.OpenField.Anchors);
                DungeonFlowTiming.EndStage(10, anchorsPlaced ? "成功" : "失败，重试下一个 Seed");
                if (!anchorsPlaced)
                {
                    yield return null;
                    continue;
                }

                PublishProgress(targetSceneName, 0.68f, "Placing open field content", $"Attempt {attemptIndex + 1}");
                DungeonFlowTiming.BeginStage(11, "放置地牢内容", $"Attempt={attemptIndex + 1}");
                bool contentPlaced = OpenFieldDungeonContentGenerator.TryPlace(layout, candidateSeed, theme.OpenField.Content);
                DungeonFlowTiming.EndStage(11, contentPlaced ? "成功" : "失败，重试下一个 Seed");
                if (!contentPlaced)
                {
                    yield return null;
                    continue;
                }

                DungeonFlowTiming.BeginStage(12, "构建并校验运行时场景数据", $"Attempt={attemptIndex + 1}");
                RuntimeDungeonSceneData sceneData = OpenFieldDungeonSceneDataBuilder.Build(
                    layout,
                    theme,
                    dungeonConfig,
                    dungeonFloor,
                    isBossFloor);
                if (!HasValidExitGuard(sceneData, layout.ExitInterestPoint, isBossFloor))
                {
                    DungeonFlowTiming.EndStage(12, "出口守卫无效，重试下一个 Seed");
                    PublishProgress(
                        targetSceneName,
                        0.76f,
                        "Open field candidate rejected",
                        $"Attempt {attemptIndex + 1} cannot deploy the exit guard squad; continuing with the next seed.");
                    yield return null;
                    continue;
                }
                DungeonFlowTiming.EndStage(12, "场景数据校验通过");

                DungeonFlowTiming.BeginStage(13, "保存当前运行时地牢数据");
                runData.Seed = candidateSeed;
                runData.CurrentFloor = dungeonFloor;
                RuntimeDataComponent.Instance.SetCurrentOpenFieldDungeonLayout(
                    layout,
                    sceneData,
                    dungeonFloor,
                    candidateSeed,
                    attemptIndex + 1);
                DungeonFlowTiming.EndStage(13, $"AcceptedAttempt={attemptIndex + 1} Seed={candidateSeed}");
                PublishProgress(targetSceneName, 0.84f, "Open field layout ready", $"Accepted Attempt {attemptIndex + 1} Seed {candidateSeed}");
                yield return DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine(
                    targetSceneName,
                    (progress, title, detail) => PublishProgress(targetSceneName, progress, title, detail));
                PublishProgress(targetSceneName, 0.999f, "Open field ready", $"{theme.Name} Level {dungeonFloor} Seed {candidateSeed}");
                yield break;
            }

            string generationError =
                $"Unable to generate a valid open-field dungeon after {MaxGenerationAttempts} attempts.";
            PublishProgress(targetSceneName, 0.76f, "Open field generation failed", generationError);
            DungeonFlowTiming.Fail(generationError);
            throw new InvalidOperationException(generationError);
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
                        if (member != null && !string.IsNullOrWhiteSpace(member.UnitName) && member.Cost > 0 && member.Weight > 0)
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

            foreach (RuntimeDungeonInterestPointSpawnData interestPointSpawn in sceneData.InterestPointSpawns)
            {
                if (interestPointSpawn == null || interestPointSpawn.EncounterId != exitInterestPoint.EncounterId)
                    continue;

                foreach (RuntimeDungeonMonsterSpawnData memberSpawn in interestPointSpawn.MemberSpawns)
                {
                    if (memberSpawn != null && memberSpawn.RegionId == exitInterestPoint.EncounterId && memberSpawn.IsBoss == requiresBoss)
                        return true;
                }
            }

            return false;
        }

        private static DungeonThemeData ResolveThemeData(int dungeonThemeId)
        {
            DungeonThemeData theme = DataComponent.Instance?.Get<DungeonThemeData>(dungeonThemeId);
            theme?.EnsureValid();
            return theme;
        }

        private static DungeonConfig GetDungeonConfig()
        {
            return ConfigComponent.Instance.Get<DungeonConfig>();
        }
        private static bool IsBossFloor(int dungeonFloor)
        {
            return Mathf.Clamp(dungeonFloor, 1, DungeonConfig.LevelsPerTheme) == DungeonConfig.LevelsPerTheme;
        }

        private static void PublishProgress(string targetSceneName, float progress, string title, string detail)
        {
            EventComponent.Instance?.Publish(new TransitionLoadProgressChangedEvent(
                targetSceneName,
                Mathf.Clamp01(progress),
                title ?? string.Empty,
                detail ?? string.Empty));
        }

        private static int DeriveMasterSeed(DungeonRunData runData, int dungeonThemeId, int dungeonFloor)
        {
            unchecked
            {
                uint baseSeed = (uint)(runData?.BaseSeed == 0 ? DefaultSeed : runData.BaseSeed);
                uint theme = (uint)Mathf.Max(0, dungeonThemeId);
                uint floor = (uint)Mathf.Max(1, dungeonFloor);
                uint mixed = baseSeed ^ (theme * 2246822519u) ^ (floor * 3266489917u) ^ 2246822519u;
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
