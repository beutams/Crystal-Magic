using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.UI
{
    public enum NotificationPolicy { Stack, Merge, Queue }

    public readonly struct NotificationRequest
    {
        public readonly string Key, Text, Channel;
        public readonly float From, To, Maximum;
        public readonly int Scope, Cycle, MaxVisible, MaxQueued;
        public readonly NotificationPolicy Policy;
        public readonly Color Tint;

        public NotificationRequest(string key, string text, string channel, NotificationPolicy policy,
            float from = 0, float to = 0, float maximum = 100, int scope = 0, int cycle = 0,
            int maxVisible = 8, int maxQueued = 32, Color? tint = null)
        {
            Key = key; Text = text; Channel = channel; Policy = policy;
            From = from; To = to; Maximum = Mathf.Max(0.001f, maximum);
            Scope = scope; Cycle = cycle; MaxVisible = Mathf.Max(1, maxVisible);
            MaxQueued = Mathf.Max(1, maxQueued); Tint = tint ?? Color.white;
        }
    }

    public sealed class NotificationRecord
    {
        public int Id { get; }
        public int Revision { get; private set; }
        public NotificationRequest Request { get; private set; }
        public NotificationRecord(int id, in NotificationRequest request) { Id = id; Request = request; }
        internal void Update(in NotificationRequest request) { Request = request; Revision++; }
    }

    /// <summary>Pure queue policy; no Unity objects, animation timing or gameplay reads.</summary>
    public sealed class NotificationQueue
    {
        private readonly List<NotificationRecord> _active = new();
        private readonly List<NotificationRecord> _pending = new();
        private int _nextId, _scope;
        public IReadOnlyList<NotificationRecord> Active => _active;
        public int PendingCount => _pending.Count;

        public void Enqueue(in NotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Channel)) return;
            if (request.Scope != 0 && request.Scope != _scope)
            {
                if (_scope != 0) Clear();
                _scope = request.Scope;
            }
            if (request.Policy == NotificationPolicy.Merge)
            {
                foreach (var item in _active)
                    if (SameGroup(item.Request, request)) { item.Update(request); return; }
                foreach (var item in _pending)
                    if (SameGroup(item.Request, request))
                    {
                        var old = item.Request;
                        item.Update(new NotificationRequest(request.Key, request.Text, request.Channel, request.Policy,
                            old.From, request.To, request.Maximum, request.Scope, request.Cycle,
                            request.MaxVisible, request.MaxQueued, request.Tint));
                        return;
                    }
            }
            int count = 0;
            foreach (var item in _active) if (item.Request.Channel == request.Channel) count++;
            var record = new NotificationRecord(++_nextId, request);
            if (request.Policy == NotificationPolicy.Stack)
            {
                if (count >= request.MaxVisible)
                {
                    int oldest = _active.FindIndex(item => item.Request.Channel == record.Request.Channel);
                    if (oldest >= 0) _active.RemoveAt(oldest);
                }
                _active.Add(record);
            }
            else if (count == 0) _active.Add(record);
            else
            {
                int pendingCount = 0;
                foreach (var item in _pending) if (item.Request.Channel == request.Channel) pendingCount++;
                if (pendingCount >= request.MaxQueued)
                    _pending.RemoveAt(_pending.FindIndex(item => item.Request.Channel == record.Request.Channel));
                _pending.Add(record);
            }
        }

        public void Complete(int id)
        {
            int index = _active.FindIndex(item => item.Id == id);
            if (index < 0) return;
            string channel = _active[index].Request.Channel;
            _active.RemoveAt(index);
            int next = _pending.FindIndex(item => item.Request.Channel == channel);
            if (next < 0) return;
            _active.Add(_pending[next]);
            _pending.RemoveAt(next);
        }

        public void Clear() { _active.Clear(); _pending.Clear(); _scope = 0; }

        private static bool SameGroup(in NotificationRequest a, in NotificationRequest b) =>
            a.Key == b.Key && a.Channel == b.Channel && a.Scope == b.Scope && a.Cycle == b.Cycle;
    }
}
