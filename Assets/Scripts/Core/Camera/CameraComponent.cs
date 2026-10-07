using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 相机管理组件
    /// </summary>
    public class CameraComponent : GameComponent<CameraComponent>
    {
        public override int Priority => 13;

        private SceneCamera _current;
        private readonly List<ShakeInstance> _shakes = new();
        private Camera _shakeAppliedCamera;
        private Vector3 _lastShakeOffset;
        private World _followQueryWorld;
        private Entity _followTargetEntity = Entity.Null;
        private float _screenShakeScale = 1f;
        private readonly CameraWorldBoundsConstraint _worldBounds = new();
        private readonly List<FollowTargetLease> _followOverrides = new();
        private Vector3 _returnPosition;
        private float _returnSmooth;
        private bool _returningFromOverride;
        private float _returnElapsed;
        private Vector3 _returnStartPosition;

        /// <summary>当前活跃的场景相机</summary>
        public Camera Current => _current != null ? _current.Camera : Camera.main;

        public void Register(SceneCamera cam)
        {
            if (_current != cam)
                ClearFollowTargets();
            _current = cam;
            _worldBounds.Apply(cam.Camera);
            Debug.Log($"[CameraComponent] Registered: {cam.gameObject.name}");
        }

        public void Unregister(SceneCamera cam)
        {
            if (_current == cam)
            {
                ClearFollowTargets();
                _current = null;
                Debug.Log($"[CameraComponent] Unregistered: {cam.gameObject.name}");
            }
        }

        public void SetWorldBounds(int ownerId, Rect worldBounds)
        {
            SetWorldBounds(ownerId, new Bounds(worldBounds.center, new Vector3(worldBounds.width, worldBounds.height, 0)));
        }

        public void SetWorldBounds(int ownerId, Bounds worldBounds)
        {
            RestoreShakeOffset();
            _worldBounds.Set(ownerId, worldBounds);
            _worldBounds.Apply(Current);
        }

        public void ClearWorldBounds(int ownerId)
        {
            _worldBounds.Clear(ownerId);
        }

        private void LateUpdate()
        {
            RestoreShakeOffset();

            Camera camera = Current;
            if (camera == null)
                return;

            ApplyFollow(camera, Time.deltaTime);
            _worldBounds.Apply(camera);

            if (_shakes.Count == 0)
                return;

            Vector3 basePosition = camera.transform.position;
            Vector3 offset = CalculateShakeOffset(camera, basePosition, Time.deltaTime);
            if (offset == Vector3.zero)
                return;

            Vector3 constrainedPosition = _worldBounds.ClampPosition(camera, basePosition + offset);
            _lastShakeOffset = constrainedPosition - basePosition;
            if (_lastShakeOffset == Vector3.zero)
                return;

            _shakeAppliedCamera = camera;
            camera.transform.position = constrainedPosition;
        }
        #region Shake
        public void SetScreenShakeScale(float scale)
        {
            _screenShakeScale = Mathf.Max(0f, scale);
        }

        public void AddShake(Vector3 worldPosition, float duration, float amplitude, float frequency, bool useDistanceAttenuation, float radius)
        {
            amplitude *= _screenShakeScale;
            if (duration <= 0f || amplitude <= 0f)
                return;

            _shakes.Add(new ShakeInstance
            {
                WorldPosition = worldPosition,
                Duration = duration,
                Amplitude = amplitude,
                Frequency = Mathf.Max(0.01f, frequency),
                UseDistanceAttenuation = useDistanceAttenuation,
                Radius = Mathf.Max(0f, radius),
                Seed = UnityEngine.Random.value * 1000f,
            });
        }

        private Vector3 CalculateShakeOffset(Camera camera, Vector3 cameraPosition, float deltaTime)
        {
            Vector3 offset = Vector3.zero;
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;

            for (int i = _shakes.Count - 1; i >= 0; i--)
            {
                ShakeInstance shake = _shakes[i];
                shake.Elapsed += deltaTime;
                if (shake.Elapsed >= shake.Duration)
                {
                    _shakes.RemoveAt(i);
                    continue;
                }

                float fade = 1f - shake.Elapsed / shake.Duration;
                float attenuation = GetDistanceAttenuation(camera, cameraPosition, shake);
                float strength = shake.Amplitude * fade * attenuation;
                if (strength <= 0f)
                {
                    _shakes[i] = shake;
                    continue;
                }

                float sample = shake.Elapsed * shake.Frequency;
                float x = Mathf.PerlinNoise(shake.Seed, sample) * 2f - 1f;
                float y = Mathf.PerlinNoise(shake.Seed + 23.17f, sample) * 2f - 1f;
                offset += (right * x + up * y) * strength;
                _shakes[i] = shake;
            }

            return offset;
        }
        private static float GetDistanceAttenuation(Camera camera, Vector3 cameraPosition, ShakeInstance shake)
        {
            if (!shake.UseDistanceAttenuation || shake.Radius <= 0f)
                return 1f;

            Vector3 toSource = shake.WorldPosition - cameraPosition;
            Vector3 planar = toSource - camera.transform.forward * Vector3.Dot(toSource, camera.transform.forward);
            return Mathf.Clamp01(1f - planar.magnitude / shake.Radius);
        }
        private void RestoreShakeOffset()
        {
            if (_lastShakeOffset == Vector3.zero)
                return;

            if (_shakeAppliedCamera != null)
                _shakeAppliedCamera.transform.position -= _lastShakeOffset;

            _lastShakeOffset = Vector3.zero;
            _shakeAppliedCamera = null;
        }
        #endregion

        #region Follow
        /// <summary>
        /// Temporarily follow an entity in its owning World. The newest live lease wins;
        /// disposing an older lease never releases a newer speaker's lock.
        /// </summary>
        public IDisposable AcquireFollowTarget(World world, Entity target, float smooth = 8f)
        {
            if (!TryGetEntityTargetPosition(world, target, out _))
                return null;
            RestoreShakeOffset();
            if (_followOverrides.Count == 0 && !_returningFromOverride)
                _returnPosition = Current != null ? Current.transform.position : Vector3.zero;
            smooth = float.IsNaN(smooth) || float.IsInfinity(smooth) ? 8f : Mathf.Clamp(smooth, 0f, 20f);
            var lease = new FollowTargetLease(this, world, target, smooth);
            _followOverrides.Add(lease);
            _returnSmooth = smooth;
            _returningFromOverride = false;
            return lease;
        }

        private void ReleaseFollowTarget(FollowTargetLease lease)
        {
            if (_followOverrides.Remove(lease) && _followOverrides.Count == 0)
            {
                _returningFromOverride = true;
                _returnElapsed = 0f;
            }
        }

        private void ClearFollowTargets()
        {
            foreach (FollowTargetLease lease in _followOverrides)
                lease.Owner = null;
            _followOverrides.Clear();
            _returningFromOverride = false;
        }

        private bool TryGetOverridePosition(out Vector3 position, out float smooth)
        {
            // Prune destroyed entities/disposed Worlds, including suspended older locks.
            for (int i = _followOverrides.Count - 1; i >= 0; i--)
                if (!TryGetEntityTargetPosition(_followOverrides[i].World, _followOverrides[i].Target, out _))
                    _followOverrides[i].Dispose();
            if (_followOverrides.Count > 0)
            {
                FollowTargetLease lease = _followOverrides[_followOverrides.Count - 1];
                smooth = lease.Smooth;
                return TryGetEntityTargetPosition(lease.World, lease.Target, out position);
            }
            position = default;
            smooth = 0f;
            return false;
        }

        private void ApplyFollow(Camera camera, float deltaTime)
        {
            Vector3 currentPosition = camera.transform.position;
            bool hasOverride = TryGetOverridePosition(out Vector3 targetPosition, out float smooth);
            Vector3 desiredPosition;
            if (hasOverride)
            {
                desiredPosition = _current != null ? _current.GetDesiredPosition(targetPosition, currentPosition)
                    : new Vector3(targetPosition.x, targetPosition.y, currentPosition.z);
            }
            else if (_current != null && _current.FollowPlayerTag && TryGetPlayerTargetPosition(out targetPosition))
            {
                desiredPosition = _current.GetDesiredPosition(targetPosition, currentPosition);
                smooth = _returningFromOverride ? _returnSmooth : _current.FollowSmooth;
            }
            else if (_returningFromOverride)
            {
                // Static scene cameras return to their previous position, not to a player.
                desiredPosition = _returnPosition;
                smooth = _returnSmooth;
            }
            else
                return;

            desiredPosition = _worldBounds.ClampPosition(camera, desiredPosition);
            if (!hasOverride && _returningFromOverride)
            {
                if (_returnElapsed == 0f)
                    _returnStartPosition = currentPosition;
                _returnElapsed += Mathf.Max(0f, deltaTime);
                // A finite blend restores the original follow settings even while the
                // player keeps moving. Distance-only convergence would keep extra lag forever.
                float progress = smooth <= 0f ? 1f : Mathf.Clamp01(_returnElapsed * smooth / 4f);
                camera.transform.position = Vector3.Lerp(_returnStartPosition, desiredPosition, Mathf.SmoothStep(0f, 1f, progress));
                _returningFromOverride = progress < 1f;
                return;
            }
            float t = smooth <= 0f ? 1f : 1f - Mathf.Exp(-smooth * Mathf.Max(0f, deltaTime));
            camera.transform.position = Vector3.Lerp(currentPosition, desiredPosition, t);
        }

        private bool TryGetPlayerTargetPosition(out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            if (_followQueryWorld != world)
            {
                _followQueryWorld = world;
                _followTargetEntity = Entity.Null;
            }

            EntityManager entityManager = world.EntityManager;
            if (_followTargetEntity == Entity.Null ||
                !entityManager.Exists(_followTargetEntity) ||
                !entityManager.HasComponent<LocalToWorld>(_followTargetEntity) ||
                GameWorldManager.Role == GameWorldRole.Client &&
                !entityManager.HasComponent<NetworkPlayerComponent>(_followTargetEntity))
            {
                if (!GameRuntimeStateUtility.TryGetPlayerEntity(entityManager, out _followTargetEntity) ||
                    !entityManager.HasComponent<LocalToWorld>(_followTargetEntity))
                {
                    _followTargetEntity = Entity.Null;
                    return false;
                }
            }

            return TryGetEntityTargetPosition(world, _followTargetEntity, out targetPosition);
        }

        private static bool TryGetEntityTargetPosition(World world, Entity target, out Vector3 targetPosition)
        {
            targetPosition = default;
            if (world == null || !world.IsCreated || target == Entity.Null)
                return false;
            EntityManager entityManager = world.EntityManager;
            if (!entityManager.Exists(target) || !entityManager.HasComponent<LocalToWorld>(target) ||
                entityManager.HasComponent<DestroyEntityFlag>(target) && entityManager.IsComponentEnabled<DestroyEntityFlag>(target))
                return false;
            LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(target);
            float3 position = localToWorld.Position;
            if (entityManager.HasComponent<ClientPlayerMovePresentationComponent>(target))
            {
                ClientPlayerMovePresentationComponent presentation =
                    entityManager.GetComponentData<ClientPlayerMovePresentationComponent>(target);
                if (presentation.Initialized != 0)
                    position = presentation.CurrentPosition;
            }
            targetPosition = new Vector3(position.x, position.y, position.z);
            return true;
        }

        private sealed class FollowTargetLease : IDisposable
        {
            public CameraComponent Owner;
            public readonly World World;
            public readonly Entity Target;
            public readonly float Smooth;

            public FollowTargetLease(CameraComponent owner, World world, Entity target, float smooth)
            {
                Owner = owner;
                World = world;
                Target = target;
                Smooth = smooth;
            }

            public void Dispose()
            {
                CameraComponent owner = Owner;
                Owner = null;
                if (owner != null)
                    owner.ReleaseFollowTarget(this);
            }
        }

        #endregion

        public override void Cleanup()
        {
            RestoreShakeOffset();
            _shakes.Clear();
            ClearFollowTargets();
            _current = null;
            _followQueryWorld = null;
            _followTargetEntity = Entity.Null;
            _worldBounds.Reset();
            base.Cleanup();
        }

        private struct ShakeInstance
        {
            public Vector3 WorldPosition;
            public float Duration;
            public float Elapsed;
            public float Amplitude;
            public float Frequency;
            public bool UseDistanceAttenuation;
            public float Radius;
            public float Seed;
        }
    }
}
