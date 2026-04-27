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
            SetSettings(GameSettingsData.CreateDefault(), saveToDisk);
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

        public void SetUnitVolume(float value)
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.UnitVolume = value;
            settings.UIVolume = value;
            SetSettings(settings);
        }

        public void SetUIVolume(float value)
        {
            SetUnitVolume(value);
        }

        public void SetScreenShakeScale(float value)
        {
            GameSettingsData settings = GetSettingsCopy();
            settings.ScreenShakeScale = value;
            SetSettings(settings);
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
            settings ??= GameSettingsData.CreateDefault();
            settings.Clamp();
            settings.UIVolume = settings.UnitVolume;
            return settings;
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
