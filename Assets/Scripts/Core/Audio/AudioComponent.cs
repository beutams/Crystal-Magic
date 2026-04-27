using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;

namespace CrystalMagic.Core {
    public enum AudioChannel
    {
        BGM,
        Unit,
        UI,
    }

    /// <summary>
    /// 音频管理组件
    /// </summary>
    public class AudioComponent : GameComponent<AudioComponent>
    {
        public override int Priority => 28;

        // -------- BGM --------
        [SerializeField] private AudioMixerGroup _bgmMixer;
        private AudioSource _bgmSource;
        private AudioSource _bgmFadeSource;   // 切歌时旧音轨用这个淡出
        private float _bgmVolume = 1f;

        // -------- SFX --------
        [SerializeField] private AudioMixerGroup _unitMixer;
        [SerializeField] private AudioMixerGroup _uiMixer;
        [SerializeField] private int _unitPoolSize = 32;
        [SerializeField] private int _uiPoolSize = 12;
        [SerializeField] private int _maxUnitPlaying = 24;
        [SerializeField] private int _maxUiPlaying = 8;
        private readonly List<PooledAudioSource> _unitPool = new();
        private readonly List<PooledAudioSource> _uiPool = new();
        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private float _sfxVolume = 1f;

        // -------- 容器 --------
        private Transform _audioRoot;
        private Transform _unitRoot;
        private Transform _uiRoot;

        public override void Initialize()
        {
            base.Initialize();

            GameObject root = new GameObject("[AudioRoot]");
            DontDestroyOnLoad(root);
            _audioRoot = root.transform;
            _unitRoot = CreateChildRoot("UnitAudioPool");
            _uiRoot = CreateChildRoot("UIAudioPool");

            _bgmSource = CreateAudioSource("BGM");
            _bgmSource.loop = true;
            _bgmSource.outputAudioMixerGroup = _bgmMixer;

            _bgmFadeSource = CreateAudioSource("BGM_Fade");
            _bgmFadeSource.loop = true;
            _bgmFadeSource.outputAudioMixerGroup = _bgmMixer;

            CreatePool(_unitPool, _unitRoot, "UnitAudio", _unitPoolSize, _unitMixer);
            CreatePool(_uiPool, _uiRoot, "UIAudio", _uiPoolSize, _uiMixer);
        }

        // ==================== BGM ====================

        /// <summary>
        /// 立即播放 BGM（无淡入淡出）
        /// </summary>
        public void PlayBGM(string assetPath, bool loop = true)
        {
            AudioClip clip = LoadClip(assetPath);
            if (clip == null) return;

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.Play();
        }

        public void PlayBGM(string assetPath, float volumeScale, bool loop = true)
        {
            float oldVolume = _bgmVolume;
            _bgmVolume = Mathf.Clamp01(volumeScale);
            PlayBGM(assetPath, loop);
            _bgmVolume = oldVolume;
        }

        /// <summary>
        /// 切换 BGM，带淡入淡出
        /// </summary>
        public void SwitchBGM(string assetPath, float fadeDuration = 0.5f, bool loop = true)
        {
            AudioClip clip = LoadClip(assetPath);
            if (clip == null) return;

            StartCoroutine(DoSwitchBGM(clip, fadeDuration, loop));
        }

        private IEnumerator DoSwitchBGM(AudioClip newClip, float fadeDuration, bool loop)
        {
            // 把当前音轨移到 FadeSource 淡出
            _bgmFadeSource.clip = _bgmSource.clip;
            _bgmFadeSource.timeSamples = _bgmSource.timeSamples;
            _bgmFadeSource.volume = _bgmSource.volume;
            _bgmFadeSource.Play();

            // 新音轨从静音开始淡入
            _bgmSource.clip = newClip;
            _bgmSource.loop = loop;
            _bgmSource.volume = 0f;
            _bgmSource.Play();

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                _bgmSource.volume = Mathf.Lerp(0f, _bgmVolume, t);
                _bgmFadeSource.volume = Mathf.Lerp(_bgmVolume, 0f, t);
                yield return null;
            }

            _bgmSource.volume = _bgmVolume;
            _bgmFadeSource.Stop();
            _bgmFadeSource.clip = null;
        }

        public void StopBGM(float fadeDuration = 0f)
        {
            if (fadeDuration <= 0f)
            {
                _bgmSource.Stop();
                return;
            }
            StartCoroutine(DoFadeOutBGM(fadeDuration));
        }

