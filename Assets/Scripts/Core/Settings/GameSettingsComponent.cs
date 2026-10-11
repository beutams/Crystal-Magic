using System;
using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class GameSettingsComponent : GameComponent<GameSettingsComponent>
    {
        public const string SettingsChangedEventName = "GameSettings.Changed";
        public const string SettingsSavedEventName = "GameSettings.Saved";

        private const string SettingsFolderName = "GameSettings";
        private const string SettingsFileName = "settings.json";

        private GameSettingsData _settings;
        private int _appliedWidth;
        private int _appliedHeight;
        private bool _appliedFullscreen;

        public override int Priority => 29;

        public override void Initialize()
        {
            base.Initialize();

            EnsureSettingsFolderExists();
            LoadSettingsOrCreateDefault();
            PublishSettingsChanged();
        }

        public GameSettingsData GetSettingsCopy()
        {
            EnsureSettingsValid();
            return _settings.Clone();
        }

        public void SetSettings(GameSettingsData settings, bool saveToDisk = false)
        {
            _settings = Sanitize(settings);
            PublishSettingsChanged();

            if (saveToDisk)
                SaveSettings();
        }

        public bool ReloadSettings()
        {
            if (!TryLoadSettings(out GameSettingsData loadedSettings))
                return false;

            _settings = loadedSettings;
            PublishSettingsChanged();
            return true;
        }

        public bool SaveSettings()
        {
            EnsureSettingsValid();

            try
            {
                EnsureSettingsFolderExists();
                string json = JsonUtility.ToJson(_settings, true);
                System.IO.File.WriteAllText(GetSettingsPath(), json);
                EventComponent.Instance.Publish(new CommonGameEvent(SettingsSavedEventName, _settings.Clone()));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameSettingsComponent] Failed to save settings: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public void ResetToDefault(bool saveToDisk = false)
        {
            GameSettingsData defaults = GameSettingsData.CreateDefault();
            SetDefaultDisplay(defaults);
            SetSettings(defaults, saveToDisk);
        }

        public void ToggleFullscreen()
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.Fullscreen = !settings.Fullscreen;
            SetSettings(settings);
        }

        public void ShiftResolution(int offset)
        {
            GameSettingsData settings = GetSettingsCopy();
            Vector2Int current = new(settings.ResolutionWidth, settings.ResolutionHeight);
            Vector2Int next = GameDisplaySettings.ShiftResolution(current, offset,
                GameDisplaySettings.GetResolutions(GetDesktopSize(), current));
            settings.ResolutionWidth = next.x;
            settings.ResolutionHeight = next.y;
            SetSettings(settings);
        }

        public static void SetDefaultDisplay(GameSettingsData settings)
        {
            Vector2Int desktop = GetDesktopSize();
            settings.Fullscreen = true;
            settings.ResolutionWidth = desktop.x;
            settings.ResolutionHeight = desktop.y;
        }

        private static Vector2Int GetDesktopSize()
        {
            Resolution desktop = Screen.currentResolution;
            return new Vector2Int(desktop.width >= 640 ? desktop.width : Mathf.Max(640, Screen.width),
                desktop.height >= 360 ? desktop.height : Mathf.Max(360, Screen.height));
        }

        public void SetMasterVolume(float value)
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.MasterVolume = value;
            SetSettings(settings);
        }

        public void SetBgmVolume(float value)
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.BgmVolume = value;
            SetSettings(settings);
        }

        public void SetSfxVolume(float value)
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.SfxVolume = value;
            SetSettings(settings);
        }

        public void SetScreenShakeScale(float value)
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.ScreenShakeScale = value;
            SetSettings(settings);
        }

        public void SetLanguage(GameLanguage language, bool saveToDisk = true)
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.Language = language;
            SetSettings(settings, saveToDisk);
        }

        private void LoadSettingsOrCreateDefault()
        {
            if (TryLoadSettings(out GameSettingsData loadedSettings))
            {
                _settings = loadedSettings;
                return;
            }

            _settings = GameSettingsData.CreateDefault();
            SaveSettings();
        }

        private bool TryLoadSettings(out GameSettingsData settings)
        {
            string path = GetSettingsPath();
            settings = null;

            if (!System.IO.File.Exists(path))
                return false;

            try
            {
                string json = System.IO.File.ReadAllText(path);
                settings = Sanitize(JsonUtility.FromJson<GameSettingsData>(json));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSettingsComponent] Failed to load settings, using defaults: {ex.Message}");
                settings = null;
                return false;
            }
        }

        private void PublishSettingsChanged()
        {
            EnsureSettingsValid();

            ApplyDisplaySettings();
            if (CameraComponent.Instance != null)
                CameraComponent.Instance.SetScreenShakeScale(_settings.ScreenShakeScale);

            EventComponent.Instance.Publish(new CommonGameEvent(SettingsChangedEventName, _settings.Clone()));
        }

        private void EnsureSettingsValid()
        {
            _settings = Sanitize(_settings);
        }

        private static GameSettingsData Sanitize(GameSettingsData settings)
        {
            settings = settings?.Clone() ?? GameSettingsData.CreateDefault();
            GameDisplaySettings.Resolve(settings, GetDesktopSize(), new Vector2Int(Screen.width, Screen.height), Screen.fullScreen);
            return settings;
        }

        private void ApplyDisplaySettings()
        {
            if (_appliedWidth == _settings.ResolutionWidth && _appliedHeight == _settings.ResolutionHeight &&
                _appliedFullscreen == _settings.Fullscreen)
                return;
            _appliedWidth = _settings.ResolutionWidth;
            _appliedHeight = _settings.ResolutionHeight;
            _appliedFullscreen = _settings.Fullscreen;
            // Editor previews and headless servers must not resize the developer's desktop.
#if !UNITY_EDITOR && !UNITY_SERVER
            if (!Application.isBatchMode)
                Screen.SetResolution(_appliedWidth, _appliedHeight,
                    _appliedFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }

        private string GetSettingsFolderPath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, SettingsFolderName);
        }

        private string GetSettingsPath()
        {
            return System.IO.Path.Combine(GetSettingsFolderPath(), SettingsFileName);
        }

        private void EnsureSettingsFolderExists()
        {
            string folderPath = GetSettingsFolderPath();
            if (!System.IO.Directory.Exists(folderPath))
                System.IO.Directory.CreateDirectory(folderPath);
        }
    }
}
