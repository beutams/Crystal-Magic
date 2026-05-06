using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class UnitHealthBarManager : IDisposable
    {
        private const string GroupName = "Bottom";
        private const float WorldYOffset = 1.4f;

        private readonly Dictionary<Entity, ActiveBar> _activeBars = new();
        private readonly List<Entity> _cleanupEntities = new();

        private RectTransform _rootRect;
        private Camera _currentCamera;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized)
                return;

            EventComponent.Instance.Subscribe<UnitDamagedEvent>(HandleUnitDamaged);
            ResolveFloatingRoot();
            _initialized = true;
        }

        public void Tick()
        {
            if (!_initialized || _activeBars.Count == 0)
                return;

            if (!ResolveFloatingRoot())
                return;

            UpdateBars();
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            EventComponent.Instance.Unsubscribe<UnitDamagedEvent>(HandleUnitDamaged);
            ReleaseAllBars();
            _rootRect = null;
            _currentCamera = null;
            _initialized = false;
        }

        private void HandleUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!IsEnemyUnit(gameEvent.TargetEntity))
                return;

            ActiveBar bar = GetOrCreateBar(gameEvent.TargetEntity);
            if (bar == null)
                return;

            bar.HideAtTime = Time.time + UIComponent.Instance.GetUnitHealthBarShowSeconds();
            bar.Model?.SetHealth(gameEvent.CurrentHealth, gameEvent.MaxHealth);
            bar.Model?.SetVisible(true);
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

        private ActiveBar GetOrCreateBar(Entity entity)
        {
            if (_activeBars.TryGetValue(entity, out ActiveBar existingBar) && existingBar.View != null)
                return existingBar;

            if (!ResolveFloatingRoot())
                return null;

            UnitHealthBarUI view = UIComponent.Instance.Open<UnitHealthBarUI>();
            if (view == null)
                return null;

            UnitHealthBarUIModel model = UIComponent.Instance.GetModel<UnitHealthBarUIModel>(view);
            if (model == null)
            {
                UIComponent.Instance.ReleaseUI(view);
                return null;
            }

            UIComponent.Instance.SetLifetime(view, UILifetime.Manual);
            view.PrepareForFloatingRoot(_rootRect);
            model.SetVisible(true);

            ActiveBar bar = new ActiveBar
            {
                Entity = entity,
                View = view,
                Model = model,
            };
            _activeBars[entity] = bar;
            return bar;
        }

        private void UpdateBars()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || _rootRect == null || _currentCamera == null)
                return;

            EntityManager entityManager = world.EntityManager;
            _cleanupEntities.Clear();

            foreach (KeyValuePair<Entity, ActiveBar> pair in _activeBars)
            {
                ActiveBar bar = pair.Value;
                if (bar == null || bar.View == null)
                {
                    _cleanupEntities.Add(pair.Key);
                    continue;
                }

                if (Time.time >= bar.HideAtTime)
                {
                    _cleanupEntities.Add(pair.Key);
                    continue;
                }

                Entity entity = pair.Key;
                if (!entityManager.Exists(entity)
                    || !entityManager.HasComponent<LocalToWorld>(entity)
                    || !entityManager.HasComponent<UnitVitalityComponent>(entity))
                {
                    _cleanupEntities.Add(entity);
                    continue;
                }

                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
                LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(entity);
                Vector3 worldPosition = (Vector3)localToWorld.Position + Vector3.up * WorldYOffset;
                Vector3 screenPosition = _currentCamera.WorldToScreenPoint(worldPosition);
                if (screenPosition.z <= 0f)
                {
                    bar.Model?.SetVisible(false);
                    continue;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPosition, _currentCamera, out Vector2 localPoint))
                {
                    bar.Model?.UpdateDisplay(vitality.CurrentHealth, vitality.RealMaxHealth, localPoint, true);
                }
            }

            for (int i = 0; i < _cleanupEntities.Count; i++)
            {
                ReleaseBar(_cleanupEntities[i]);
            }
        }

        private static bool IsEnemyUnit(Entity entity)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            EntityManager entityManager = world.EntityManager;
            if (!entityManager.Exists(entity)
                || !entityManager.HasComponent<UnitVitalityComponent>(entity)
                || !entityManager.HasComponent<UnitFactionComponent>(entity))
            {
                return false;
            }

            return entityManager.GetComponentData<UnitFactionComponent>(entity).Value == UnitFactionType.Enemy;
        }

        private void ReleaseBar(Entity entity)
        {
            if (!_activeBars.TryGetValue(entity, out ActiveBar bar))
                return;

            _activeBars.Remove(entity);
            if (bar?.View != null)
                UIComponent.Instance.ReleaseUI(bar.View);
        }

        private void ReleaseAllBars()
        {
            foreach (KeyValuePair<Entity, ActiveBar> pair in _activeBars)
            {
                if (pair.Value?.View != null)
                    UIComponent.Instance.ReleaseUI(pair.Value.View);
            }

            _activeBars.Clear();
            _cleanupEntities.Clear();
        }

        private sealed class ActiveBar
        {
            public Entity Entity;
            public UnitHealthBarUI View;
            public UnitHealthBarUIModel Model;
            public float HideAtTime;
        }
    }
}
