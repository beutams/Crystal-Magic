using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
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
        private readonly List<UnitHealthBarBuffDisplayData> _buffDisplayBuffer = new();

        private UnitHealthBarUI _rootView;
        private RectTransform _rootRect;
        private Camera _currentCamera;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized)
                return;

            EventComponent.Instance.Subscribe<UnitDamagedEvent>(HandleUnitDamaged);
            EnsureRootView();
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
            ReleaseRootView();
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
            _rootView?.UpdateBar(bar.Handle, gameEvent.CurrentHealth, gameEvent.MaxHealth, Vector2.zero, true);
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

        private bool EnsureRootView()
        {
            if (_rootView != null)
                return true;

            if (UIComponent.Instance == null)
                return false;

            _rootView = UIComponent.Instance.Open<UnitHealthBarUI>();
            if (_rootView == null)
                return false;

            UIComponent.Instance.SetLifetime(_rootView, UILifetime.Manual);
            _rootView.PrepareForFloatingRoot();
            return true;
        }

        private ActiveBar GetOrCreateBar(Entity entity)
        {
            if (_activeBars.TryGetValue(entity, out ActiveBar existingBar) && existingBar.Handle != null)
                return existingBar;

            if (!EnsureRootView() || !ResolveFloatingRoot())
                return null;

            UnitHealthBarUI.BarHandle handle = _rootView.AcquireBar();
            if (handle == null)
                return null;

            ActiveBar bar = new ActiveBar
            {
                Entity = entity,
                Handle = handle,
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
                if (bar?.Handle == null)
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
                    _rootView?.SetBarVisible(bar.Handle, false);
                    continue;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPosition, _currentCamera, out Vector2 localPoint))
                {
                    _rootView?.UpdateBar(bar.Handle, vitality.CurrentHealth, vitality.RealMaxHealth, localPoint, true);
                    UpdateBuffDisplay(entityManager, bar);
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
            if (bar?.Handle != null)
                _rootView?.ReleaseBar(bar.Handle);
        }

        private void UpdateBuffDisplay(EntityManager entityManager, ActiveBar bar)
        {
            if (bar?.Handle == null || _rootView == null)
                return;

            BuildVisibleBuffs(entityManager, bar.Entity, _buffDisplayBuffer, out int signature);
            if (bar.LastBuffSignature == signature)
                return;

            bar.LastBuffSignature = signature;
            _rootView.UpdateBuffIcons(bar.Handle, _buffDisplayBuffer);
        }

        private static void BuildVisibleBuffs(
            EntityManager entityManager,
            Entity entity,
            List<UnitHealthBarBuffDisplayData> output,
            out int signature)
        {
            output.Clear();
            signature = 17;

            if (!entityManager.Exists(entity) || !entityManager.HasBuffer<UnitBuffElement>(entity))
                return;

            DynamicBuffer<UnitBuffElement> buffer = entityManager.GetBuffer<UnitBuffElement>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                UnitBuffElement element = buffer[i];
                if (element.BuffId < 0 || element.StackCount <= 0 || element.HasOriginEntity == 0)
                    continue;

                if (element.OriginEntity == Entity.Null ||
                    !entityManager.Exists(element.OriginEntity) ||
                    !entityManager.HasComponent<PlayerTag>(element.OriginEntity))
                {
                    continue;
                }

                if (element.SourceSkillId < 0)
                    continue;

                SkillData sourceSkill = DataComponent.Instance?.Get<SkillData>(element.SourceSkillId);
                string iconPath = sourceSkill?.IconPath;
                if (string.IsNullOrWhiteSpace(iconPath))
                    continue;

                output.Add(new UnitHealthBarBuffDisplayData
                {
                    BuffId = element.BuffId,
                    StackCount = element.StackCount,
                    SourceSkillId = element.SourceSkillId,
                    IconPath = iconPath,
                });

                signature = (signature * 31) + element.BuffId;
                signature = (signature * 31) + element.StackCount;
                signature = (signature * 31) + element.SourceSkillId;
            }
        }

        private void ReleaseAllBars()
        {
            foreach (KeyValuePair<Entity, ActiveBar> pair in _activeBars)
            {
                if (pair.Value?.Handle != null)
                    _rootView?.ReleaseBar(pair.Value.Handle);
            }

            _activeBars.Clear();
            _cleanupEntities.Clear();
        }

        private void ReleaseRootView()
        {
            if (_rootView == null)
                return;

            UIComponent.Instance.ReleaseUI(_rootView);
            _rootView = null;
        }

        private sealed class ActiveBar
        {
            public Entity Entity;
            public UnitHealthBarUI.BarHandle Handle;
            public float HideAtTime;
            public int LastBuffSignature;
        }
    }
}
