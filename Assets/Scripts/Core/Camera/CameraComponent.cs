using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 相机管理组件
    /// 追踪当前场景的主相机，提供全局访问入口
    /// 各场景相机挂载 SceneCamera 组件后自动注册/注销
    /// </summary>
    public class CameraComponent : GameComponent<CameraComponent>
    {
        public override int Priority => 13;

        private SceneCamera _current;
        private readonly List<ShakeInstance> _shakes = new();
        private Camera _shakeAppliedCamera;
        private Vector3 _lastShakeOffset;

        /// <summary>当前活跃的场景相机</summary>
        public Camera Current => _current != null ? _current.Camera : Camera.main;

        public void Register(SceneCamera cam)
        {
            _current = cam;
            Debug.Log($"[CameraComponent] Registered: {cam.gameObject.name}");
        }

        public void Unregister(SceneCamera cam)
        {
            if (_current == cam)
            {
                _current = null;
                Debug.Log($"[CameraComponent] Unregistered: {cam.gameObject.name}");
            }
        }
        public void AddShake(Vector3 worldPosition, float duration, float amplitude, float frequency, bool useDistanceAttenuation, float radius)
        {
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

        private void LateUpdate()
        {
            RestoreShakeOffset();

            Camera camera = Current;
            if (camera == null || _shakes.Count == 0)
                return;

            Vector3 basePosition = camera.transform.position;
            Vector3 offset = CalculateShakeOffset(camera, basePosition, Time.deltaTime);
            if (offset == Vector3.zero)
                return;

            _lastShakeOffset = offset;
            _shakeAppliedCamera = camera;
            camera.transform.position = basePosition + offset;
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

        public override void Cleanup()
        {
            RestoreShakeOffset();
            _shakes.Clear();
            _current = null;
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
