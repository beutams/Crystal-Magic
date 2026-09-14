using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class DamageNumberManager : IDisposable
    {
        private const string GroupName = "Bottom";
        private const string DamageNumberAssetName = "DamageNumberItem";
        private const int InitialPoolSize = 8;
        private const int MaxActiveNumbers = 32;
        private const float WorldYOffset = -1.8f;
        private const float FloatDurationSeconds = 0.65f;
        private const float FloatDistance = 84f;
        private const float HorizontalSpacing = 18f;

        private readonly List<DamageNumberItem> _activeItems = new();

        private RectTransform _rootRect;
        private Camera _currentCamera;
        private int _spawnSequence;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized)
                return;

            PoolComponent.Instance.EnsurePool(
                AssetPathHelper.GetUIAsset(DamageNumberAssetName),
                MaxActiveNumbers,
                InitialPoolSize);
            EventComponent.Instance.Subscribe<DamageAppliedEvent>(HandleDamageApplied);
            _initialized = true;
        }

        public void Tick()
        {
            if (!_initialized)
                return;

            for (int i = _activeItems.Count - 1; i >= 0; i--)
            {
                DamageNumberItem item = _activeItems[i];
                if (item == null || item.Tick(Time.deltaTime))
                    ReleaseAt(i);
            }
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            EventComponent.Instance.Unsubscribe<DamageAppliedEvent>(HandleDamageApplied);
            for (int i = _activeItems.Count - 1; i >= 0; i--)
                ReleaseAt(i);

            _rootRect = null;
            _currentCamera = null;
            _spawnSequence = 0;
            _initialized = false;
        }

        private void HandleDamageApplied(DamageAppliedEvent gameEvent)
        {
            if (gameEvent.Amount <= 0f || !ResolveFloatingRoot())
                return;

            Vector3 screenPosition = _currentCamera.WorldToScreenPoint((Vector3)gameEvent.WorldPosition + Vector3.up * WorldYOffset);
            if (screenPosition.z <= 0f ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPosition, _currentCamera, out Vector2 localPoint))
            {
                return;
            }

            if (_activeItems.Count >= MaxActiveNumbers)
                ReleaseAt(0);

            GameObject instance = PoolComponent.Instance.Get(AssetPathHelper.GetUIAsset(DamageNumberAssetName));
            if (instance == null)
                return;

            DamageNumberItem item = instance.GetComponent<DamageNumberItem>();
            if (item == null)
            {
                PoolComponent.Instance.Release(instance);
                Debug.LogError("[DamageNumberManager] DamageNumberItem prefab is missing its presenter component.");
                return;
            }

            int lane = _spawnSequence++ % 5 - 2;
            float horizontalOffset = lane * HorizontalSpacing;
            item.transform.SetParent(_rootRect, false);
            item.transform.SetAsLastSibling();
            item.Show(
                gameEvent.Amount,
                localPoint + new Vector2(horizontalOffset, 0f),
                new Vector2(horizontalOffset * 0.5f, FloatDistance),
                FloatDurationSeconds);
            _activeItems.Add(item);
        }

        private bool ResolveFloatingRoot()
        {
            UIGroup group = UIComponent.Instance.GetGroup<UIGroup>(GroupName);
            if (group == null)
                return false;

            _rootRect = group.transform as RectTransform;
            Canvas canvas = group.GetComponent<Canvas>();
            _currentCamera = canvas != null ? canvas.worldCamera : CameraComponent.Instance.Current;
            return _rootRect != null && _currentCamera != null;
        }

        private void ReleaseAt(int index)
        {
            DamageNumberItem item = _activeItems[index];
            _activeItems.RemoveAt(index);
            if (item != null)
                PoolComponent.Instance.Release(item.gameObject);
        }
    }
}
