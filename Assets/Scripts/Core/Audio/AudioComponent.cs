using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CrystalMagic.Core {
    /// <summary>
    /// 音频管理组件
    /// - BGM：单通道，支持淡入淡出切换
    /// - SFX：多通道，复用 AudioSource 池
    /// </summary>
    public class AudioComponent : GameComponent<AudioComponent>
    {
        public override int Priority => 28;

        // -------- BGM --------
        private AudioSource _bgmSource;
        private AudioSource _bgmFadeSource;   // 切歌时旧音轨用这个淡出
        private float _bgmVolume = 1f;

        // -------- SFX --------
        private List<AudioSource> _sfxPool = new();
        private const int SfxPoolSize = 16;
        private float _sfxVolume = 1f;

        // -------- 容器 --------
        private Transform _audioRoot;

        public override void Initialize()
        {
            base.Initialize();

            GameObject root = new GameObject("[AudioRoot]");
            DontDestroyOnLoad(root);
            _audioRoot = root.transform;

            _bgmSource = CreateAudioSource("BGM");
            _bgmSource.loop = true;

            _bgmFadeSource = CreateAudioSource("BGM_Fade");
            _bgmFadeSource.loop = true;

            for (int i = 0; i < SfxPoolSize; i++)
            {
                _sfxPool.Add(CreateAudioSource($"SFX_{i}"));
            }
        }

        // ==================== BGM ====================

        /// <summary>
        /// 立即播放 BGM（无淡入淡出）
        /// </summary>
        public void PlayBGM(string assetPath, bool loop = true)
        {
            AudioClip clip = ResourceComponent.Instance.Load<AudioClip>(assetPath);
            if (clip == null) return;

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.Play();
        }

        /// <summary>
        /// 切换 BGM，带淡入淡出
        /// </summary>
        public void SwitchBGM(string assetPath, float fadeDuration = 0.5f, bool loop = true)
        {
            AudioClip clip = ResourceComponent.Instance.Load<AudioClip>(assetPath);
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

        /// <summary>
        /// 播放音效（从池中取空闲 AudioSource）
        /// </summary>
        public void PlaySFX(string assetPath, float volumeScale = 1f)
        {
            AudioClip clip = ResourceComponent.Instance.Load<AudioClip>(assetPath);
            if (clip == null) return;

            AudioSource source = GetIdleSFXSource();
            if (source == null)
            {
                Debug.LogWarning("[AudioComponent] SFX pool exhausted");
                return;
            }

            source.volume = _sfxVolume * volumeScale;
            source.PlayOneShot(clip);
        }

        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        // ==================== 内部工具 ====================

        private AudioSource GetIdleSFXSource()
        {
            foreach (var src in _sfxPool)
            {
                if (!src.isPlaying) return src;
            }
            return null;
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            GameObject obj = new GameObject(sourceName);
            obj.transform.SetParent(_audioRoot);
            return obj.AddComponent<AudioSource>();
        }

        public override void Cleanup()
        {
            StopBGM();
            foreach (var src in _sfxPool) src.Stop();
            _sfxPool.Clear();

            if (_audioRoot != null)
                Destroy(_audioRoot.gameObject);

            base.Cleanup();
        }
    }
}
