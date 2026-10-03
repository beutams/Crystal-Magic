using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CrystalMagic.Core
{
    /// <summary>Wall-clock diagnostics only; never changes loading, timeouts or scheduling.</summary>
    internal static class SceneLoadTiming
    {
        private sealed class Session
        {
            public readonly string Id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + ++_sessionNumber;
            public readonly Stopwatch Clock = Stopwatch.StartNew();
            public readonly List<Stage> ActiveStages = new();
            public bool Finished;
        }

        internal sealed class Stage : IDisposable
        {
            private readonly Session _session;
            private readonly double _startMilliseconds;
            private bool _disposed;
            internal string Name { get; }

            // The factory keeps the session type private to this diagnostic helper.
            internal static Stage Create(string name) => new Stage(_sessionCurrent, name);

            private Stage(Session session, string name)
            {
                _session = session;
                Name = name;
                _startMilliseconds = session.Clock.Elapsed.TotalMilliseconds;
                session.ActiveStages.Add(this);
                Write(session, "START", name);
            }

            internal string Describe() => $"{Name} Duration={Milliseconds(_session.Clock.Elapsed.TotalMilliseconds - _startMilliseconds)}";

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _session.ActiveStages.Remove(this);
                if (!_session.Finished)
                    Write(_session, "END", Describe());
            }
        }

        private static Session _sessionCurrent;
        private static int _sessionNumber;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _sessionCurrent = null;

        public static void Begin(string detail)
        {
            if (_sessionCurrent != null)
                Finish("Superseded by another scene load.");
            _sessionCurrent = new Session();
            Write(_sessionCurrent, "FLOW START", detail);
#if UNITY_EDITOR
            Mark("EDITOR", "Import/baking details: Editor.log and <Project>/Logs/AssetImportWorker*.log. " +
                "ImportAndHeader is an observed combined wait, not a measurement of baking alone.");
#endif
        }

        public static void EnsureStarted(string detail)
        {
            if (_sessionCurrent == null)
                Begin(detail);
        }

        public static Stage Measure(string name) => _sessionCurrent == null ? null : Stage.Create(name);

        public static void Mark(string kind, string detail)
        {
            if (_sessionCurrent != null)
                Write(_sessionCurrent, kind, detail);
        }

        public static void Finish(string error = null)
        {
            Session session = _sessionCurrent;
            if (session == null)
                return;

            if (error != null)
            {
                foreach (Stage stage in session.ActiveStages)
                    Write(session, "INTERRUPTED", stage.Describe());
            }
            Write(session, error == null ? "FLOW COMPLETE" : "FLOW FAILED", error ?? "Input and simulation unlocked.");
            session.Clock.Stop();
            session.Finished = true;
            _sessionCurrent = null;
        }

        public static string Milliseconds(double value) => value.ToString("F3", CultureInfo.InvariantCulture) + "ms";

        private static void Write(Session session, string kind, string detail)
        {
            // Avoid collecting a stack trace for every diagnostic line.
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}",
                $"[SceneLoadTiming][{session.Id}][{kind}] Total={Milliseconds(session.Clock.Elapsed.TotalMilliseconds)} {detail}");
        }
    }
}
