using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.UI
{
    [Serializable]
    public sealed class NotificationTemplate
    {
        public string Key, Channel, TextKey;
        public NotificationItemView Prefab;
        public NotificationPolicy Policy;
        public int MaxVisible = 8, MaxQueued = 32;
        public float Maximum = 100;
    }

    public sealed class NotificationUI : UIBase<NotificationUIData, NotificationUIModel>
    {
        public NotificationTemplate[] Templates = Array.Empty<NotificationTemplate>();
        private readonly Dictionary<int, NotificationItemView> _items = new();
        private readonly List<int> _completed = new();
        public event Action<int> Completed;

        public NotificationTemplate FindTemplate(string key)
        {
            foreach (var template in Templates) if (template.Key == key) return template;
            return null;
        }

        protected override void RefreshView()
        {
            if (Model == null) return;
            var wanted = new HashSet<int>();
            foreach (var record in Model.Active) wanted.Add(record.Id);
            var remove = new List<int>();
            foreach (var pair in _items) if (!wanted.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (int id in remove) Release(id);
            var slots = new Dictionary<string, int>();
            foreach (var record in Model.Active)
            {
                var request = record.Request;
                if (!_items.TryGetValue(record.Id, out var item))
                {
                    NotificationTemplate template = FindTemplate(request.Key);
                    if (template?.Prefab == null) continue;
                    item = UISubViewBase.AcquireFromPool(template.Prefab, UI.Root.RectTransform);
                    if (item == null) continue;
                    _items.Add(record.Id, item);
                }
                slots.TryGetValue(request.Channel, out int slot);
                slots[request.Channel] = slot + 1;
                item.Present(record, slot);
            }
        }

        public override void OnUpdate()
        {
            _completed.Clear();
            foreach (var pair in _items)
                if (pair.Value.Tick(Time.unscaledDeltaTime)) _completed.Add(pair.Key);
            foreach (int id in _completed) Completed?.Invoke(id);
        }

        public override void OnClose()
        {
            base.OnClose();
            var ids = new List<int>(_items.Keys);
            foreach (int id in ids) Release(id);
        }

        private void Release(int id)
        {
            var item = _items[id];
            _items.Remove(id);
            item.ResetForPool();
            UISubViewBase.ReleaseToPool(item);
        }
    }
}
