using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class PickupTipManager : IDisposable
    {
        private const string GroupName = "Bottom";
        private const string AssetName = "PickupTipItem";
        private const int InitialPoolSize = 4;
        private const int MaxActiveTips = 8;
        private const float DurationSeconds = 1.1f;

        private readonly List<PickupTipItem> _activeItems = new();
        private RectTransform _rootRect;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized)
                return;

            PoolComponent.Instance.EnsurePool(
                AssetPathHelper.GetUIAsset(AssetName),
                MaxActiveTips,
                InitialPoolSize);
            EventComponent.Instance.Subscribe<PickupFeedbackEvent>(HandleFeedback);
            _initialized = true;
        }

        public void Tick()
        {
            if (!_initialized)
                return;

            for (int i = _activeItems.Count - 1; i >= 0; i--)
            {
                PickupTipItem item = _activeItems[i];
                if (item == null || item.Tick(Time.deltaTime))
                    ReleaseAt(i);
            }
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            EventComponent.Instance.Unsubscribe<PickupFeedbackEvent>(HandleFeedback);
            for (int i = _activeItems.Count - 1; i >= 0; i--)
                ReleaseAt(i);

            _rootRect = null;
            _initialized = false;
        }

        private void HandleFeedback(PickupFeedbackEvent gameEvent)
        {
            if (!ResolveRoot())
                return;

            if (_activeItems.Count >= MaxActiveTips)
                ReleaseAt(0);

            string text;
            Color color = Color.white;
            switch (gameEvent.Type)
            {
                case PickupFeedbackType.Item:
                    ItemData itemData = DataComponent.Instance.Get<ItemData>(gameEvent.ItemId);
                    string itemName = itemData != null && !string.IsNullOrWhiteSpace(itemData.Name)
                        ? itemData.Name
                        : $"Item {gameEvent.ItemId}";
                    text = gameEvent.Amount > 1 ? $"{itemName} x{gameEvent.Amount}" : itemName;
                    break;
                case PickupFeedbackType.Money:
                    string moneyName = LocalizationComponent.Instance.Get("world.drop.money");
                    text = gameEvent.Amount > 1 ? $"{moneyName} x{gameEvent.Amount}" : moneyName;
                    break;
                default:
                    text = LocalizationComponent.Instance.Get("ui.shop.inventory_full");
                    color = new Color(1f, 0.45f, 0.35f, 1f);
                    break;
            }

            GameObject instance = PoolComponent.Instance.Get(AssetPathHelper.GetUIAsset(AssetName));
            if (instance == null)
                return;

            PickupTipItem item = instance.GetComponent<PickupTipItem>();
            if (item == null)
            {
                PoolComponent.Instance.Release(instance);
                Debug.LogError("[PickupTipManager] PickupTipItem prefab is missing its presenter component.");
                return;
            }

            item.transform.SetParent(_rootRect, false);
            item.transform.SetAsLastSibling();
            item.Show(text, color, new Vector2(0f, -120f + _activeItems.Count * 24f), DurationSeconds);
            _activeItems.Add(item);
        }

        private bool ResolveRoot()
        {
            UIGroup group = UIComponent.Instance.GetGroup<UIGroup>(GroupName);
            _rootRect = group != null ? group.transform as RectTransform : null;
            return _rootRect != null;
        }

        private void ReleaseAt(int index)
        {
            PickupTipItem item = _activeItems[index];
            _activeItems.RemoveAt(index);
            if (item != null)
                PoolComponent.Instance.Release(item.gameObject);
        }
    }
}