        private IEnumerator DoFadeOutBGM(float fadeDuration)
        {
            float startVolume = _bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
                yield return null;
            }
            _bgmSource.Stop();
            _bgmSource.volume = _bgmVolume;
        }

        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            if (_bgmSource.isPlaying)
                _bgmSource.volume = _bgmVolume;
        }

        // ==================== SFX ====================
        public void SetSFXVolume(float volume)
        {
            SetSFXVolume(volume);
        }

        public void PlayUnit(string assetPath, Vector3 position, float volumeScale = 1f, float pitch = 1f, float spatialBlend = 1f, float delaySeconds = 0f)
        {
            PlayFromPool(_unitPool, AudioChannel.Unit, assetPath, position, volumeScale, pitch, spatialBlend, delaySeconds, false, Entity.Null, default, Vector3.zero);
        }

        public void PlayUnitFollowEntity(string assetPath, Entity entity, EntityManager entityManager, Vector3 offset, float volumeScale = 1f, float pitch = 1f, float spatialBlend = 1f, float delaySeconds = 0f)
        {
            Vector3 position = TryGetEntityPosition(entity, entityManager, out Vector3 entityPosition)
                ? entityPosition + offset
                : offset;

            PlayFromPool(_unitPool, AudioChannel.Unit, assetPath, position, volumeScale, pitch, spatialBlend, delaySeconds, true, entity, entityManager, offset);
        }

        public void PlayUI(string assetPath, float volumeScale = 1f, float pitch = 1f, float delaySeconds = 0f)
        {
            PlayFromPool(_uiPool, AudioChannel.UI, assetPath, Vector3.zero, volumeScale, pitch, 0f, delaySeconds, false, Entity.Null, default, Vector3.zero);
        }

        // ==================== 内部工具 ====================

        private void Update()
        {
            UpdatePool(_unitPool);
            UpdatePool(_uiPool);
        }
        /// <summary>
        /// 从指定音频池中取出一个空闲音源并开始播放，可选支持延迟播放与跟随实体。
        /// </summary>
        /// <param name="pool">这次要从哪个对象池里取音源，_unitPool 或 _uiPool</param>
        /// <param name="channel">音频通道类型，决定用哪个 mixer、最大同时播放数、当前通道音量</param>
        /// <param name="assetPath">音频资源路径，用来从 ResourceComponent 加载 AudioClip</param>
        /// <param name="position">音源初始播放位置</param>
        /// <param name="volumeScale"></param>
        /// <param name="pitch">播放音调，1 是原速，越大越快，越小越慢</param>
        /// <param name="spatialBlend">3D 空间混合值，0 是 2D，1 是完全 3D</param>
        /// <param name="delaySeconds">延迟多少秒后再播放</param>
        /// <param name="followEntity">是否跟随某个实体移动</param>
        /// <param name="entity">要跟随的 ECS 实体</param>
        /// <param name="entityManager">用来查询这个实体当前位置的 EntityManager</param>
        /// <param name="followOffset">跟随时的位置偏移量</param>
        private void PlayFromPool(List<PooledAudioSource> pool, AudioChannel channel, string assetPath, Vector3 position, float volumeScale, float pitch, float spatialBlend, float delaySeconds, bool followEntity, Entity entity, EntityManager entityManager, Vector3 followOffset)
        {
            AudioClip clip = LoadClip(assetPath);
            if (clip == null)
                return;

            UpdatePool(pool);
            if (GetPlayingCount(pool) >= GetMaxPlaying(channel))
                return;

            PooledAudioSource pooled = GetIdleSource(pool);
            if (pooled == null)
            {
                Debug.LogWarning($"[AudioComponent] {channel} audio pool exhausted");
                return;
            }

            float safePitch = Mathf.Max(0.01f, pitch);
            AudioSource source = pooled.Source;
            source.Stop();
            source.clip = clip;
            source.loop = false;
            source.volume = GetChannelVolume(channel) * Mathf.Clamp01(volumeScale);
            source.pitch = safePitch;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.transform.position = position;
            source.outputAudioMixerGroup = GetMixer(channel);

            pooled.InUse = true;
            pooled.Channel = channel;
            pooled.VolumeScale = Mathf.Clamp01(volumeScale);
            pooled.FollowEntity = followEntity;
            pooled.Entity = entity;
            pooled.EntityManager = entityManager;
            pooled.FollowOffset = followOffset;
            pooled.ReleaseTime = Time.time + Mathf.Max(0f, delaySeconds) + clip.length / safePitch + 0.1f;

            if (delaySeconds > 0f)
                source.PlayDelayed(delaySeconds);
            else
                source.Play();
        }

        private void UpdatePool(List<PooledAudioSource> pool)
        {
            float now = Time.time;
            for (int i = 0; i < pool.Count; i++)
            {
                PooledAudioSource pooled = pool[i];
                if (!pooled.InUse)
                    continue;

                if (pooled.FollowEntity)
                {
                    if (TryGetEntityPosition(pooled.Entity, pooled.EntityManager, out Vector3 position))
                        pooled.Source.transform.position = position + pooled.FollowOffset;
                    else
                        pooled.FollowEntity = false;
                }

                if (now >= pooled.ReleaseTime)
                    ReleaseSource(pooled);
            }
        }

        private static bool TryGetEntityPosition(Entity entity, EntityManager entityManager, out Vector3 position)
        {
            if (entity != Entity.Null &&
                entityManager.Exists(entity) &&
                entityManager.HasComponent<LocalTransform>(entity))
            {
                Unity.Mathematics.float3 entityPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;
                position = new Vector3(entityPosition.x, entityPosition.y, entityPosition.z);
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private int GetPlayingCount(List<PooledAudioSource> pool)
        {
            int count = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].InUse)
                    count++;
            }
            return count;
        }

        private static PooledAudioSource GetIdleSource(List<PooledAudioSource> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].InUse)
                    return pool[i];
            }
            return null;
        }

        private void ReleaseSource(PooledAudioSource pooled)
        {
            pooled.Source.Stop();
            pooled.Source.clip = null;
            pooled.InUse = false;
            pooled.Channel = AudioChannel.UI;
            pooled.VolumeScale = 1f;
            pooled.FollowEntity = false;
            pooled.Entity = Entity.Null;
            pooled.EntityManager = default;
            pooled.FollowOffset = Vector3.zero;
            pooled.ReleaseTime = 0f;
        }

        private void RefreshPoolVolumes(List<PooledAudioSource> pool, AudioChannel channel)
        {
            float channelVolume = GetChannelVolume(channel);
            for (int i = 0; i < pool.Count; i++)
            {
                PooledAudioSource pooled = pool[i];
                if (!pooled.InUse || pooled.Channel != channel)
                    continue;

                pooled.Source.volume = channelVolume * pooled.VolumeScale;
            }
        }

        private int GetMaxPlaying(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Unit => _maxUnitPlaying,
                AudioChannel.UI => _maxUiPlaying,
                _ => 1,
            };
        }

        private float GetChannelVolume(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Unit => _sfxVolume,
                AudioChannel.UI => _sfxVolume,
                _ => _bgmVolume,
            };
        }

        private AudioMixerGroup GetMixer(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Unit => _unitMixer,
                AudioChannel.UI => _uiMixer,
                _ => _bgmMixer,
            };
        }

        private AudioClip LoadClip(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            if (_clipCache.TryGetValue(assetPath, out AudioClip cachedClip))
                return cachedClip;

            AudioClip clip = ResourceComponent.Instance.Load<AudioClip>(assetPath);
            if (clip != null)
                _clipCache[assetPath] = clip;

            return clip;
        }

        private void CreatePool(List<PooledAudioSource> pool, Transform parent, string sourceName, int poolSize, AudioMixerGroup mixer)
        {
            pool.Clear();
            int safePoolSize = Mathf.Max(0, poolSize);
            for (int i = 0; i < safePoolSize; i++)
            {
                AudioSource source = CreateAudioSource($"{sourceName}_{i}", parent);
                source.loop = false;
                source.playOnAwake = false;
                source.outputAudioMixerGroup = mixer;
                pool.Add(new PooledAudioSource { Source = source });
            }
        }

        private Transform CreateChildRoot(string rootName)
        {
            GameObject obj = new GameObject(rootName);
            obj.transform.SetParent(_audioRoot);
            return obj.transform;
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            return CreateAudioSource(sourceName, _audioRoot);
        }

        private AudioSource CreateAudioSource(string sourceName, Transform parent)
        {
            GameObject obj = new GameObject(sourceName);
            obj.transform.SetParent(parent);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        public override void Cleanup()
        {
            StopBGM();
            StopPool(_unitPool);
            StopPool(_uiPool);
            _clipCache.Clear();

            if (_audioRoot != null)
                Destroy(_audioRoot.gameObject);

            base.Cleanup();
        }

        private void StopPool(List<PooledAudioSource> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].Source.Stop();
                pool[i].Source.clip = null;
            }
            pool.Clear();
        }

        private sealed class PooledAudioSource
        {
            public AudioSource Source;
            public bool InUse;
            public AudioChannel Channel;
            public float VolumeScale = 1f;
            public bool FollowEntity;
            public Entity Entity;
            public EntityManager EntityManager;
            public Vector3 FollowOffset;
            public float ReleaseTime;
        }
    }
}
