using System;
using System.IO;
using System.Text;
using CrystalMagic.Game.MapDemo;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal static class DungeonGenerationAttemptRecorder
    {
        private const int MaxRecordedAttempts = 500;

        private static string _sessionLogPath;
        private static int _recordedAttempts;
        private static bool _suppressedFurtherAttempts;

        public static void BeginSession(
            int dungeonFloor,
            int masterSeed,
            int maxAttemptCount,
            bool useBestCandidateFallback,
            DungeonMakerTunnelingConfig config,
            Vector2Int largeRoomRange,
            Vector2Int mediumRoomRange,
            Vector2Int smallRoomRange,
            Vector2Int walkableTileRange)
        {
            try
            {
                string directory = ResolveLogDirectory();
                Directory.CreateDirectory(directory);

                string fileName = $"dungeon_generation_floor{dungeonFloor}_{DateTime.Now:yyyyMMdd_HHmmss}_{masterSeed}.log";
                _sessionLogPath = Path.Combine(directory, fileName);
                _recordedAttempts = 0;
                _suppressedFurtherAttempts = false;

                StringBuilder builder = new();
                builder.AppendLine("Dungeon Generation Attempt Log");
                builder.AppendLine($"CreatedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine($"Floor: {dungeonFloor}");
                builder.AppendLine($"MasterSeed: {masterSeed}");
                builder.AppendLine($"AttemptLimit: {maxAttemptCount}");
                builder.AppendLine($"UseBestCandidateFallback: {useBestCandidateFallback}");
                builder.AppendLine();
                builder.AppendLine("Qualification Rules");
                builder.AppendLine($"  LargeRooms: {FormatRange(largeRoomRange)}");
                builder.AppendLine($"  MediumRooms: {FormatRange(mediumRoomRange)}");
                builder.AppendLine($"  SmallRooms: {FormatRange(smallRoomRange)}");
                builder.AppendLine($"  WalkableTiles: {FormatRange(walkableTileRange)}");
                builder.AppendLine("  EntryExitRequirement: LargeRooms > 0 && SmallRooms > 0");
                builder.AppendLine();
                builder.AppendLine("Generator Config");
                builder.AppendLine($"  MapSize: {config.DimX} x {config.DimY}");
                builder.AppendLine($"  MaxRooms(S/M/L): {config.MaxSmallDungeonRooms}/{config.MaxMediumDungeonRooms}/{config.MaxLargeDungeonRooms}");
                builder.AppendLine($"  MinRoomSize(S/M/L): {config.MinSmallRoomSize}/{config.MinMediumRoomSize}/{config.MinLargeRoomSize}");
                builder.AppendLine($"  MaxRoomSize: {config.MaxRoomSize}");
                builder.AppendLine($"  TunnelJoinDist: {config.TunnelJoinDist}");
                builder.AppendLine($"  Patience: {config.Patience}");
                builder.AppendLine($"  Mutator: {config.Mutator}");
                builder.AppendLine($"  RoomAspectRatio: {config.RoomAspectRatio:0.###}");
                builder.AppendLine($"  InitialTunnelers: {(config.Tunnelers == null ? 0 : config.Tunnelers.Length)}");
                builder.AppendLine();
                builder.AppendLine("Attempts");

                File.WriteAllText(_sessionLogPath, builder.ToString(), Encoding.UTF8);
                Debug.Log($"[DungeonGenerationAttemptRecorder] Writing generation diagnostics to {_sessionLogPath}");
            }
            catch (Exception ex)
            {
                _sessionLogPath = null;
                Debug.LogWarning($"[DungeonGenerationAttemptRecorder] Failed to start logging: {ex.Message}");
            }
        }

        public static void RecordAttempt(
            int attemptNumber,
            int candidateSeed,
            int candidateScore,
            int bestScoreSoFar,
            DungeonMakerTunnelingStats stats,
            bool qualified,
            Vector2Int largeRoomRange,
            Vector2Int mediumRoomRange,
            Vector2Int smallRoomRange,
            Vector2Int walkableTileRange)
        {
            if (string.IsNullOrWhiteSpace(_sessionLogPath) || _suppressedFurtherAttempts)
                return;

            if (_recordedAttempts >= MaxRecordedAttempts)
            {
                AppendLine($"[Recorder] Reached {MaxRecordedAttempts} recorded attempts. Further attempt lines are suppressed.");
                _suppressedFurtherAttempts = true;
                return;
            }

            _recordedAttempts++;
            string qualificationSummary = BuildQualificationSummary(stats, largeRoomRange, mediumRoomRange, smallRoomRange, walkableTileRange);
            string statsSummary = BuildStatsSummary(stats);
            AppendLine(
                $"[Attempt {attemptNumber}] Seed={candidateSeed} Score={candidateScore} BestScore={bestScoreSoFar} Qualified={qualified} | {statsSummary} | {qualificationSummary}");
        }

        public static void RecordAccepted(int attemptNumber, int candidateSeed, int candidateScore, DungeonMakerTunnelingStats stats)
        {
            if (string.IsNullOrWhiteSpace(_sessionLogPath))
                return;

            AppendLine($"[Result] Accepted attempt {attemptNumber} Seed={candidateSeed} Score={candidateScore} | {BuildStatsSummary(stats)}");
        }

        public static void RecordFallbackAccepted(int attemptNumber, int candidateSeed, int candidateScore, int searchedAttempts, DungeonMakerTunnelingStats stats)
        {
            if (string.IsNullOrWhiteSpace(_sessionLogPath))
                return;

            AppendLine(
                $"[Result] Fallback accepted after {searchedAttempts} attempts. Using attempt {attemptNumber} Seed={candidateSeed} Score={candidateScore} | {BuildStatsSummary(stats)}");
        }

        public static void RecordFailure(int searchedAttempts, int bestScore)
        {
            if (string.IsNullOrWhiteSpace(_sessionLogPath))
                return;

            AppendLine($"[Result] Search failed after {searchedAttempts} attempts. BestScore={bestScore}");
        }

        private static string ResolveLogDirectory()
        {
            string baseDirectory = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "DungeonGenerationLogs"))
                : Path.Combine(Application.persistentDataPath, "DungeonGenerationLogs");
            return baseDirectory;
        }

        private static void AppendLine(string line)
        {
            try
            {
                File.AppendAllText(_sessionLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DungeonGenerationAttemptRecorder] Failed to append log line: {ex.Message}");
            }
        }

        private static string BuildStatsSummary(DungeonMakerTunnelingStats stats)
        {
            if (stats == null)
                return "Stats=null";

            return
                $"Rooms(S/M/L/A)={stats.SmallRooms}/{stats.MediumRooms}/{stats.LargeRooms}/{stats.AnteRooms}, " +
                $"TotalRooms={stats.TotalRooms}, Walkable={stats.WalkableTiles}, Blocked={stats.BlockedTiles}, " +
                $"RoomTiles={stats.RoomTiles}, TunnelTiles={stats.TunnelTiles}, MobTiles={stats.MobTiles}, TreasureTiles={stats.TreasureTiles}";
        }

        private static string BuildQualificationSummary(
            DungeonMakerTunnelingStats stats,
            Vector2Int largeRoomRange,
            Vector2Int mediumRoomRange,
            Vector2Int smallRoomRange,
            Vector2Int walkableTileRange)
        {
            if (stats == null)
                return "Checks=StatsMissing";

            StringBuilder builder = new("Checks=");
            bool hasEntryExit = stats.LargeRooms > 0 && stats.SmallRooms > 0;
            builder.Append("EntryExit=").Append(hasEntryExit ? "PASS" : "FAIL");
            builder.Append("; LargeRooms=").Append(FormatCheck(stats.LargeRooms, largeRoomRange));
            builder.Append("; MediumRooms=").Append(FormatCheck(stats.MediumRooms, mediumRoomRange));
            builder.Append("; SmallRooms=").Append(FormatCheck(stats.SmallRooms, smallRoomRange));
            builder.Append("; WalkableTiles=").Append(FormatCheck(stats.WalkableTiles, walkableTileRange));
            return builder.ToString();
        }

        private static string FormatCheck(int value, Vector2Int range)
        {
            Vector2Int normalized = NormalizeRange(range);
            bool pass = value >= normalized.x && value <= normalized.y;
            return $"{value} {(pass ? "PASS" : "FAIL")} target={normalized.x}-{normalized.y}";
        }

        private static string FormatRange(Vector2Int range)
        {
            Vector2Int normalized = NormalizeRange(range);
            return $"{normalized.x} - {normalized.y}";
        }

        private static Vector2Int NormalizeRange(Vector2Int range)
        {
            return new Vector2Int(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }
    }
}
