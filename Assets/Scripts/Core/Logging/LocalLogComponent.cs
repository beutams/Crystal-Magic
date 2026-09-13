using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Captures Unity logs and persists them to a per-session file.
    /// </summary>
    public sealed class LocalLogComponent : GameComponent<LocalLogComponent>
    {
        private const string LogFolderName = "Logs";
        private const int MaxPendingLogCount = 4096;
        private const int MaxLogsFlushedPerFrame = 1000;

        private readonly object _pendingLogsLock = new();
        private readonly Queue<PendingLog> _pendingLogs = new();

        private StreamWriter _writer;
        private string _logDirectoryPath;
        private string _logFilePath;
        private volatile bool _isLogging;
        private bool _isLogCallbackSubscribed;
        private bool _hasReportedFailure;

        private readonly struct PendingLog
        {
            public PendingLog(DateTime timestamp, LogType type, string message, string stackTrace)
            {
                Timestamp = timestamp;
                Type = type;
                Message = message;
                StackTrace = stackTrace;
            }

            public DateTime Timestamp { get; }
            public LogType Type { get; }
            public string Message { get; }
            public string StackTrace { get; }
        }

        public override int Priority => 1;

        /// <summary>
        /// Gets the directory containing the current and previous session logs.
        /// </summary>
        public string LogDirectoryPath => _logDirectoryPath;

        /// <summary>
        /// Gets the current session log file path.
        /// </summary>
        public string LogFilePath => _logFilePath;

        protected override void Awake()
        {
            InitializeSingletonInstance(this);
        }

        public override void Initialize()
        {
            StartLogging();
            base.Initialize();
        }

        private void Update()
        {
            FlushPendingLogs(MaxLogsFlushedPerFrame);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                FlushPendingLogs();
        }

        public override void Cleanup()
        {
            StopLogging();
            base.Cleanup();
        }

        protected override void OnDestroy()
        {
            StopLogging();
            base.OnDestroy();
        }

        private void StartLogging()
        {
            if (_isLogging)
                return;

            try
            {
                _logDirectoryPath = Path.Combine(Application.persistentDataPath, LogFolderName);
                Directory.CreateDirectory(_logDirectoryPath);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                _logFilePath = Path.Combine(_logDirectoryPath, $"GameLog_{timestamp}.log");
                FileStream fileStream = new(_logFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fileStream, new UTF8Encoding(false), 4096, false)
                {
                    AutoFlush = true,
                };

                _isLogging = true;
                Application.logMessageReceivedThreaded += HandleLogMessage;
                _isLogCallbackSubscribed = true;
                WriteDirect($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}][Session] Started");
                Debug.Log($"[LocalLogComponent] Writing logs to: {_logFilePath}");
            }
            catch (Exception ex)
            {
                StopLogging();
                ReportFailure($"Failed to start local logging: {ex.Message}");
            }
        }

        private void StopLogging()
        {
            if (!_isLogging && !_isLogCallbackSubscribed && _writer == null)
                return;

            UnsubscribeLogCallback();
            FlushPendingLogs();

            if (_writer != null)
            {
                try
                {
                    WriteDirect($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}][Session] Ended");
                }
                catch (Exception ex)
                {
                    ReportFailure($"Failed to finalize local log: {ex.Message}");
                }
            }

            CloseWriter();
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_isLogging)
                return;

            lock (_pendingLogsLock)
            {
                if (!_isLogging)
                    return;

                while (_pendingLogs.Count >= MaxPendingLogCount)
                    _pendingLogs.Dequeue();

                _pendingLogs.Enqueue(new PendingLog(DateTime.Now, type, condition, stackTrace));
            }
        }

        private void FlushPendingLogs(int maxCount = int.MaxValue)
        {
            if (_writer == null)
                return;

            List<PendingLog> batch = null;
            lock (_pendingLogsLock)
            {
                int count = Math.Min(maxCount, _pendingLogs.Count);
                if (count == 0)
                    return;

                batch = new List<PendingLog>(count);
                for (int i = 0; i < count; i++)
                    batch.Add(_pendingLogs.Dequeue());
            }

            try
            {
                foreach (PendingLog pendingLog in batch)
                    WriteDirect(FormatLog(pendingLog));
            }
            catch (Exception ex)
            {
                UnsubscribeLogCallback();
                ReportFailure($"Failed to write local log: {ex.Message}");
                CloseWriter();
            }
        }

        private void UnsubscribeLogCallback()
        {
            if (!_isLogCallbackSubscribed)
                return;

            Application.logMessageReceivedThreaded -= HandleLogMessage;
            _isLogCallbackSubscribed = false;
            _isLogging = false;
        }

        private void WriteDirect(string content)
        {
            if (_writer == null)
                return;

            _writer.WriteLine(content);
        }

        private void CloseWriter()
        {
            _isLogging = false;

            lock (_pendingLogsLock)
                _pendingLogs.Clear();

            if (_writer == null)
                return;

            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch (Exception ex)
            {
                ReportFailure($"Failed to close local log: {ex.Message}");
            }
            finally
            {
                _writer = null;
            }
        }

        private void ReportFailure(string message)
        {
            if (_hasReportedFailure)
                return;

            _hasReportedFailure = true;
            Debug.LogError($"[LocalLogComponent] {message}");
        }

        private static string FormatLog(PendingLog pendingLog)
        {
            StringBuilder builder = new();
            builder.Append('[')
                .Append(pendingLog.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append("][")
                .Append(pendingLog.Type)
                .Append("] ")
                .Append(pendingLog.Message);

            if (!string.IsNullOrWhiteSpace(pendingLog.StackTrace))
            {
                builder.AppendLine();
                builder.Append(pendingLog.StackTrace.TrimEnd());
            }

            return builder.ToString();
        }
    }
}
