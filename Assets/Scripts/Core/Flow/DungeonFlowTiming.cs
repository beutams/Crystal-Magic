using System;
using System.Globalization;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Measures the dungeon entry pipeline so slow stages can be identified from the local log.
    /// </summary>
    internal static class DungeonFlowTiming
    {
        private const int StageCount = 17;

        private static readonly Stopwatch FlowStopwatch = new();
        private static readonly Stopwatch StageStopwatch = new();

        private static bool _isActive;
        private static int _activeStageNumber;
        private static string _activeStageName;
        private static string _flowId;

        public static void Begin(LoadGameContext context)
        {
            if (_isActive)
                Fail("A new dungeon flow started before the previous flow completed.");

            _flowId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            FlowStopwatch.Restart();
            StageStopwatch.Reset();
            _activeStageNumber = 0;
            _activeStageName = null;
            _isActive = true;

            Debug.Log($"[DungeonFlowTiming][FLOW START] Id={_flowId} Theme={context?.DungeonThemeId ?? -1} Floor={context?.DungeonFloor ?? 1}");
        }

        public static void EnsureStarted(LoadGameContext context)
        {
            if (!_isActive)
                Begin(context);
        }

        public static void BeginStage(int stageNumber, string stageName, string detail = null)
        {
            if (!_isActive)
                return;

            if (_activeStageNumber != 0)
            {
                Debug.LogWarning($"[DungeonFlowTiming][{_activeStageNumber:00}/{StageCount}][WARN] Stage was not ended before starting stage {stageNumber:00}.");
                EndStage(_activeStageNumber, "Implicitly ended before the next stage.");
            }

            _activeStageNumber = stageNumber;
            _activeStageName = stageName ?? string.Empty;
            StageStopwatch.Restart();
            Debug.Log($"[DungeonFlowTiming][{stageNumber:00}/{StageCount}][START] {_activeStageName}{FormatDetail(detail)}");
        }

        public static void EndStage(int stageNumber, string detail = null)
        {
            if (!_isActive)
                return;

            if (_activeStageNumber != stageNumber)
            {
                Debug.LogWarning($"[DungeonFlowTiming][WARN] Tried to end stage {stageNumber:00}, but active stage is {_activeStageNumber:00}.");
                return;
            }

            StageStopwatch.Stop();
            Debug.Log(
                $"[DungeonFlowTiming][{stageNumber:00}/{StageCount}][END] {_activeStageName} " +
                $"Duration={FormatMilliseconds(StageStopwatch.Elapsed.TotalMilliseconds)} " +
                $"Cumulative={FormatMilliseconds(FlowStopwatch.Elapsed.TotalMilliseconds)}" +
                FormatDetail(detail));
            _activeStageNumber = 0;
            _activeStageName = null;
        }

        public static void Complete(string detail = null)
        {
            if (!_isActive)
                return;

            if (_activeStageNumber != 0)
                EndStage(_activeStageNumber, "Flow completed while this stage was still active.");

            Debug.Log(
                $"[DungeonFlowTiming][FLOW COMPLETE] Id={_flowId} " +
                $"Total={FormatMilliseconds(FlowStopwatch.Elapsed.TotalMilliseconds)}" +
                FormatDetail(detail));
            Reset();
        }

        public static void Fail(string detail)
        {
            if (!_isActive)
                return;

            double stageMilliseconds = StageStopwatch.IsRunning ? StageStopwatch.Elapsed.TotalMilliseconds : 0d;
            Debug.LogError(
                $"[DungeonFlowTiming][FLOW FAILED] Id={_flowId} " +
                $"ActiveStage={_activeStageNumber:00}/{StageCount} {_activeStageName} " +
                $"StageDuration={FormatMilliseconds(stageMilliseconds)} " +
                $"Cumulative={FormatMilliseconds(FlowStopwatch.Elapsed.TotalMilliseconds)}" +
                FormatDetail(detail));
            Reset();
        }

        private static void Reset()
        {
            _isActive = false;
            _activeStageNumber = 0;
            _activeStageName = null;
            _flowId = null;
            FlowStopwatch.Reset();
            StageStopwatch.Reset();
        }

        private static string FormatDetail(string detail)
        {
            return string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Detail={detail}";
        }

        private static string FormatMilliseconds(double milliseconds)
        {
            return $"{milliseconds.ToString("F3", CultureInfo.InvariantCulture)}ms";
        }
    }
}
