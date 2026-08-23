using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
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
        private World _enemyBuffQueryWorld;
        private EntityQuery _enemyBuffQuery;
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
            if (!_initialized)
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
            ReleaseEnemyBuffQuery();
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
            EnsureBarsForVisibleBuffs(world, entityManager);
            _cleanupEntities.Clear();

            foreach (KeyValuePair<Entity, ActiveBar> pair in _activeBars)
            {
                ActiveBar bar = pair.Value;
                if (bar?.Handle == null)
                {
                    _cleanupEntities.Add(pair.Key);
                    continue;
                }

                Entity entity = pair.Key;
                if (!entityManager.Exists(entity)
                    || !entityManager.HasComponent<LocalToWorld>(entity)
                    || !entityManager.HasComponent<UnitVitalityComponent>(entity)
                    || (entityManager.HasComponent<UnitDeathComponent>(entity) &&
                        entityManager.IsComponentEnabled<UnitDeathComponent>(entity)))
                {
                    _cleanupEntities.Add(entity);
                    continue;
                }

                BuildVisibleBuffs(entityManager, entity, _buffDisplayBuffer, out int signature);
                bool hasVisibleBuffs = _buffDisplayBuffer.Count > 0;
                if (Time.time >= bar.HideAtTime && !hasVisibleBuffs)
                {
                    _cleanupEntities.Add(pair.Key);
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
                    _rootView?.UpdateBar(bar.Handle, vitality.CurrentHealth, UnitModifierResolver.GetMaxHealth(entityManager, entity), localPoint, true);
                    UpdateBuffDisplay(bar, signature);
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

            return UnitFactionUtility.IsHostile(entityManager.GetComponentData<UnitFactionComponent>(entity).Value);
        }

        private void ReleaseBar(Entity entity)
        {
            if (!_activeBars.TryGetValue(entity, out ActiveBar bar))
                return;

            _activeBars.Remove(entity);
            if (bar?.Handle != null)
                _rootView?.ReleaseBar(bar.Handle);
        }

        private void UpdateBuffDisplay(ActiveBar bar, int signature)
        {
            if (bar?.Handle == null || _rootView == null)
                return;

            if (bar.LastBuffSignature == signature)
                return;

            bar.LastBuffSignature = signature;
            _rootView.UpdateBuffIcons(bar.Handle, _buffDisplayBuffer);
        }

        private void EnsureBarsForVisibleBuffs(World world, EntityManager entityManager)
        {
            if (!EnsureEnemyBuffQuery(world))
                return;

            using NativeArray<Entity> entities = _enemyBuffQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!IsEnemyUnit(entity))
                    continue;

                BuildVisibleBuffs(entityManager, entity, _buffDisplayBuffer, out _);
                if (_buffDisplayBuffer.Count <= 0)
                    continue;

                GetOrCreateBar(entity);
            }
        }

        private bool EnsureEnemyBuffQuery(World world)
        {
            if (world == null || !world.IsCreated)
                return false;

            if (_enemyBuffQueryWorld == world)
                return true;

            ReleaseEnemyBuffQuery();
            _enemyBuffQueryWorld = world;
            _enemyBuffQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitBuffRuntimeComponent>(),
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<UnitVitalityComponent>(),
                ComponentType.ReadOnly<LocalToWorld>());
            return true;
        }

        private void ReleaseEnemyBuffQuery()
        {
            if (_enemyBuffQueryWorld == null || !_enemyBuffQueryWorld.IsCreated)
            {
                _enemyBuffQueryWorld = null;
                _enemyBuffQuery = default;
                return;
            }

            _enemyBuffQuery.Dispose();
            _enemyBuffQueryWorld = null;
            _enemyBuffQuery = default;
        }

        private static void BuildVisibleBuffs(
            EntityManager entityManager,
            Entity entity,
            List<UnitHealthBarBuffDisplayData> output,
            out int signature)
        {
            output.Clear();
            signature = 17;

            if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitBuffRuntimeComponent>(entity))
                return;

            UnitBuffRuntimeComponent runtimeComponent = entityManager.GetComponentObject<UnitBuffRuntimeComponent>(entity);
            if (runtimeComponent?.Buffs == null)
                return;

            for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
                if (entry.BuffId < 0 || entry.StackCount <= 0 || !entry.HasOriginEntity)
                    continue;

                if (entry.OriginEntity == Entity.Null ||
                    !entityManager.Exists(entry.OriginEntity) ||
                    !entityManager.HasComponent<UnitFactionComponent>(entry.OriginEntity) ||
                    !UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entry.OriginEntity).Value))
                {
                    continue;
                }

                if (entry.SourceSkillId < 0)
                    continue;

                SkillData sourceSkill = DataComponent.Instance?.Get<SkillData>(entry.SourceSkillId);
                string iconPath = sourceSkill?.IconPath;
                if (string.IsNullOrWhiteSpace(iconPath))
                    continue;

                output.Add(new UnitHealthBarBuffDisplayData
                {
                    BuffId = entry.BuffId,
                    StackCount = entry.StackCount,
                    SourceSkillId = entry.SourceSkillId,
                    IconPath = iconPath,
                });

                signature = (signature * 31) + entry.BuffId;
                signature = (signature * 31) + entry.StackCount;
                signature = (signature * 31) + entry.SourceSkillId;
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
