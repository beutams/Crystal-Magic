using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    [DisallowMultipleComponent]
    public sealed class Flipbook4x4Runtime : MonoBehaviour
    {
        private static readonly int StartTimeId = Shader.PropertyToID("_StartTime");
        private static readonly int FpsId = Shader.PropertyToID("_FPS");
        private static readonly int LoopId = Shader.PropertyToID("_Loop");
        private static readonly int FrameCountId = Shader.PropertyToID("_FrameCount");
        private static readonly int GridXId = Shader.PropertyToID("_GridX");
        private static readonly int GridYId = Shader.PropertyToID("_GridY");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField, Min(1)] private int _gridX = 4;
        [SerializeField, Min(1)] private int _gridY = 4;
        [SerializeField, Min(1)] private int _frameCount = 16;
        [SerializeField, Min(0.01f)] private float _fps = 16f;
        [SerializeField] private bool _loop = true;
        [SerializeField] private bool _destroyWhenFinished;

        private MaterialPropertyBlock _propertyBlock;
        private Renderer[] _renderers;
        private float _startTime;
        private bool _initialized;
        private Texture _overrideTexture;

        public void Initialize(Texture overrideTexture, int frameCount, bool loop, bool destroyWhenFinished)
        {
            _overrideTexture = overrideTexture;
            _frameCount = Mathf.Clamp(frameCount, 1, 16);
            _loop = loop;
            _destroyWhenFinished = destroyWhenFinished;
            _initialized = true;
            RefreshStartTime();
            ApplyProperties();
        }

        private void Awake()
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            CacheRenderers();
            if (_startTime <= 0f)
                RefreshStartTime();
            ApplyProperties();
        }

        private void OnEnable()
        {
            if (!_initialized && _startTime <= 0f)
                RefreshStartTime();
            ApplyProperties();
        }

        private void Update()
        {
            if (!_destroyWhenFinished || _loop || _fps <= 0f)
                return;

            float duration = _frameCount / _fps;
            if (Time.time - _startTime >= duration)
                PoolComponent.Instance.Release(gameObject);
        }

        private void CacheRenderers()
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void RefreshStartTime()
        {
            _startTime = Time.time;
        }

        private void ApplyProperties()
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            CacheRenderers();
            if (_renderers == null || _renderers.Length == 0)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                _propertyBlock.Clear();
                if (_overrideTexture != null)
                    _propertyBlock.SetTexture(BaseMapId, _overrideTexture);
                _propertyBlock.SetFloat(StartTimeId, _startTime);
                _propertyBlock.SetFloat(FpsId, _fps);
                _propertyBlock.SetFloat(LoopId, _loop ? 1f : 0f);
                _propertyBlock.SetFloat(FrameCountId, _frameCount);
                _propertyBlock.SetFloat(GridXId, _gridX);
                _propertyBlock.SetFloat(GridYId, _gridY);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
