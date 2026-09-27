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
        private const float WorldYOffset = -1.4f;
        private const float BuffRefreshIntervalSeconds = 0.1f;
        private const float BuffDiscoveryPauseSeconds = 0.1f;
        private const int BuffDiscoveryBudgetPerFrame = 32;

        private readonly Dictionary<Entity, ActiveBar> _activeBars = new();
        private readonly List<Entity> _cleanupEntities = new();
        private readonly List<UnitHealthBarBuffDisplayData> _buffDisplayBuffer = new();

        private UnitHealthBarUI _rootView;
        private RectTransform _rootRect;
        private Camera _currentCamera;
        private World _enemyBuffQueryWorld;
        private EntityQuery _enemyBuffQuery;
        private NativeArray<Entity> _buffDiscoveryEntities;
        private int _buffDiscoveryIndex;
        private float _nextBuffDiscoveryTime;
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

            if ((_rootRect == null || _currentCamera == null) && !ResolveFloatingRoot())
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

            if (!EnsureRootView()
                || ((_rootRect == null || _currentCamera == null) && !ResolveFloatingRoot()))
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
            DiscoverBarsWithVisibleBuffs(world, entityManager);
            _cleanupEntities.Clear();
            float now = Time.time;

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
                    || !IsEnemyUnit(entityManager, entity)
                    || !entityManager.HasComponent<LocalToWorld>(entity)
                    || !entityManager.HasComponent<UnitVitalityComponent>(entity)
                    || (entityManager.HasComponent<UnitDeathComponent>(entity) &&
                        entityManager.IsComponentEnabled<UnitDeathComponent>(entity)))
                {
                    _cleanupEntities.Add(entity);
                    continue;
                }

                if (now >= bar.NextBuffRefreshTime)
                {
                    BuildVisibleBuffs(
                        entityManager,
                        entity,
                        _buffDisplayBuffer,
                        requirePlayerOrigin: true,
                        out int signature);
                    bar.HasVisibleBuffs = _buffDisplayBuffer.Count > 0;
                    bar.NextBuffRefreshTime = now + BuffRefreshIntervalSeconds;
                    UpdateBuffDisplay(bar, signature);
                }

                if (now >= bar.HideAtTime && !bar.HasVisibleBuffs)
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

            return IsEnemyUnit(world.EntityManager, entity);
        }

        private static bool IsEnemyUnit(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.Exists(entity)
                || !entityManager.HasComponent<UnitVitalityComponent>(entity)
                || !entityManager.HasComponent<UnitFactionComponent>(entity))
            {
                return false;
            }

            if (entityManager.HasComponent<NetworkPlayerComponent>(entity) ||
                entityManager.HasComponent<PlayerInputComponent>(entity))
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

        private void DiscoverBarsWithVisibleBuffs(World world, EntityManager entityManager)
        {
            if (!EnsureEnemyBuffQuery(world))
                return;

            if (!_buffDiscoveryEntities.IsCreated)
            {
                if (Time.time < _nextBuffDiscoveryTime)
                    return;

                _buffDiscoveryEntities = _enemyBuffQuery.ToEntityArray(Allocator.Persistent);
                _buffDiscoveryIndex = 0;
                if (_buffDiscoveryEntities.Length == 0)
                {
                    CompleteBuffDiscoveryCycle();
                    return;
                }
            }

            int endIndex = Math.Min(
                _buffDiscoveryIndex + BuffDiscoveryBudgetPerFrame,
                _buffDiscoveryEntities.Length);
            for (; _buffDiscoveryIndex < endIndex; _buffDiscoveryIndex++)
            {
                Entity entity = _buffDiscoveryEntities[_buffDiscoveryIndex];
                if (!IsEnemyUnit(entityManager, entity))
                    continue;

                BuildVisibleBuffs(
                    entityManager,
                    entity,
                    _buffDisplayBuffer,
                    requirePlayerOrigin: true,
                    out _);
                if (_buffDisplayBuffer.Count <= 0)
                    continue;

                GetOrCreateBar(entity);
            }

            if (_buffDiscoveryIndex >= _buffDiscoveryEntities.Length)
                CompleteBuffDiscoveryCycle();
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
                ComponentType.ReadOnly<UnitBuffComponent>(),
                ComponentType.ReadOnly<UnitBuffElement>(),
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<UnitVitalityComponent>(),
                ComponentType.ReadOnly<LocalToWorld>());
            return true;
        }

        private void ReleaseEnemyBuffQuery()
        {
            ReleaseBuffDiscoveryEntities();
            _nextBuffDiscoveryTime = 0f;

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

        private void CompleteBuffDiscoveryCycle()
        {
            ReleaseBuffDiscoveryEntities();
            _nextBuffDiscoveryTime = Time.time + BuffDiscoveryPauseSeconds;
        }

        private void ReleaseBuffDiscoveryEntities()
        {
            if (_buffDiscoveryEntities.IsCreated)
                _buffDiscoveryEntities.Dispose();

            _buffDiscoveryEntities = default;
            _buffDiscoveryIndex = 0;
        }

        internal static void BuildVisibleBuffs(
            EntityManager entityManager,
            Entity entity,
            List<UnitHealthBarBuffDisplayData> output,
            bool requirePlayerOrigin,
            out int signature)
        {
            output.Clear();
            signature = 17;

            if (entityManager.Exists(entity) && entityManager.HasBuffer<ClientBuffPresentationElement>(entity))
            {
                DynamicBuffer<ClientBuffPresentationElement> presentation =
                    entityManager.GetBuffer<ClientBuffPresentationElement>(entity, true);
                for (int i = 0; i < presentation.Length; i++)
                {
                    ClientBuffPresentationElement entry = presentation[i];
                    AddVisibleBuff(
                        entityManager,
                        entry.BuffId,
                        entry.StackCount,
                        entry.OriginEntity,
                        output,
                        requirePlayerOrigin,
                        ref signature);
                }

                return;
            }

            if (!entityManager.Exists(entity) || !entityManager.HasBuffer<UnitBuffElement>(entity))
                return;

            DynamicBuffer<UnitBuffElement> buffs = entityManager.GetBuffer<UnitBuffElement>(entity, true);
            for (int i = 0; i < buffs.Length; i++)
            {
                UnitBuffElement entry = buffs[i];
                AddVisibleBuff(
                    entityManager,
                    entry.BuffId,
                    entry.StackCount,
                    entry.OriginEntity,
                    output,
                    requirePlayerOrigin,
                    ref signature);
            }
        }

        private static void AddVisibleBuff(
            EntityManager entityManager,
            int buffId,
            int stackCount,
            Entity originEntity,
            List<UnitHealthBarBuffDisplayData> output,
            bool requirePlayerOrigin,
            ref int signature)
        {
            if (buffId < 0 || stackCount <= 0)
            {
                return;
            }

            if (requirePlayerOrigin &&
                (originEntity == Entity.Null ||
                 !entityManager.Exists(originEntity) ||
                 !entityManager.HasComponent<UnitFactionComponent>(originEntity) ||
                 !UnitFactionUtility.IsPlayer(entityManager
                     .GetComponentData<UnitFactionComponent>(originEntity).Value)))
            {
                return;
            }

            BuffData buffData = DataComponent.Instance?.Get<BuffData>(buffId);
            string iconPath = buffData?.IconPath;
            if (string.IsNullOrWhiteSpace(iconPath))
                return;

            output.Add(new UnitHealthBarBuffDisplayData
            {
                BuffId = buffId,
                StackCount = stackCount,
                IconPath = iconPath,
            });

            signature = (signature * 31) + buffId;
            signature = (signature * 31) + stackCount;
            signature = (signature * 31) + iconPath.GetHashCode();
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
            public float NextBuffRefreshTime;
            public bool HasVisibleBuffs;
        }
    }
}
