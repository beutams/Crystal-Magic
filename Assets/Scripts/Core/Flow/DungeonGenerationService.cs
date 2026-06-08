using System;
using System.Collections;
using System.Collections.Generic;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.MapDemo;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal static class DungeonGenerationService
    {
        private const int StepBatchSize = 32;
        private const float ProgressReportIntervalSeconds = 0.05f;
        private const float FloorVisualDepth = 0.1f;
        private const float FloorVisualZ = 0.85f;
        private const float WallVisualDepth = 1.6f;
        private const float WallVisualZ = 0.8f;

        private static readonly DungeonGenerationRules DefaultRules = new();

        public static IEnumerator GenerateForTransition(LoadGameContext context, string targetSceneName)
        {
            int dungeonFloor = DungeonState.PrepareDungeonRun(context);
            DungeonRunData runData = SaveDataComponent.Instance?.GetDungeonRunData();
            if (runData == null)
                yield break;

            DungeonConfig dungeonConfig = GetDungeonConfig();
            DungeonThemeData theme = ResolveThemeData(dungeonFloor);
            bool isBossFloor = IsBossFloor(dungeonFloor, dungeonConfig);
            DungeonMakerTunnelingConfig config = BuildConfig(dungeonFloor);
            ValidateConfig(config);

            if (runData.CurrentFloor == dungeonFloor && runData.Seed != 0)
            {
                if (isBossFloor)
                {
                    yield return GenerateSingleBossLayoutCoroutine(
                        dungeonFloor,
                        runData.Seed,
                        dungeonConfig,
                        theme,
                        targetSceneName,
                        "Rebuilding boss room",
                        "Reusing saved boss room seed");
                }
                else
                {
                    yield return GenerateSingleAcceptedLayoutCoroutine(
                        dungeonFloor,
                        runData.Seed,
                        config,
                        dungeonConfig,
                        theme,
                        targetSceneName,
                        "Rebuilding dungeon layout",
                        "Reusing saved qualified seed");
                }

                yield break;
            }

            int masterSeed = DeriveMasterSeed(runData, context, dungeonFloor);
            if (isBossFloor)
            {
                PublishProgress(
                    targetSceneName,
                    0.35f,
                    "Building boss room",
                    $"Floor {dungeonFloor} Seed {masterSeed}");

                GeneratedDungeonPayload bossPayload = BuildBossFloorPayload(dungeonFloor, masterSeed, dungeonConfig, theme);
                runData.Seed = masterSeed;
                runData.CurrentFloor = dungeonFloor;
                RuntimeDataComponent.Instance.SetCurrentDungeonLayout(bossPayload.Layout, bossPayload.SceneData, dungeonFloor, masterSeed, 1);
                yield return DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine(
                    targetSceneName,
                    (progress, title, detail) => PublishProgress(targetSceneName, progress, title, detail));
                PublishProgress(
                    targetSceneName,
                    0.999f,
                    "Boss room ready",
                    $"Boss floor {dungeonFloor} Seed {masterSeed}");
                yield break;
            }

            PublishProgress(
                targetSceneName,
                0.28f,
                "Searching dungeon layout",
                $"Floor {dungeonFloor} MasterSeed {masterSeed}");

            int attemptIndex = 0;
            while (true)
            {
                int candidateSeed = DeriveCandidateSeed(masterSeed, attemptIndex);
                CandidateGenerationContext candidateContext = new();
                yield return GenerateCandidateCoroutine(
                    candidateSeed,
                    config,
                    attemptIndex,
                    targetSceneName,
                    candidateContext);

                if (IsMapQualified(candidateContext.Result.Stats, DefaultRules))
                {
                    runData.Seed = candidateSeed;
                    runData.CurrentFloor = dungeonFloor;
                    RuntimeDungeonSceneData sceneData = BuildSceneData(candidateContext.Result, dungeonFloor, theme, dungeonConfig);
                    RuntimeDataComponent.Instance.SetCurrentDungeonLayout(candidateContext.Result, sceneData, dungeonFloor, candidateSeed, attemptIndex + 1);
                    yield return DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine(
                        targetSceneName,
                        (progress, title, detail) => PublishProgress(targetSceneName, progress, title, detail));
                    PublishProgress(
                        targetSceneName,
                        0.999f,
                        "Dungeon layout ready",
                        $"Accepted attempt {attemptIndex + 1} Seed {candidateSeed}");
                    yield break;
                }

                attemptIndex++;
                PublishProgress(
                    targetSceneName,
                    0.32f,
                    "Searching dungeon layout",
                    $"Attempt {attemptIndex} rejected, continuing search");
                yield return null;
            }
        }

        private static IEnumerator GenerateSingleAcceptedLayoutCoroutine(
            int dungeonFloor,
            int acceptedSeed,
            DungeonMakerTunnelingConfig config,
            DungeonConfig dungeonConfig,
            DungeonThemeData theme,
            string targetSceneName,
            string title,
            string detail)
        {
            CandidateGenerationContext acceptedContext = new();
            yield return GenerateCandidateCoroutine(
                acceptedSeed,
                config,
                0,
                targetSceneName,
                acceptedContext,
                title,
                detail);

            RuntimeDungeonSceneData sceneData = BuildSceneData(acceptedContext.Result, dungeonFloor, theme, dungeonConfig);
            RuntimeDataComponent.Instance.SetCurrentDungeonLayout(acceptedContext.Result, sceneData, dungeonFloor, acceptedSeed, 1);
            yield return DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine(
                targetSceneName,
                (progress, progressTitle, progressDetail) => PublishProgress(targetSceneName, progress, progressTitle, progressDetail));
            PublishProgress(
                targetSceneName,
                0.999f,
                "Dungeon layout ready",
                $"Seed {acceptedSeed} rebuilt successfully");
        }

        private static IEnumerator GenerateSingleBossLayoutCoroutine(
            int dungeonFloor,
            int seed,
            DungeonConfig dungeonConfig,
            DungeonThemeData theme,
            string targetSceneName,
            string title,
            string detail)
        {
            PublishProgress(targetSceneName, 0.4f, title, detail);
            yield return null;

            GeneratedDungeonPayload payload = BuildBossFloorPayload(dungeonFloor, seed, dungeonConfig, theme);
            RuntimeDataComponent.Instance.SetCurrentDungeonLayout(payload.Layout, payload.SceneData, dungeonFloor, seed, 1);
            yield return DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine(
                targetSceneName,
                (progress, progressTitle, progressDetail) => PublishProgress(targetSceneName, progress, progressTitle, progressDetail));
            PublishProgress(
                targetSceneName,
                0.999f,
                "Boss room ready",
                $"Seed {seed} rebuilt successfully");
        }

        private static IEnumerator GenerateCandidateCoroutine(
            int candidateSeed,
            DungeonMakerTunnelingConfig config,
            int attemptIndex,
            string targetSceneName,
            CandidateGenerationContext context,
            string title = null,
            string detail = null)
        {
            DungeonMakerTunnelingGenerator.Stepper stepper = new(candidateSeed, config);
            float nextReportTime = Time.realtimeSinceStartup;

            while (stepper.HasMoreBuilders)
            {
                for (int i = 0; i < StepBatchSize && stepper.HasMoreBuilders; i++)
                {
                    stepper.StepOnce();
                }

                float now = Time.realtimeSinceStartup;
                if (now >= nextReportTime)
                {
                    string progressTitle = string.IsNullOrWhiteSpace(title) ? "Generating dungeon layout" : title;
                    string progressDetail = string.IsNullOrWhiteSpace(detail)
                        ? $"Attempt {attemptIndex + 1} Seed {candidateSeed} Gen {stepper.ActiveGeneration} Builders {stepper.LiveBuilderCount}"
                        : $"{detail} Gen {stepper.ActiveGeneration} Builders {stepper.LiveBuilderCount}";
                    PublishProgress(
                        targetSceneName,
                        EstimateCandidateProgress(stepper.ActiveGeneration, stepper.LiveBuilderCount),
                        progressTitle,
                        progressDetail);
                    nextReportTime = now + ProgressReportIntervalSeconds;
                }

                yield return null;
            }

            DungeonMakerTunnelingResult rawResult = stepper.BuildResult();
            DungeonMakerTunnelingResult postProcessed = PostProcessCandidateResult(rawResult, DefaultRules);
            context.Result = FinalizeGeneratedLayout(postProcessed, DefaultRules);
        }

        private static float EstimateCandidateProgress(int activeGeneration, int liveBuilderCount)
        {
            float generationProgress = 1f - Mathf.Exp(-Mathf.Max(0, activeGeneration) / 10f);
            float builderProgress = liveBuilderCount <= 0 ? 1f : 1f / (1f + liveBuilderCount * 0.08f);
            float combined = generationProgress * 0.65f + builderProgress * 0.35f;
            return Mathf.Lerp(0.35f, 0.92f, Mathf.Clamp01(combined));
        }

        private static void PublishProgress(string targetSceneName, float progress, string title, string detail)
        {
            EventComponent.Instance?.Publish(new TransitionLoadProgressChangedEvent(
                targetSceneName,
                Mathf.Clamp01(progress),
                title ?? string.Empty,
                detail ?? string.Empty));
        }

        private static int DeriveMasterSeed(DungeonRunData runData, LoadGameContext context, int dungeonFloor)
        {
            unchecked
            {
                uint timestamp = (uint)runData.RunTimestamp;
                uint timestampHigh = (uint)(runData.RunTimestamp >> 32);
                uint floor = (uint)Mathf.Max(1, dungeonFloor);
                uint saveIndex = (uint)Mathf.Max(0, context?.SaveIndex ?? 0);
                uint mixed = timestamp ^ (timestampHigh * 2246822519u) ^ (floor * 3266489917u) ^ (saveIndex * 668265263u);
                int result = (int)(mixed == 0 ? (uint)DungeonMakerTunnelingGenerator.DefaultSeed : mixed);
                return result;
            }
        }

        private static DungeonMakerTunnelingConfig BuildConfig(int dungeonFloor)
        {
            DungeonMakerTunnelingConfig config = DungeonMakerTunnelingConfig.CreateDefault();
            config.MaxSmallDungeonRooms = Mathf.Max(12, config.MaxSmallDungeonRooms + Mathf.Max(0, dungeonFloor - 1));
            config.MaxMediumDungeonRooms = Mathf.Max(6, config.MaxMediumDungeonRooms + Mathf.Max(0, dungeonFloor / 3));
            config.MaxLargeDungeonRooms = Mathf.Max(1, config.MaxLargeDungeonRooms + Mathf.Max(0, dungeonFloor / 5));
            return config;
        }

        private static void ValidateConfig(DungeonMakerTunnelingConfig config)
        {
            config ??= DungeonMakerTunnelingConfig.CreateDefault();
            config.DimX = Mathf.Max(3, config.DimX);
            config.DimY = Mathf.Max(3, config.DimY);
            config.MaxRoomSize = Mathf.Max(1, config.MaxRoomSize);
            config.MinSmallRoomSize = Mathf.Clamp(config.MinSmallRoomSize, 1, config.MaxRoomSize);
            config.MinMediumRoomSize = Mathf.Clamp(config.MinMediumRoomSize, config.MinSmallRoomSize, config.MaxRoomSize);
            config.MinLargeRoomSize = Mathf.Clamp(config.MinLargeRoomSize, config.MinMediumRoomSize, config.MaxRoomSize);
            config.RoomAspectRatio = Mathf.Max(0.01f, (float)config.RoomAspectRatio);
            config.BabyDelayProbsTunneler ??= new List<int>();
            config.BabyDelayProbsRoomie ??= new List<int>();
            config.MaxAgesT ??= new List<int>();
            config.RoomSizeProbS ??= new List<DungeonMakerTripleInt>();
            config.RoomSizeProbB ??= new List<DungeonMakerTripleInt>();
            config.JoinPref ??= new List<int>();
            config.SizeUpProb ??= new List<int>();
            config.SizeDownProb ??= new List<int>();
            config.AnteRoomProb ??= new List<int>();
            config.Tunnelers ??= Array.Empty<DungeonMakerTunnelerSeedData>();
        }

        private static DungeonMakerTunnelingResult PostProcessCandidateResult(
            DungeonMakerTunnelingResult result,
            DungeonGenerationRules rules)
        {
            if (!rules.PruneDeadEnds)
                return result;

            DungeonMakerSquareData[] map = CopySourceMap(result);
            DungeonMakerTileOrigin[] origins = CopySourceOrigins(result);
            DungeonMakerSkeletonKind[] skeletonKinds = CopySourceSkeletonKinds(result);
            bool[] removedDeadEndMask = new bool[map.Length];
            int prunedDeadEndTiles = 0;

            PruneDeadEndCorridorsByLogicalSkeleton(
                result,
                map,
                skeletonKinds,
                result.SourceWidth,
                result.SourceHeight,
                removedDeadEndMask,
                ref prunedDeadEndTiles);

            if (prunedDeadEndTiles == 0)
                return result;

            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            DungeonMakerRegion[] copiedRegions = new DungeonMakerRegion[regions.Count];
            for (int i = 0; i < regions.Count; i++)
                copiedRegions[i] = regions[i];

            return new DungeonMakerTunnelingResult(
                result.SourceWidth,
                result.SourceHeight,
                result.Seed,
                map,
                origins,
                skeletonKinds,
                copiedRegions,
                CopySkeletonSegments(result),
                CopySkeletonLinks(result),
                CopySkeletonAttachments(result),
                BuildStatsFromMap(result, map));
        }

        private static DungeonMakerTunnelingResult FinalizeGeneratedLayout(
            DungeonMakerTunnelingResult result,
            DungeonGenerationRules rules)
        {
            DungeonMakerTunnelingResult finalized = AssignSpecialRooms(result);
            if (rules.SpawnEncounters)
                finalized = AssignEncounterMarkers(finalized, rules);

            return finalized;
        }

        private static DungeonMakerTunnelingResult AssignSpecialRooms(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            if (regions.Count == 0)
                return result;

            DungeonMakerSquareData[] map = CopySourceMap(result);
            List<int> smallRoomIndices = new();
            List<int> largeRoomIndices = new();
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                if (region.Kind != DungeonMakerRegionKind.Room || !RegionHasWalkableTile(region, map))
                    continue;

                switch (region.RoomSizeClass)
                {
                    case DungeonMakerRoomSizeClass.Small:
                        smallRoomIndices.Add(i);
                        break;
                    case DungeonMakerRoomSizeClass.Large:
                        largeRoomIndices.Add(i);
                        break;
                }
            }

            if (smallRoomIndices.Count == 0 && largeRoomIndices.Count == 0)
                return result;

            System.Random random = new(unchecked(result.Seed ^ 0x51A7F00D));
            int selectedSmallIndex = PickRegionIndex(random, smallRoomIndices);
            int selectedLargeIndex = PickRegionIndex(random, largeRoomIndices);

            DungeonMakerRegion[] assignedRegions = new DungeonMakerRegion[regions.Count];
            bool changed = false;
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                DungeonMakerSpecialRoomRole role = DungeonMakerSpecialRoomRole.None;
                if (i == selectedSmallIndex)
                    role = DungeonMakerSpecialRoomRole.Start;
                else if (i == selectedLargeIndex)
                    role = DungeonMakerSpecialRoomRole.NextLevel;

                if (region.SpecialRoomRole != role)
                {
                    changed = true;
                    assignedRegions[i] = new DungeonMakerRegion(region.Id, region.Kind, region.TileIndices, region.RoomSizeClass, role);
                }
                else
                {
                    assignedRegions[i] = region;
                }
            }

            if (!changed)
                return result;

            return new DungeonMakerTunnelingResult(
                result.SourceWidth,
                result.SourceHeight,
                result.Seed,
                CopySourceMap(result),
                CopySourceOrigins(result),
                CopySourceSkeletonKinds(result),
                assignedRegions,
                CopySkeletonSegments(result),
                CopySkeletonLinks(result),
                CopySkeletonAttachments(result),
                CopyStats(result.Stats));
        }

        private static DungeonMakerTunnelingResult AssignEncounterMarkers(
            DungeonMakerTunnelingResult result,
            DungeonGenerationRules rules)
        {
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            if (regions.Count == 0)
                return result;

            DungeonMakerSquareData[] map = CopySourceMap(result);
            DungeonMakerTileOrigin[] origins = CopySourceOrigins(result);
            bool changed = false;
            System.Random random = new(unchecked(result.Seed ^ 0x2D7E4A11));

            List<int> corridorCandidates = new();
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                List<int> candidates = CollectRegionCandidates(region, map);
                if (candidates.Count == 0)
                    continue;

                switch (region.Kind)
                {
                    case DungeonMakerRegionKind.Corridor:
                        corridorCandidates.AddRange(candidates);
                        break;
                    case DungeonMakerRegionKind.AnteRoom:
                        changed |= PlaceAnteRoomEncounters(candidates, map, origins, random, rules);
                        break;
                    case DungeonMakerRegionKind.Room:
                        changed |= PlaceRoomEncounters(region, candidates, map, origins, random, rules);
                        break;
                }
            }

            changed |= PlaceCorridorEncounters(corridorCandidates, map, origins, random, rules.CorridorLevel1SpawnChanceDenominator);
            if (!changed)
                return result;

            return new DungeonMakerTunnelingResult(
                result.SourceWidth,
                result.SourceHeight,
                result.Seed,
                map,
                origins,
                CopySourceSkeletonKinds(result),
                CopyRegions(result),
                CopySkeletonSegments(result),
                CopySkeletonLinks(result),
                CopySkeletonAttachments(result),
                BuildStatsFromMap(result, map));
        }

        private static bool PlaceAnteRoomEncounters(
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            DungeonGenerationRules rules)
        {
            int totalCount = RollEncounterCount(random, rules.AnteRoomMonsterCountRange, candidates.Count);
            return PlaceRandomMonsterMix(candidates, map, origins, random, totalCount, allowLevel3: false);
        }

        private static bool PlaceRoomEncounters(
            DungeonMakerRegion region,
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            DungeonGenerationRules rules)
        {
            bool changed = false;
            DungeonMakerSquareData chestTile = GetTreasureTileForRoom(region.RoomSizeClass);
            if (chestTile != DungeonMakerSquareData.CLOSED)
                changed |= TryPlaceMarker(candidates, map, origins, random, chestTile, DungeonMakerTileOrigin.TreasurePlacement);

            if (region.SpecialRoomRole == DungeonMakerSpecialRoomRole.Start)
                return changed;

            switch (region.RoomSizeClass)
            {
                case DungeonMakerRoomSizeClass.Small:
                    changed |= PlaceRandomMonsterMix(candidates, map, origins, random, RollEncounterCount(random, rules.SmallRoomMonsterCountRange, candidates.Count), allowLevel3: false);
                    break;
                case DungeonMakerRoomSizeClass.Medium:
                    changed |= PlaceRandomMonsterMix(candidates, map, origins, random, RollEncounterCount(random, rules.MediumRoomMonsterCountRange, candidates.Count), allowLevel3: false);
                    break;
                case DungeonMakerRoomSizeClass.Large:
                    changed |= PlaceRandomMonsterMix(candidates, map, origins, random, RollEncounterCount(random, rules.LargeRoomMonsterCountRange, candidates.Count), allowLevel3: true);
                    break;
            }

            return changed;
        }

        private static bool PlaceCorridorEncounters(
            List<int> corridorCandidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            int spawnChanceDenominator)
        {
            if (corridorCandidates.Count == 0)
                return false;

            bool changed = false;
            for (int i = corridorCandidates.Count - 1; i >= 0; i--)
            {
                if (random.Next(spawnChanceDenominator) != 0)
                    continue;

                int tileIndex = corridorCandidates[i];
                corridorCandidates.RemoveAt(i);
                map[tileIndex] = DungeonMakerSquareData.MOB1;
                origins[tileIndex] = DungeonMakerTileOrigin.MonsterPlacement;
                changed = true;
            }

            return changed;
        }

        private static int RollEncounterCount(System.Random random, EncounterCountRange range, int candidateCount)
        {
            if (candidateCount <= 0 || range.Max <= 0)
                return 0;

            int rolled = random.Next(range.Min, range.Max + 1);
            return Math.Min(candidateCount, rolled);
        }

        private static bool PlaceRandomMonsterMix(
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            int totalCount,
            bool allowLevel3)
        {
            if (totalCount <= 0 || candidates.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < totalCount; i++)
            {
                DungeonMakerSquareData monsterTile = allowLevel3
                    ? (DungeonMakerSquareData)((int)DungeonMakerSquareData.MOB1 + random.Next(3))
                    : (DungeonMakerSquareData)((int)DungeonMakerSquareData.MOB1 + random.Next(2));
                changed |= TryPlaceMarker(candidates, map, origins, random, monsterTile, DungeonMakerTileOrigin.MonsterPlacement);
            }

            return changed;
        }

        private static bool TryPlaceMarker(
            List<int> candidates,
            DungeonMakerSquareData[] map,
            DungeonMakerTileOrigin[] origins,
            System.Random random,
            DungeonMakerSquareData markerTile,
            DungeonMakerTileOrigin origin)
        {
            if (candidates.Count == 0)
                return false;

            int candidateIndex = random.Next(candidates.Count);
            int tileIndex = candidates[candidateIndex];
            candidates.RemoveAt(candidateIndex);
            map[tileIndex] = markerTile;
            origins[tileIndex] = origin;
            return true;
        }

        private static DungeonMakerSquareData GetTreasureTileForRoom(DungeonMakerRoomSizeClass roomSizeClass)
        {
            return roomSizeClass switch
            {
                DungeonMakerRoomSizeClass.Small => DungeonMakerSquareData.TREAS1,
                DungeonMakerRoomSizeClass.Medium => DungeonMakerSquareData.TREAS2,
                DungeonMakerRoomSizeClass.Large => DungeonMakerSquareData.TREAS3,
                _ => DungeonMakerSquareData.CLOSED,
            };
        }

        private static List<int> CollectRegionCandidates(DungeonMakerRegion region, DungeonMakerSquareData[] map)
        {
            List<int> candidates = new(region.TileIndices.Length);
            int[] tiles = region.TileIndices;
            for (int i = 0; i < tiles.Length; i++)
            {
                int tileIndex = tiles[i];
                if (CanHostEncounterMarker(map[tileIndex]))
                    candidates.Add(tileIndex);
            }

            return candidates;
        }

        private static void PruneDeadEndCorridorsByLogicalSkeleton(
            DungeonMakerTunnelingResult result,
            DungeonMakerSquareData[] map,
            DungeonMakerSkeletonKind[] skeletonKinds,
            int width,
            int height,
            bool[] removedDeadEndMask,
            ref int prunedDeadEndTiles)
        {
            IReadOnlyList<DungeonMakerSkeletonSegment> segments = result.SkeletonSegments;
            if (segments.Count == 0)
                return;

            PruneDeadEndSkeletonSegments(result, width, height, out bool[] removedCorridorMask);

            for (int i = 0; i < removedCorridorMask.Length; i++)
            {
                if (!removedCorridorMask[i])
                    continue;

                removedDeadEndMask[i] = true;
                map[i] = GetDeadEndFilledTile(map[i]);
                if (skeletonKinds[i] != DungeonMakerSkeletonKind.None)
                    skeletonKinds[i] = DungeonMakerSkeletonKind.None;
                prunedDeadEndTiles++;
            }
        }

        private static void PruneDeadEndSkeletonSegments(
            DungeonMakerTunnelingResult result,
            int width,
            int height,
            out bool[] removedCorridorMask)
        {
            IReadOnlyList<DungeonMakerSkeletonSegment> segments = result.SkeletonSegments;
            IReadOnlyList<DungeonMakerSkeletonLink> links = result.SkeletonLinks;
            IReadOnlyList<DungeonMakerSkeletonAttachment> attachments = result.SkeletonAttachments;
            int segmentCount = segments.Count;
            bool[] aliveSegments = new bool[segmentCount];
            Array.Fill(aliveSegments, true);

            Dictionary<int, int> segmentIndexById = new(segmentCount);
            for (int i = 0; i < segmentCount; i++)
                segmentIndexById[segments[i].Id] = i;

            HashSet<int>[] allNeighbors = new HashSet<int>[segmentCount];
            int[] degree = new int[segmentCount];
            bool[] valuableSegments = new bool[segmentCount];

            for (int i = 0; i < segmentCount; i++)
                allNeighbors[i] = new HashSet<int>();

            for (int i = 0; i < attachments.Count; i++)
            {
                DungeonMakerSkeletonAttachment attachment = attachments[i];
                if (segmentIndexById.TryGetValue(attachment.SegmentId, out int segmentIndex))
                    valuableSegments[segmentIndex] = true;
            }

            for (int i = 0; i < links.Count; i++)
            {
                DungeonMakerSkeletonLink link = links[i];
                int fromSegmentIndex = ResolveSegmentIndex(link.FromSegmentId, link.From, segments, segmentIndexById);
                int toSegmentIndex = ResolveSegmentIndex(link.ToSegmentId, link.To, segments, segmentIndexById);
                if (fromSegmentIndex < 0 || toSegmentIndex < 0 || fromSegmentIndex == toSegmentIndex)
                    continue;

                if (allNeighbors[fromSegmentIndex].Add(toSegmentIndex))
                    degree[fromSegmentIndex]++;
                if (allNeighbors[toSegmentIndex].Add(fromSegmentIndex))
                    degree[toSegmentIndex]++;
            }

            Queue<int> queue = new();
            for (int i = 0; i < segmentCount; i++)
            {
                if (aliveSegments[i] && !valuableSegments[i] && degree[i] <= 1)
                    queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int segmentIndex = queue.Dequeue();
                if (!aliveSegments[segmentIndex] || valuableSegments[segmentIndex] || degree[segmentIndex] > 1)
                    continue;

                aliveSegments[segmentIndex] = false;
                foreach (int neighborIndex in allNeighbors[segmentIndex])
                {
                    if (!aliveSegments[neighborIndex])
                        continue;

                    degree[neighborIndex]--;
                    if (!valuableSegments[neighborIndex] && degree[neighborIndex] <= 1)
                        queue.Enqueue(neighborIndex);
                }
            }

            removedCorridorMask = new bool[width * height];
            for (int i = 0; i < segmentCount; i++)
            {
                if (!aliveSegments[i])
                    MarkOwnedTiles(removedCorridorMask, segments[i].OwnedTileIndices);
            }
        }

        private static int ResolveSegmentIndex(
            int segmentId,
            Vector2Int point,
            IReadOnlyList<DungeonMakerSkeletonSegment> segments,
            Dictionary<int, int> segmentIndexById)
        {
            if (segmentId >= 0 && segmentIndexById.TryGetValue(segmentId, out int exactIndex))
                return exactIndex;

            for (int i = 0; i < segments.Count; i++)
            {
                if (PointLiesOnSegment(point, segments[i]))
                    return i;
            }

            return -1;
        }

        private static bool PointLiesOnSegment(Vector2Int point, DungeonMakerSkeletonSegment segment)
        {
            if (segment.Start.x == segment.End.x)
            {
                if (point.x != segment.Start.x)
                    return false;

                int minY = Math.Min(segment.Start.y, segment.End.y);
                int maxY = Math.Max(segment.Start.y, segment.End.y);
                return point.y >= minY && point.y <= maxY;
            }

            if (segment.Start.y == segment.End.y)
            {
                if (point.y != segment.Start.y)
                    return false;

                int minX = Math.Min(segment.Start.x, segment.End.x);
                int maxX = Math.Max(segment.Start.x, segment.End.x);
                return point.x >= minX && point.x <= maxX;
            }

            return point == segment.Start || point == segment.End;
        }

        private static void MarkOwnedTiles(bool[] mask, int[] ownedTileIndices)
        {
            for (int i = 0; i < ownedTileIndices.Length; i++)
                mask[ownedTileIndices[i]] = true;
        }

        private static DungeonMakerSquareData GetDeadEndFilledTile(DungeonMakerSquareData tile)
        {
            return tile switch
            {
                DungeonMakerSquareData.G_OPEN => DungeonMakerSquareData.G_CLOSED,
                DungeonMakerSquareData.NJ_OPEN => DungeonMakerSquareData.NJ_CLOSED,
                DungeonMakerSquareData.NJ_G_OPEN => DungeonMakerSquareData.NJ_G_CLOSED,
                _ => DungeonMakerSquareData.CLOSED,
            };
        }

        private static bool RegionHasWalkableTile(DungeonMakerRegion region, DungeonMakerSquareData[] map)
        {
            int[] tiles = region.TileIndices;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (IsWalkableTile(map[tiles[i]]))
                    return true;
            }

            return false;
        }

        private static bool CanHostEncounterMarker(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.IT_OPEN
                or DungeonMakerSquareData.IR_OPEN
                or DungeonMakerSquareData.IA_OPEN
                or DungeonMakerSquareData.OPEN
                or DungeonMakerSquareData.G_OPEN
                or DungeonMakerSquareData.NJ_OPEN
                or DungeonMakerSquareData.NJ_G_OPEN;
        }

        private static bool IsWalkableTile(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.OPEN
                or DungeonMakerSquareData.G_OPEN
                or DungeonMakerSquareData.NJ_OPEN
                or DungeonMakerSquareData.NJ_G_OPEN
                or DungeonMakerSquareData.IR_OPEN
                or DungeonMakerSquareData.IT_OPEN
                or DungeonMakerSquareData.IA_OPEN
                or DungeonMakerSquareData.H_DOOR
                or DungeonMakerSquareData.V_DOOR
                or DungeonMakerSquareData.MOB1
                or DungeonMakerSquareData.MOB2
                or DungeonMakerSquareData.MOB3
                or DungeonMakerSquareData.TREAS1
                or DungeonMakerSquareData.TREAS2
                or DungeonMakerSquareData.TREAS3;
        }

        private static bool IsMapQualified(DungeonMakerTunnelingStats stats, DungeonGenerationRules rules)
        {
            return IsWithinRange(stats.LargeRooms, rules.LargeRoomRange)
                && IsWithinRange(stats.MediumRooms, rules.MediumRoomRange)
                && IsWithinRange(stats.SmallRooms, rules.SmallRoomRange)
                && IsWithinRange(stats.WalkableTiles, rules.WalkableTileRange);
        }

        private static bool IsWithinRange(int value, Vector2Int range)
        {
            Vector2Int normalized = NormalizeRange(range);
            return value >= normalized.x && value <= normalized.y;
        }

        private static Vector2Int NormalizeRange(Vector2Int range)
        {
            int min = Mathf.Min(range.x, range.y);
            int max = Mathf.Max(range.x, range.y);
            return new Vector2Int(min, max);
        }

        private static int PickRegionIndex(System.Random random, List<int> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            return candidates[random.Next(candidates.Count)];
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
                return (int)z;
            }
        }

        private static DungeonMakerTunnelingStats BuildStatsFromMap(DungeonMakerTunnelingResult sourceResult, DungeonMakerSquareData[] map)
        {
            DungeonMakerTunnelingStats source = sourceResult.Stats;
            return BuildStatsFromMap(
                source.SourceWidth,
                source.SourceHeight,
                map,
                source.TotalRooms,
                source.SmallRooms,
                source.MediumRooms,
                source.LargeRooms,
                source.AnteRooms);
        }

        private static DungeonMakerTunnelingStats BuildStatsFromMap(
            int sourceWidth,
            int sourceHeight,
            DungeonMakerSquareData[] map,
            int totalRooms,
            int smallRooms,
            int mediumRooms,
            int largeRooms,
            int anteRooms)
        {
            DungeonMakerTunnelingStats stats = new()
            {
                SourceWidth = sourceWidth,
                SourceHeight = sourceHeight,
                DisplayWidth = sourceHeight,
                DisplayHeight = sourceWidth,
                TotalCells = map.Length,
                TotalRooms = totalRooms,
                SmallRooms = smallRooms,
                MediumRooms = mediumRooms,
                LargeRooms = largeRooms,
                AnteRooms = anteRooms,
            };

            for (int i = 0; i < map.Length; i++)
            {
                switch (map[i])
                {
                    case DungeonMakerSquareData.OPEN:
                        stats.OpenTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.CLOSED:
                        stats.ClosedTiles++;
                        stats.BlockedTiles++;
                        break;
                    case DungeonMakerSquareData.G_OPEN:
                        stats.BoundaryOpenTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.G_CLOSED:
                        stats.BoundaryClosedTiles++;
                        stats.BlockedTiles++;
                        break;
                    case DungeonMakerSquareData.NJ_OPEN:
                        stats.NonJoinOpenTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.NJ_CLOSED:
                        stats.NonJoinClosedTiles++;
                        stats.BlockedTiles++;
                        break;
                    case DungeonMakerSquareData.NJ_G_OPEN:
                        stats.NonJoinBoundaryOpenTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.NJ_G_CLOSED:
                        stats.NonJoinBoundaryClosedTiles++;
                        stats.BlockedTiles++;
                        break;
                    case DungeonMakerSquareData.IR_OPEN:
                        stats.RoomTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.IT_OPEN:
                        stats.TunnelTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.IA_OPEN:
                        stats.AnteRoomTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.H_DOOR:
                        stats.HorizontalDoorTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.V_DOOR:
                        stats.VerticalDoorTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.MOB1:
                    case DungeonMakerSquareData.MOB2:
                    case DungeonMakerSquareData.MOB3:
                        stats.MobTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.TREAS1:
                    case DungeonMakerSquareData.TREAS2:
                    case DungeonMakerSquareData.TREAS3:
                        stats.TreasureTiles++;
                        stats.WalkableTiles++;
                        break;
                    case DungeonMakerSquareData.COLUMN:
                        stats.ColumnTiles++;
                        stats.BlockedTiles++;
                        break;
                }
            }

            return stats;
        }

        private static DungeonMakerTunnelingStats CopyStats(DungeonMakerTunnelingStats source)
        {
            return new DungeonMakerTunnelingStats
            {
                SourceWidth = source.SourceWidth,
                SourceHeight = source.SourceHeight,
                DisplayWidth = source.DisplayWidth,
                DisplayHeight = source.DisplayHeight,
                TotalCells = source.TotalCells,
                TotalRooms = source.TotalRooms,
                SmallRooms = source.SmallRooms,
                MediumRooms = source.MediumRooms,
                LargeRooms = source.LargeRooms,
                AnteRooms = source.AnteRooms,
                OpenTiles = source.OpenTiles,
                ClosedTiles = source.ClosedTiles,
                BoundaryOpenTiles = source.BoundaryOpenTiles,
                BoundaryClosedTiles = source.BoundaryClosedTiles,
                NonJoinOpenTiles = source.NonJoinOpenTiles,
                NonJoinClosedTiles = source.NonJoinClosedTiles,
                NonJoinBoundaryOpenTiles = source.NonJoinBoundaryOpenTiles,
                NonJoinBoundaryClosedTiles = source.NonJoinBoundaryClosedTiles,
                RoomTiles = source.RoomTiles,
                TunnelTiles = source.TunnelTiles,
                AnteRoomTiles = source.AnteRoomTiles,
                HorizontalDoorTiles = source.HorizontalDoorTiles,
                VerticalDoorTiles = source.VerticalDoorTiles,
                MobTiles = source.MobTiles,
                TreasureTiles = source.TreasureTiles,
                ColumnTiles = source.ColumnTiles,
                WalkableTiles = source.WalkableTiles,
                BlockedTiles = source.BlockedTiles,
            };
        }

        private static DungeonMakerSquareData[] CopySourceMap(DungeonMakerTunnelingResult result)
        {
            int width = result.SourceWidth;
            int height = result.SourceHeight;
            DungeonMakerSquareData[] map = new DungeonMakerSquareData[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    map[x * height + y] = result.GetSourceTile(x, y);
            }

            return map;
        }

        private static DungeonMakerTileOrigin[] CopySourceOrigins(DungeonMakerTunnelingResult result)
        {
            int width = result.SourceWidth;
            int height = result.SourceHeight;
            DungeonMakerTileOrigin[] origins = new DungeonMakerTileOrigin[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    origins[x * height + y] = result.GetSourceOrigin(x, y);
            }

            return origins;
        }

        private static DungeonMakerSkeletonKind[] CopySourceSkeletonKinds(DungeonMakerTunnelingResult result)
        {
            int width = result.SourceWidth;
            int height = result.SourceHeight;
            DungeonMakerSkeletonKind[] kinds = new DungeonMakerSkeletonKind[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    kinds[x * height + y] = result.GetSourceSkeletonKind(x, y);
            }

            return kinds;
        }

        private static DungeonMakerRegion[] CopyRegions(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerRegion> source = result.Regions;
            DungeonMakerRegion[] copied = new DungeonMakerRegion[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DungeonMakerRegion region = source[i];
                copied[i] = new DungeonMakerRegion(region.Id, region.Kind, region.TileIndices, region.RoomSizeClass, region.SpecialRoomRole);
            }

            return copied;
        }

        private static DungeonMakerSkeletonSegment[] CopySkeletonSegments(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerSkeletonSegment> source = result.SkeletonSegments;
            DungeonMakerSkeletonSegment[] copied = new DungeonMakerSkeletonSegment[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DungeonMakerSkeletonSegment segment = source[i];
                int[] ownedTileIndices = new int[segment.OwnedTileIndices.Length];
                Array.Copy(segment.OwnedTileIndices, ownedTileIndices, ownedTileIndices.Length);
                copied[i] = new DungeonMakerSkeletonSegment(segment.Id, segment.BuilderId, segment.Start, segment.End, ownedTileIndices);
            }

            return copied;
        }

        private static DungeonMakerSkeletonLink[] CopySkeletonLinks(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerSkeletonLink> source = result.SkeletonLinks;
            DungeonMakerSkeletonLink[] copied = new DungeonMakerSkeletonLink[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DungeonMakerSkeletonLink link = source[i];
                copied[i] = new DungeonMakerSkeletonLink(link.FromSegmentId, link.ToSegmentId, link.From, link.To);
            }

            return copied;
        }

        private static DungeonMakerSkeletonAttachment[] CopySkeletonAttachments(DungeonMakerTunnelingResult result)
        {
            IReadOnlyList<DungeonMakerSkeletonAttachment> source = result.SkeletonAttachments;
            DungeonMakerSkeletonAttachment[] copied = new DungeonMakerSkeletonAttachment[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DungeonMakerSkeletonAttachment attachment = source[i];
                copied[i] = new DungeonMakerSkeletonAttachment(attachment.SegmentId);
            }

            return copied;
        }

        private static RuntimeDungeonSceneData BuildSceneData(
            DungeonMakerTunnelingResult result,
            int dungeonFloor,
            DungeonThemeData theme,
            DungeonConfig dungeonConfig,
            DungeonBossRoomData bossRoom = null,
            int seed = 0)
        {
            DungeonMakerSquareData[] map = CopySourceMap(result);
            int[] regionIdByTile = BuildRegionIdByTileMap(result, map.Length);
            RuntimeDungeonSceneData sceneData = new()
            {
                ThemeId = theme?.Id ?? -1,
                ThemeKey = theme?.ThemeKey ?? string.Empty,
                IsBossFloor = bossRoom != null,
                CellWorldSize = Mathf.Max(0.25f, dungeonConfig?.CellWorldSize ?? 2f),
                ExitInteractionRange = Mathf.Max(0.5f, dungeonConfig?.ExitInteractionRange ?? 3f),
                CorridorMaterialPath = ResolveMaterialPath(theme?.CorridorMaterialPath, dungeonConfig?.DefaultCorridorMaterialPath),
                RoomMaterialPath = ResolveMaterialPath(theme?.RoomMaterialPath, dungeonConfig?.DefaultRoomMaterialPath),
                AnteRoomMaterialPath = ResolveMaterialPath(theme?.AnteRoomMaterialPath, dungeonConfig?.DefaultAnteRoomMaterialPath),
                WallMaterialPath = ResolveMaterialPath(theme?.WallMaterialPath, dungeonConfig?.DefaultWallMaterialPath),
                ExitClosedMaterialPath = ResolveMaterialPath(theme?.ExitClosedMaterialPath, dungeonConfig?.DefaultExitClosedMaterialPath),
                ExitOpenMaterialPath = ResolveMaterialPath(theme?.ExitOpenMaterialPath, dungeonConfig?.DefaultExitOpenMaterialPath),
            };

            AddEnvironmentSpawns(sceneData, result);

            RuntimeDungeonSceneObjectSpawnData playerSpawn = bossRoom == null
                ? BuildSpecialPointData(result, map, regionIdByTile, sceneData.CellWorldSize, DungeonMakerSpecialRoomRole.Start)
                    ?? BuildFallbackPointData(result, map, regionIdByTile, sceneData.CellWorldSize, requiresRoomClear: false)
                : CreatePointFromBossCoordinate(result, regionIdByTile, bossRoom.PlayerSpawn, sceneData.CellWorldSize, requiresRoomClear: false)
                    ?? BuildFallbackPointData(result, map, regionIdByTile, sceneData.CellWorldSize, requiresRoomClear: false);
            sceneData.PlayerSpawnWorldPosition = playerSpawn?.WorldPosition ?? Vector3.zero;

            RuntimeDungeonSceneObjectSpawnData exitSpawn = bossRoom == null
                ? BuildSpecialPointData(result, map, regionIdByTile, sceneData.CellWorldSize, DungeonMakerSpecialRoomRole.NextLevel)
                    ?? BuildFallbackPointData(result, map, regionIdByTile, sceneData.CellWorldSize, requiresRoomClear: true)
                : CreatePointFromBossCoordinate(result, regionIdByTile, bossRoom.ExitSpawn, sceneData.CellWorldSize, requiresRoomClear: true)
                    ?? BuildFallbackPointData(result, map, regionIdByTile, sceneData.CellWorldSize, requiresRoomClear: true);
            if (exitSpawn != null)
            {
                sceneData.SceneObjects.Add(CreateExitSceneObject(exitSpawn, sceneData, dungeonFloor + 1));
            }

            if (bossRoom == null)
            {
                for (int tileIndex = 0; tileIndex < map.Length; tileIndex++)
                {
                    int monsterLevel = GetMonsterLevel(map[tileIndex]);
                    if (monsterLevel > 0)
                    {
                        sceneData.MonsterSpawns.Add(CreateMonsterSpawnData(
                            result,
                            tileIndex,
                            regionIdByTile[tileIndex],
                            monsterLevel,
                            dungeonFloor,
                            theme,
                            dungeonConfig,
                            isBoss: false));
                        continue;
                    }

                    int treasureLevel = GetTreasureLevel(map[tileIndex]);
                    if (treasureLevel <= 0)
                        continue;

                    List<RuntimeDungeonTreasureRewardData> treasureRewards = ResolveTreasureRewards(treasureLevel, dungeonFloor, theme, dungeonConfig);
                    if (treasureRewards.Count <= 0)
                        continue;

                    RuntimeDungeonSceneObjectSpawnData treasurePoint = CreatePointData(
                        result,
                        tileIndex,
                        regionIdByTile[tileIndex],
                        sceneData.CellWorldSize,
                        requiresRoomClear: false);
                    sceneData.SceneObjects.Add(CreateTreasureSceneObject(treasurePoint, treasureRewards));
                }
            }
            else
            {
                RuntimeDungeonMonsterSpawnData bossSpawn = CreateBossMonsterSpawnData(result, regionIdByTile, bossRoom.BossSpawn, dungeonFloor, theme, dungeonConfig, bossRoom, seed);
                if (bossSpawn != null)
                    sceneData.MonsterSpawns.Add(bossSpawn);

                List<Int2Data> supportSpawnPoints = bossRoom.SupportSpawnPoints ?? new List<Int2Data>();
                for (int i = 0; i < supportSpawnPoints.Count; i++)
                {
                    RuntimeDungeonMonsterSpawnData supportSpawn = CreateSupportMonsterSpawnData(
                        result,
                        regionIdByTile,
                        supportSpawnPoints[i],
                        dungeonFloor,
                        theme,
                        dungeonConfig,
                        seed + i + 1);
                    if (supportSpawn != null)
                        sceneData.MonsterSpawns.Add(supportSpawn);
                }

                List<RuntimeDungeonTreasureRewardData> rewardEntries = bossRoom.RewardTreasurePoolId > 0
                    ? ResolveTreasureRewardsFromPool(bossRoom.RewardTreasurePoolId, 3, dungeonFloor, theme?.Id ?? 0)
                    : ResolveTreasureRewards(3, dungeonFloor, theme, dungeonConfig);
                if (rewardEntries.Count > 0)
                {
                    RuntimeDungeonSceneObjectSpawnData rewardPoint = CreatePointFromBossCoordinate(result, regionIdByTile, bossRoom.RewardSpawn, sceneData.CellWorldSize, requiresRoomClear: false);
                    if (rewardPoint != null)
                        sceneData.SceneObjects.Add(CreateTreasureSceneObject(rewardPoint, rewardEntries));
                }
            }

            return sceneData;
        }

        private static GeneratedDungeonPayload BuildBossFloorPayload(
            int dungeonFloor,
            int seed,
            DungeonConfig dungeonConfig,
            DungeonThemeData theme)
        {
            DungeonBossRoomData bossRoom = ResolveBossRoomData(dungeonFloor, theme, dungeonConfig, seed);
            bossRoom?.EnsureValid();
            bossRoom ??= CreateFallbackBossRoomData(dungeonFloor);

            DungeonMakerTunnelingResult layout = BuildBossRoomLayout(seed, bossRoom);
            RuntimeDungeonSceneData sceneData = BuildSceneData(layout, dungeonFloor, theme, dungeonConfig, bossRoom, seed);
            return new GeneratedDungeonPayload(layout, sceneData);
        }

        private static DungeonConfig GetDungeonConfig()
        {
            return ConfigComponent.Instance?.Get<DungeonConfig>() ?? new DungeonConfig();
        }

        private static DungeonThemeData ResolveThemeData(int dungeonFloor)
        {
            IEnumerable<DungeonThemeData> themes = DataComponent.Instance?.FindAll<DungeonThemeData>(static _ => true);
            DungeonThemeData bestMatch = null;
            int bestDistance = int.MaxValue;

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
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestMatch = theme;
                    }
                }
            }

            if (bestMatch != null)
                return bestMatch;

            DungeonConfig config = GetDungeonConfig();
            int bandSize = Mathf.Max(1, config.ThemeBandSize);
            int bandStart = ((Mathf.Max(1, dungeonFloor) - 1) / bandSize) * bandSize + 1;
            return new DungeonThemeData
            {
                Id = bandStart,
                Name = $"Dungeon Theme {bandStart}",
                ThemeKey = $"theme_{bandStart:D2}",
                FloorStart = bandStart,
                FloorEnd = bandStart + bandSize - 1,
            };
        }

        private static bool IsBossFloor(int dungeonFloor, DungeonConfig config)
        {
            int bossFloorInterval = Mathf.Max(1, config?.BossFloorInterval ?? 10);
            return Mathf.Max(1, dungeonFloor) % bossFloorInterval == 0;
        }

        private static List<RuntimeDungeonTreasureRewardData> ResolveTreasureRewards(int level, int dungeonFloor, DungeonThemeData theme, DungeonConfig dungeonConfig)
        {
            int fallbackPoolId = level switch
            {
                1 => dungeonConfig?.FallbackTreasure1PoolId ?? -1,
                2 => dungeonConfig?.FallbackTreasure2PoolId ?? -1,
                3 => dungeonConfig?.FallbackTreasure3PoolId ?? -1,
                _ => dungeonConfig?.FallbackTreasure1PoolId ?? -1,
            };

            int themePoolId = level switch
            {
                1 => theme?.Treasure1PoolId ?? -1,
                2 => theme?.Treasure2PoolId ?? -1,
                3 => theme?.Treasure3PoolId ?? -1,
                _ => -1,
            };

            int resolvedPoolId = themePoolId > 0 ? themePoolId : fallbackPoolId;
            return ResolveTreasureRewardsFromPool(resolvedPoolId, level, dungeonFloor, theme?.Id ?? 0);
        }

        private static DungeonBossRoomData ResolveBossRoomData(int dungeonFloor, DungeonThemeData theme, DungeonConfig dungeonConfig, int seed)
        {
            int bandIndex = GetThemeBandIndex(dungeonFloor, dungeonConfig);
            List<DungeonBossRoomData> candidates = new();
            HashSet<int> allowedIds = theme?.BossRoomIds != null && theme.BossRoomIds.Count > 0
                ? new HashSet<int>(theme.BossRoomIds)
                : null;
            IEnumerable<DungeonBossRoomData> allRooms = DataComponent.Instance?.FindAll<DungeonBossRoomData>(static _ => true);
            if (allRooms != null)
            {
                foreach (DungeonBossRoomData room in allRooms)
                {
                    if (room == null)
                        continue;

                    room.EnsureValid();
                    if (allowedIds != null && !allowedIds.Contains(room.Id))
                        continue;
                    if (!string.IsNullOrWhiteSpace(theme?.ThemeKey)
                        && !string.IsNullOrWhiteSpace(room.ThemeKey)
                        && !string.Equals(room.ThemeKey, theme.ThemeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (bandIndex < room.FloorBandStart || bandIndex > room.FloorBandEnd)
                        continue;

                    candidates.Add(room);
                }
            }

            if (candidates.Count == 0 && dungeonConfig?.FallbackBossRoomId > 0)
                return DataComponent.Instance?.Get<DungeonBossRoomData>(dungeonConfig.FallbackBossRoomId);

            if (candidates.Count == 0)
                return null;

            int selectedIndex = GetDeterministicIndex(seed, candidates.Count);
            return candidates[selectedIndex];
        }

        private static DungeonBossRoomData CreateFallbackBossRoomData(int dungeonFloor)
        {
            DungeonConfig config = GetDungeonConfig();
            int bandIndex = GetThemeBandIndex(dungeonFloor, config);
            return new DungeonBossRoomData
            {
                Id = 100000 + bandIndex,
                Name = $"Fallback Boss Room {bandIndex}",
                FloorBandStart = bandIndex,
                FloorBandEnd = bandIndex,
                Width = 24,
                Height = 18,
                PlayerSpawn = new Int2Data(3, 3),
                ExitSpawn = new Int2Data(20, 14),
                RewardSpawn = new Int2Data(12, 14),
                BossSpawn = new Int2Data(12, 9),
                BossPoolIds = new List<int>(),
                RewardTreasurePoolId = -1,
            };
        }

        private static DungeonMakerTunnelingResult BuildBossRoomLayout(int seed, DungeonBossRoomData bossRoom)
        {
            bossRoom?.EnsureValid();
            bossRoom ??= CreateFallbackBossRoomData(1);

            int width = Mathf.Max(8, bossRoom.Width);
            int height = Mathf.Max(8, bossRoom.Height);
            int tileCount = width * height;
            DungeonMakerSquareData[] map = new DungeonMakerSquareData[tileCount];
            DungeonMakerTileOrigin[] origins = new DungeonMakerTileOrigin[tileCount];
            DungeonMakerSkeletonKind[] skeletonKinds = new DungeonMakerSkeletonKind[tileCount];
            List<int> roomTileIndices = new(tileCount);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int tileIndex = x * height + y;
                    bool isBoundary = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    if (isBoundary)
                    {
                        map[tileIndex] = DungeonMakerSquareData.G_CLOSED;
                        origins[tileIndex] = DungeonMakerTileOrigin.BoundaryClosed;
                        continue;
                    }

                    map[tileIndex] = DungeonMakerSquareData.IR_OPEN;
                    origins[tileIndex] = DungeonMakerTileOrigin.RoomCarve;
                    roomTileIndices.Add(tileIndex);
                }
            }

            int rewardTileIndex = GetTileIndexForBossCoordinate(bossRoom.RewardSpawn, width, height);
            if (rewardTileIndex >= 0)
            {
                map[rewardTileIndex] = DungeonMakerSquareData.TREAS3;
                origins[rewardTileIndex] = DungeonMakerTileOrigin.TreasurePlacement;
            }

            DungeonMakerRegion[] regions =
            {
                new DungeonMakerRegion(
                    0,
                    DungeonMakerRegionKind.Room,
                    roomTileIndices.ToArray(),
                    DungeonMakerRoomSizeClass.Large,
                    DungeonMakerSpecialRoomRole.None)
            };

            DungeonMakerTunnelingStats stats = BuildStatsFromMap(width, height, map, 1, 0, 0, 1, 0);
            return new DungeonMakerTunnelingResult(
                width,
                height,
                seed,
                map,
                origins,
                skeletonKinds,
                regions,
                Array.Empty<DungeonMakerSkeletonSegment>(),
                Array.Empty<DungeonMakerSkeletonLink>(),
                Array.Empty<DungeonMakerSkeletonAttachment>(),
                stats);
        }

        private static RuntimeDungeonSceneObjectSpawnData BuildSpecialPointData(
            DungeonMakerTunnelingResult result,
            DungeonMakerSquareData[] map,
            int[] regionIdByTile,
            float cellWorldSize,
            DungeonMakerSpecialRoomRole role)
        {
            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                if (region.SpecialRoomRole != role)
                    continue;

                if (!TryPickSpecialObjectTileIndex(region, map, result.SourceHeight, out int tileIndex))
                    return null;

                return CreatePointData(
                    result,
                    tileIndex,
                    regionIdByTile[tileIndex],
                    cellWorldSize,
                    requiresRoomClear: role == DungeonMakerSpecialRoomRole.NextLevel);
            }

            return null;
        }

        private static RuntimeDungeonSceneObjectSpawnData BuildFallbackPointData(
            DungeonMakerTunnelingResult result,
            DungeonMakerSquareData[] map,
            int[] regionIdByTile,
            float cellWorldSize,
            bool requiresRoomClear)
        {
            for (int tileIndex = 0; tileIndex < map.Length; tileIndex++)
            {
                if (!IsWalkableTile(map[tileIndex]))
                    continue;

                return CreatePointData(result, tileIndex, regionIdByTile[tileIndex], cellWorldSize, requiresRoomClear);
            }

            return null;
        }

        private static bool TryPickSpecialObjectTileIndex(
            DungeonMakerRegion region,
            DungeonMakerSquareData[] map,
            int sourceHeight,
            out int tileIndex)
        {
            tileIndex = -1;
            if (region?.TileIndices == null || region.TileIndices.Length == 0)
                return false;

            List<int> preferred = new();
            List<int> fallback = new();
            int[] tiles = region.TileIndices;
            for (int i = 0; i < tiles.Length; i++)
            {
                int candidate = tiles[i];
                if (CanHostSpecialObject(map[candidate]))
                    preferred.Add(candidate);
                else if (IsWalkableTile(map[candidate]))
                    fallback.Add(candidate);
            }

            List<int> source = preferred.Count > 0 ? preferred : fallback;
            if (source.Count == 0)
                return false;

            tileIndex = PickClosestTileToRegionCenter(region, source, sourceHeight);
            return tileIndex >= 0;
        }

        private static int PickClosestTileToRegionCenter(DungeonMakerRegion region, List<int> candidates, int sourceHeight)
        {
            int[] regionTiles = region.TileIndices;
            if (regionTiles == null || regionTiles.Length == 0 || candidates == null || candidates.Count == 0)
                return -1;

            float sumX = 0f;
            float sumY = 0f;
            for (int i = 0; i < regionTiles.Length; i++)
            {
                Vector2Int sourceCoordinate = GetSourceCoordinateFromTileIndex(regionTiles[i], sourceHeight);
                sumX += sourceCoordinate.x;
                sumY += sourceCoordinate.y;
            }

            float centerX = sumX / regionTiles.Length;
            float centerY = sumY / regionTiles.Length;
            float bestDistanceSq = float.MaxValue;
            int bestTileIndex = candidates[0];
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int candidateCoordinate = GetSourceCoordinateFromTileIndex(candidates[i], sourceHeight);
                float dx = candidateCoordinate.x - centerX;
                float dy = candidateCoordinate.y - centerY;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestTileIndex = candidates[i];
                }
            }

            return bestTileIndex;
        }

        private static RuntimeDungeonSceneObjectSpawnData CreatePointData(
            DungeonMakerTunnelingResult result,
            int tileIndex,
            int regionId,
            float cellWorldSize,
            bool requiresRoomClear)
        {
            Vector2Int sourceCoordinate = GetSourceCoordinateFromTileIndex(tileIndex, result.SourceHeight);
            Vector2Int displayCoordinate = new(sourceCoordinate.y, sourceCoordinate.x);
            return new RuntimeDungeonSceneObjectSpawnData
            {
                RegionId = regionId,
                TileIndex = tileIndex,
                SourceCoordinate = sourceCoordinate,
                DisplayCoordinate = displayCoordinate,
                WorldPosition = GetWorldPosition(result, displayCoordinate, cellWorldSize),
                RequiresRoomClear = requiresRoomClear,
            };
        }

        private static RuntimeDungeonMonsterSpawnData CreateMonsterSpawnData(
            DungeonMakerTunnelingResult result,
            int tileIndex,
            int regionId,
            int level,
            int dungeonFloor,
            DungeonThemeData theme,
            DungeonConfig dungeonConfig,
            bool isBoss)
        {
            Vector2Int sourceCoordinate = GetSourceCoordinateFromTileIndex(tileIndex, result.SourceHeight);
            Vector2Int displayCoordinate = new(sourceCoordinate.y, sourceCoordinate.x);
            return new RuntimeDungeonMonsterSpawnData
            {
                RegionId = regionId,
                TileIndex = tileIndex,
                Level = level,
                IsBoss = isBoss,
                PrefabName = ResolveMonsterPrefabName(level, result.Seed, tileIndex, dungeonFloor, theme, dungeonConfig, isBoss),
                SourceCoordinate = sourceCoordinate,
                DisplayCoordinate = displayCoordinate,
                WorldPosition = GetWorldPosition(result, displayCoordinate, Mathf.Max(0.25f, dungeonConfig?.CellWorldSize ?? 2f)),
            };
        }

        private static int[] BuildRegionIdByTileMap(DungeonMakerTunnelingResult result, int tileCount)
        {
            int[] regionIdByTile = new int[tileCount];
            Array.Fill(regionIdByTile, -1);

            IReadOnlyList<DungeonMakerRegion> regions = result.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                if (region?.TileIndices == null)
                    continue;

                for (int tileIndexIndex = 0; tileIndexIndex < region.TileIndices.Length; tileIndexIndex++)
                {
                    int tileIndex = region.TileIndices[tileIndexIndex];
                    if (tileIndex >= 0 && tileIndex < regionIdByTile.Length)
                        regionIdByTile[tileIndex] = region.Id;
                }
            }

            return regionIdByTile;
        }

        private static Vector2Int GetSourceCoordinateFromTileIndex(int tileIndex, int sourceHeight)
        {
            int safeSourceHeight = Mathf.Max(1, sourceHeight);
            int sourceX = tileIndex / safeSourceHeight;
            int sourceY = tileIndex % safeSourceHeight;
            return new Vector2Int(sourceX, sourceY);
        }

        private static Vector3 GetWorldPosition(DungeonMakerTunnelingResult result, Vector2Int displayCoordinate, float cellWorldSize)
        {
            float halfWidth = result.DisplayWidth * 0.5f;
            float halfHeight = result.DisplayHeight * 0.5f;
            float safeCellWorldSize = Mathf.Max(0.25f, cellWorldSize);
            float worldX = (displayCoordinate.x + 0.5f - halfWidth) * safeCellWorldSize;
            float worldY = (displayCoordinate.y + 0.5f - halfHeight) * safeCellWorldSize;
            return new Vector3(worldX, worldY, 0f);
        }

        private static bool CanHostSpecialObject(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.OPEN
                or DungeonMakerSquareData.G_OPEN
                or DungeonMakerSquareData.NJ_OPEN
                or DungeonMakerSquareData.NJ_G_OPEN
                or DungeonMakerSquareData.IR_OPEN
                or DungeonMakerSquareData.IT_OPEN
                or DungeonMakerSquareData.IA_OPEN;
        }

        private static int GetMonsterLevel(DungeonMakerSquareData tile)
        {
            return tile switch
            {
                DungeonMakerSquareData.MOB1 => 1,
                DungeonMakerSquareData.MOB2 => 2,
                DungeonMakerSquareData.MOB3 => 3,
                _ => 0,
            };
        }

        private static int GetTreasureLevel(DungeonMakerSquareData tile)
        {
            return tile switch
            {
                DungeonMakerSquareData.TREAS1 => 1,
                DungeonMakerSquareData.TREAS2 => 2,
                DungeonMakerSquareData.TREAS3 => 3,
                _ => 0,
            };
        }

        private static RuntimeDungeonSceneObjectSpawnData CreatePointFromBossCoordinate(
            DungeonMakerTunnelingResult layout,
            int[] regionIdByTile,
            Int2Data coordinate,
            float cellWorldSize,
            bool requiresRoomClear)
        {
            int tileIndex = GetTileIndexForBossCoordinate(coordinate, layout.SourceWidth, layout.SourceHeight);
            if (tileIndex < 0)
                return null;

            int regionId = tileIndex >= 0 && tileIndex < regionIdByTile.Length ? regionIdByTile[tileIndex] : -1;
            return CreatePointData(layout, tileIndex, regionId, cellWorldSize, requiresRoomClear);
        }

        private static RuntimeDungeonMonsterSpawnData CreateBossMonsterSpawnData(
            DungeonMakerTunnelingResult layout,
            int[] regionIdByTile,
            Int2Data coordinate,
            int dungeonFloor,
            DungeonThemeData theme,
            DungeonConfig dungeonConfig,
            DungeonBossRoomData bossRoom,
            int seed)
        {
            int tileIndex = GetTileIndexForBossCoordinate(coordinate, layout.SourceWidth, layout.SourceHeight);
            if (tileIndex < 0)
                return null;

            string prefabName = ResolveMonsterPrefabNameFromPoolIds(
                bossRoom?.BossPoolIds,
                seed,
                tileIndex,
                dungeonFloor,
                isBoss: true,
                fallbackPoolId: theme?.Mob3PoolId > 0 ? theme.Mob3PoolId : dungeonConfig?.FallbackMob3PoolId ?? -1);
            if (string.IsNullOrWhiteSpace(prefabName))
                return null;

            RuntimeDungeonMonsterSpawnData spawnData = CreateMonsterSpawnData(
                layout,
                tileIndex,
                tileIndex >= 0 && tileIndex < regionIdByTile.Length ? regionIdByTile[tileIndex] : -1,
                3,
                dungeonFloor,
                theme,
                dungeonConfig,
                isBoss: true);
            spawnData.PrefabName = prefabName;
            return spawnData;
        }

        private static RuntimeDungeonMonsterSpawnData CreateSupportMonsterSpawnData(
            DungeonMakerTunnelingResult layout,
            int[] regionIdByTile,
            Int2Data coordinate,
            int dungeonFloor,
            DungeonThemeData theme,
            DungeonConfig dungeonConfig,
            int seed)
        {
            int tileIndex = GetTileIndexForBossCoordinate(coordinate, layout.SourceWidth, layout.SourceHeight);
            if (tileIndex < 0)
                return null;

            Vector2Int sourceCoordinate = GetSourceCoordinateFromTileIndex(tileIndex, layout.SourceHeight);
            Vector2Int displayCoordinate = new(sourceCoordinate.y, sourceCoordinate.x);
            return new RuntimeDungeonMonsterSpawnData
            {
                RegionId = tileIndex >= 0 && tileIndex < regionIdByTile.Length ? regionIdByTile[tileIndex] : -1,
                TileIndex = tileIndex,
                Level = 2,
                IsBoss = false,
                PrefabName = ResolveMonsterPrefabName(2, seed, tileIndex, dungeonFloor, theme, dungeonConfig, isBoss: false),
                SourceCoordinate = sourceCoordinate,
                DisplayCoordinate = displayCoordinate,
                WorldPosition = GetWorldPosition(layout, displayCoordinate, Mathf.Max(0.25f, dungeonConfig?.CellWorldSize ?? 2f)),
            };
        }

        private static int GetTileIndexForBossCoordinate(Int2Data coordinate, int sourceWidth, int sourceHeight)
        {
            int clampedX = Mathf.Clamp(coordinate.X, 1, Mathf.Max(1, sourceWidth) - 2);
            int clampedY = Mathf.Clamp(coordinate.Y, 1, Mathf.Max(1, sourceHeight) - 2);
            return clampedX * sourceHeight + clampedY;
        }

        private static int GetThemeBandIndex(int dungeonFloor, DungeonConfig dungeonConfig)
        {
            int bandSize = Mathf.Max(1, dungeonConfig?.ThemeBandSize ?? 10);
            return ((Mathf.Max(1, dungeonFloor) - 1) / bandSize) + 1;
        }

        private static string ResolveMaterialPath(string preferredPath, string fallbackPath)
        {
            if (!string.IsNullOrWhiteSpace(preferredPath))
                return preferredPath;

            return fallbackPath ?? string.Empty;
        }

        private static string ResolveMonsterPrefabName(int level, int seed, int tileIndex, int dungeonFloor, DungeonThemeData theme, DungeonConfig dungeonConfig, bool isBoss)
        {
            int fallbackPoolId = level switch
            {
                1 => dungeonConfig?.FallbackMob1PoolId ?? -1,
                2 => dungeonConfig?.FallbackMob2PoolId ?? -1,
                3 => dungeonConfig?.FallbackMob3PoolId ?? -1,
                _ => dungeonConfig?.FallbackMob1PoolId ?? -1,
            };

            int themePoolId = level switch
            {
                1 => theme?.Mob1PoolId ?? -1,
                2 => theme?.Mob2PoolId ?? -1,
                3 => theme?.Mob3PoolId ?? -1,
                _ => -1,
            };
            int resolvedPoolId = themePoolId > 0 ? themePoolId : fallbackPoolId;
            return ResolveMonsterPrefabNameFromPoolIds(new[] { resolvedPoolId }, seed, tileIndex, dungeonFloor, isBoss, fallbackPoolId);
        }

        private static List<RuntimeDungeonTreasureRewardData> ResolveTreasureRewardsFromPool(int poolId, int level, int dungeonFloor, int themeId)
        {
            if (poolId <= 0)
                return new List<RuntimeDungeonTreasureRewardData>();

            DungeonTreasurePoolData pool = DataComponent.Instance?.Get<DungeonTreasurePoolData>(poolId);
            pool?.EnsureValid();
            if (pool?.Entries == null || pool.Entries.Count == 0)
                return new List<RuntimeDungeonTreasureRewardData>();

            int referenceFloor = Mathf.Max(1, dungeonFloor);
            List<DungeonTreasurePoolEntryData> entries = new();
            int totalWeight = 0;
            for (int i = 0; i < pool.Entries.Count; i++)
            {
                DungeonTreasurePoolEntryData entry = pool.Entries[i];
                if (entry == null || entry.Rewards == null || entry.Rewards.Count == 0)
                    continue;
                if (referenceFloor < entry.MinFloor || referenceFloor > entry.MaxFloor)
                    continue;

                entries.Add(entry);
                totalWeight += Mathf.Max(1, entry.Weight);
            }

            if (entries.Count == 0 || totalWeight <= 0)
                return new List<RuntimeDungeonTreasureRewardData>();

            int seed = (themeId * 19349663) ^ (level * 83492791) ^ (poolId * 297121507) ^ (referenceFloor * 4256249);
            int pick = GetDeterministicIndex(seed, totalWeight);
            for (int i = 0; i < entries.Count; i++)
            {
                pick -= Mathf.Max(1, entries[i].Weight);
                if (pick < 0)
                    return BuildRuntimeTreasureRewards(entries[i].Rewards);
            }

            return BuildRuntimeTreasureRewards(entries[0].Rewards);
        }

        private static List<RuntimeDungeonTreasureRewardData> BuildRuntimeTreasureRewards(IEnumerable<DungeonTreasureRewardEntryData> rewards)
        {
            List<RuntimeDungeonTreasureRewardData> resolvedRewards = new();
            if (rewards == null)
                return resolvedRewards;

            foreach (DungeonTreasureRewardEntryData reward in rewards)
            {
                if (!IsValidTreasureReward(reward))
                    continue;

                resolvedRewards.Add(new RuntimeDungeonTreasureRewardData
                {
                    RewardType = reward.RewardType,
                    ItemId = reward.ItemId,
                    Chance = Mathf.Clamp01(reward.Chance),
                    MinQuantity = Mathf.Max(0, reward.MinQuantity),
                    MaxQuantity = Mathf.Max(Mathf.Max(0, reward.MinQuantity), reward.MaxQuantity),
                });
            }

            return resolvedRewards;
        }

        private static List<RuntimeDungeonTreasureRewardData> CloneTreasureRewards(IEnumerable<RuntimeDungeonTreasureRewardData> rewards)
        {
            List<RuntimeDungeonTreasureRewardData> clonedRewards = new();
            if (rewards == null)
                return clonedRewards;

            foreach (RuntimeDungeonTreasureRewardData reward in rewards)
            {
                if (reward == null)
                    continue;

                clonedRewards.Add(new RuntimeDungeonTreasureRewardData
                {
                    RewardType = reward.RewardType,
                    ItemId = reward.ItemId,
                    Chance = Mathf.Clamp01(reward.Chance),
                    MinQuantity = Mathf.Max(0, reward.MinQuantity),
                    MaxQuantity = Mathf.Max(Mathf.Max(0, reward.MinQuantity), reward.MaxQuantity),
                });
            }

            return clonedRewards;
        }

        private static bool IsValidTreasureReward(DungeonTreasureRewardEntryData reward)
        {
            if (reward == null)
                return false;

            return reward.RewardType switch
            {
                DropRewardType.Money => reward.MaxQuantity > 0 || reward.MinQuantity > 0,
                _ => reward.ItemId >= 0,
            };
        }

        private static string ResolveMonsterPrefabNameFromPoolIds(
            IEnumerable<int> poolIds,
            int seed,
            int tileIndex,
            int dungeonFloor,
            bool isBoss,
            int fallbackPoolId)
        {
            List<DungeonMonsterPoolEntryData> preferredEntries = new();
            List<DungeonMonsterPoolEntryData> generalEntries = new();
            CollectEligibleMonsterEntries(poolIds, dungeonFloor, preferredEntries, generalEntries);
            if (preferredEntries.Count == 0 && generalEntries.Count == 0 && fallbackPoolId > 0)
                CollectEligibleMonsterEntries(new[] { fallbackPoolId }, dungeonFloor, preferredEntries, generalEntries);

            List<DungeonMonsterPoolEntryData> selectedEntries = isBoss
                ? preferredEntries.Count > 0 ? preferredEntries : generalEntries
                : generalEntries;
            if (selectedEntries.Count == 0)
                return string.Empty;

            int totalWeight = 0;
            for (int i = 0; i < selectedEntries.Count; i++)
                totalWeight += Mathf.Max(1, selectedEntries[i].Weight);
            if (totalWeight <= 0)
                return selectedEntries[0].UnitName ?? string.Empty;

            int pick = GetDeterministicIndex(unchecked(seed ^ tileIndex ^ (dungeonFloor * 486187739)), totalWeight);
            for (int i = 0; i < selectedEntries.Count; i++)
            {
                pick -= Mathf.Max(1, selectedEntries[i].Weight);
                if (pick < 0)
                    return selectedEntries[i].UnitName ?? string.Empty;
            }

            return selectedEntries[0].UnitName ?? string.Empty;
        }

        private static void CollectEligibleMonsterEntries(
            IEnumerable<int> poolIds,
            int dungeonFloor,
            List<DungeonMonsterPoolEntryData> preferredEntries,
            List<DungeonMonsterPoolEntryData> generalEntries)
        {
            if (poolIds == null)
                return;

            foreach (int poolId in poolIds)
            {
                if (poolId <= 0)
                    continue;

                DungeonMonsterPoolData pool = DataComponent.Instance?.Get<DungeonMonsterPoolData>(poolId);
                pool?.EnsureValid();
                if (pool?.Entries == null)
                    continue;

                for (int i = 0; i < pool.Entries.Count; i++)
                {
                    DungeonMonsterPoolEntryData entry = pool.Entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.UnitName))
                        continue;
                    if (dungeonFloor < entry.MinFloor || dungeonFloor > entry.MaxFloor)
                        continue;

                    if (entry.BossOnly)
                        preferredEntries.Add(entry);
                    else
                        generalEntries.Add(entry);
                }
            }
        }

        private static RuntimeDungeonSceneObjectSpawnData CreateExitSceneObject(
            RuntimeDungeonSceneObjectSpawnData pointData,
            RuntimeDungeonSceneData sceneData,
            int targetFloor)
        {
            if (pointData == null)
                return null;

            pointData.ObjectType = RuntimeDungeonSceneObjectType.Exit;
            pointData.PrefabName = "Exit";
            pointData.Size = Vector3.one;
            pointData.InteractionRange = sceneData.ExitInteractionRange;
            pointData.TargetFloor = Mathf.Max(1, targetFloor);
            pointData.ClosedMaterialPath = sceneData.ExitClosedMaterialPath ?? string.Empty;
            pointData.OpenMaterialPath = sceneData.ExitOpenMaterialPath ?? string.Empty;
            return pointData;
        }

        private static RuntimeDungeonSceneObjectSpawnData CreateTreasureSceneObject(
            RuntimeDungeonSceneObjectSpawnData pointData,
            List<RuntimeDungeonTreasureRewardData> rewards)
        {
            if (pointData == null)
                return null;

            pointData.ObjectType = RuntimeDungeonSceneObjectType.Treasure;
            pointData.PrefabName = "Treasure";
            pointData.Size = Vector3.one;
            pointData.InteractionRange = 1.35f;
            pointData.Rewards = CloneTreasureRewards(rewards);
            return pointData;
        }

        private static void AddEnvironmentSpawns(
            RuntimeDungeonSceneData sceneData,
            DungeonMakerTunnelingResult layout)
        {
            DungeonMakerRegionKind?[,] regionKinds = BuildRegionKindMap(layout);
            AddEnvironmentSpawnsForMask(
                sceneData,
                layout,
                BuildWalkableMask(layout, regionKinds, DungeonMakerRegionKind.Room),
                sceneData.RoomMaterialPath,
                FloorVisualDepth,
                FloorVisualZ,
                "Environment");
            AddEnvironmentSpawnsForMask(
                sceneData,
                layout,
                BuildWalkableMask(layout, regionKinds, DungeonMakerRegionKind.AnteRoom),
                sceneData.AnteRoomMaterialPath,
                FloorVisualDepth,
                FloorVisualZ,
                "Environment");
            AddEnvironmentSpawnsForMask(
                sceneData,
                layout,
                BuildWalkableMask(layout, regionKinds, DungeonMakerRegionKind.Corridor),
                sceneData.CorridorMaterialPath,
                FloorVisualDepth,
                FloorVisualZ,
                "Environment");

            bool[,] wallMask = BuildSurfaceMask(layout, static tile => IsWallTile(tile));
            List<RectInt> wallRectangles = BuildRectangles(wallMask, surfaceOnly: false);
            for (int i = 0; i < wallRectangles.Count; i++)
            {
                RectInt rectangle = wallRectangles[i];
                sceneData.EnvironmentSpawns.Add(new RuntimeDungeonEnvironmentSpawnData
                {
                    PrefabName = "Collider",
                    MaterialPath = sceneData.WallMaterialPath ?? string.Empty,
                    WorldPosition = GetWorldPositionForRectangle(rectangle, layout.DisplayWidth, layout.DisplayHeight, sceneData.CellWorldSize, WallVisualZ),
                    Size = new Vector3(
                        rectangle.width * sceneData.CellWorldSize,
                        rectangle.height * sceneData.CellWorldSize,
                        WallVisualDepth),
                });
            }
        }

        private static void AddEnvironmentSpawnsForMask(
            RuntimeDungeonSceneData sceneData,
            DungeonMakerTunnelingResult layout,
            bool[,] mask,
            string materialPath,
            float depth,
            float visualZ,
            string prefabName)
        {
            List<RectInt> rectangles = BuildRectangles(mask, surfaceOnly: false);
            for (int i = 0; i < rectangles.Count; i++)
            {
                RectInt rectangle = rectangles[i];
                sceneData.EnvironmentSpawns.Add(new RuntimeDungeonEnvironmentSpawnData
                {
                    PrefabName = prefabName,
                    MaterialPath = materialPath ?? string.Empty,
                    WorldPosition = GetWorldPositionForRectangle(rectangle, layout.DisplayWidth, layout.DisplayHeight, sceneData.CellWorldSize, visualZ),
                    Size = new Vector3(
                        rectangle.width * sceneData.CellWorldSize,
                        rectangle.height * sceneData.CellWorldSize,
                        depth),
                });
            }
        }

        private static DungeonMakerRegionKind?[,] BuildRegionKindMap(DungeonMakerTunnelingResult layout)
        {
            int sourceWidth = layout.SourceWidth;
            int sourceHeight = layout.SourceHeight;
            DungeonMakerRegionKind?[,] map = new DungeonMakerRegionKind?[layout.DisplayWidth, layout.DisplayHeight];
            IReadOnlyList<DungeonMakerRegion> regions = layout.Regions;

            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                if (region?.TileIndices == null)
                    continue;

                for (int tileIndexIndex = 0; tileIndexIndex < region.TileIndices.Length; tileIndexIndex++)
                {
                    int tileIndex = region.TileIndices[tileIndexIndex];
                    int sourceX = tileIndex / sourceHeight;
                    int sourceY = tileIndex % sourceHeight;
                    if (sourceX < 0 || sourceX >= sourceWidth || sourceY < 0 || sourceY >= sourceHeight)
                        continue;

                    map[sourceY, sourceX] = region.Kind;
                }
            }

            return map;
        }

        private static bool[,] BuildWalkableMask(
            DungeonMakerTunnelingResult layout,
            DungeonMakerRegionKind?[,] regionKinds,
            DungeonMakerRegionKind targetKind)
        {
            int width = layout.DisplayWidth;
            int height = layout.DisplayHeight;
            bool[,] mask = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsWalkableTile(layout.GetDisplayTile(x, y)))
                        continue;

                    DungeonMakerRegionKind resolvedKind = regionKinds[x, y] ?? DungeonMakerRegionKind.Corridor;
                    mask[x, y] = resolvedKind == targetKind;
                }
            }

            return mask;
        }

        private static bool[,] BuildSurfaceMask(DungeonMakerTunnelingResult layout, Func<DungeonMakerSquareData, bool> predicate)
        {
            int width = layout.DisplayWidth;
            int height = layout.DisplayHeight;
            bool[,] sourceMask = new bool[width, height];
            bool[,] surfaceMask = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    sourceMask[x, y] = predicate(layout.GetDisplayTile(x, y));
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    surfaceMask[x, y] = IsSurfaceMask(sourceMask, x, y, width, height);
            }

            return surfaceMask;
        }

        private static List<RectInt> BuildRectangles(bool[,] targetMask, bool surfaceOnly)
        {
            int width = targetMask.GetLength(0);
            int height = targetMask.GetLength(1);
            bool[,] used = new bool[width, height];
            List<RectInt> rectangles = new();

            if (surfaceOnly)
            {
                bool[,] sourceMask = targetMask;
                targetMask = new bool[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        targetMask[x, y] = IsSurfaceMask(sourceMask, x, y, width, height);
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!targetMask[x, y] || used[x, y])
                        continue;

                    int rectWidth = 0;
                    while (x + rectWidth < width && targetMask[x + rectWidth, y] && !used[x + rectWidth, y])
                        rectWidth++;

                    int rectHeight = 1;
                    bool canGrow = true;
                    while (y + rectHeight < height && canGrow)
                    {
                        for (int dx = 0; dx < rectWidth; dx++)
                        {
                            if (!targetMask[x + dx, y + rectHeight] || used[x + dx, y + rectHeight])
                            {
                                canGrow = false;
                                break;
                            }
                        }

                        if (canGrow)
                            rectHeight++;
                    }

                    for (int dy = 0; dy < rectHeight; dy++)
                    {
                        for (int dx = 0; dx < rectWidth; dx++)
                            used[x + dx, y + dy] = true;
                    }

                    rectangles.Add(new RectInt(x, y, rectWidth, rectHeight));
                }
            }

            return rectangles;
        }

        private static bool IsSurfaceMask(bool[,] mask, int x, int y, int width, int height)
        {
            if (!mask[x, y])
                return false;

            if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                return true;

            return !mask[x - 1, y]
                || !mask[x + 1, y]
                || !mask[x, y - 1]
                || !mask[x, y + 1];
        }

        private static Vector3 GetWorldPositionForRectangle(
            RectInt rectangle,
            int displayWidth,
            int displayHeight,
            float cellWorldSize,
            float z)
        {
            float halfWidth = displayWidth * 0.5f;
            float halfHeight = displayHeight * 0.5f;
            float centerX = (rectangle.x + rectangle.width * 0.5f - halfWidth) * cellWorldSize;
            float centerY = (rectangle.y + rectangle.height * 0.5f - halfHeight) * cellWorldSize;
            return new Vector3(centerX, centerY, z);
        }

        private static int GetDeterministicIndex(int seed, int count)
        {
            if (count <= 0)
                return 0;

            unchecked
            {
                uint value = (uint)seed;
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (int)(value % (uint)count);
            }
        }

        private sealed class DungeonGenerationRules
        {
            public Vector2Int LargeRoomRange = new(5, 7);
            public Vector2Int MediumRoomRange = new(10, 15);
            public Vector2Int SmallRoomRange = new(20, 25);
            public Vector2Int WalkableTileRange = new(4000, 6000);
            public bool PruneDeadEnds = true;
            public bool SpawnEncounters = true;
            public int CorridorLevel1SpawnChanceDenominator = 25;
            public EncounterCountRange AnteRoomMonsterCountRange = new(1, 2);
            public EncounterCountRange SmallRoomMonsterCountRange = new(1, 2);
            public EncounterCountRange MediumRoomMonsterCountRange = new(2, 4);
            public EncounterCountRange LargeRoomMonsterCountRange = new(4, 7);
        }

        private sealed class CandidateGenerationContext
        {
            public DungeonMakerTunnelingResult Result;
        }

        private readonly struct GeneratedDungeonPayload
        {
            public GeneratedDungeonPayload(DungeonMakerTunnelingResult layout, RuntimeDungeonSceneData sceneData)
            {
                Layout = layout;
                SceneData = sceneData;
            }

            public DungeonMakerTunnelingResult Layout { get; }
            public RuntimeDungeonSceneData SceneData { get; }
        }
    }
}
