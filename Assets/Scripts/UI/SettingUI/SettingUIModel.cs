using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class SettingUIModel : UIModelBase
    {
        public const string DataChangedEventName = "SettingUIModel.DataChanged";

        private GameSettingsData _settings = GameSettingsData.CreateDefault();

        public override string ChangedEventName => DataChangedEventName;

        public float MasterVolume => _settings.MasterVolume;
        public float BgmVolume => _settings.BgmVolume;
        public float SfxVolume => _settings.SfxVolume;
        public string LanguageDisplayName => _settings.Language == GameLanguage.English ? "English" : "简体中文";

        public void ReloadFromSettings()
        {
            _settings = GameSettingsComponent.Instance.GetSettingsCopy();
            PublishChanged();
        }

        public void SetMasterVolume(float value)
        {
            _settings.MasterVolume = Mathf.Clamp01(value);
            ApplyPreview();
        }

        public void SetBgmVolume(float value)
        {
            _settings.BgmVolume = Mathf.Clamp01(value);
            ApplyPreview();
        }

        public void SetSfxVolume(float value)
        {
            _settings.SfxVolume = Mathf.Clamp01(value);
            ApplyPreview();
        }

        public void ShiftLanguage(int offset)
        {
            int languageCount = System.Enum.GetValues(typeof(GameLanguage)).Length;
            int next = ((int)_settings.Language + offset) % languageCount;
            if (next < 0)
                next += languageCount;

            _settings.Language = (GameLanguage)next;
            ApplyPreview();
        }

        public void RestoreVisibleDefaults()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefault();
            _settings.MasterVolume = defaults.MasterVolume;
            _settings.BgmVolume = defaults.BgmVolume;
            _settings.SfxVolume = defaults.SfxVolume;
            _settings.Language = defaults.Language;
            ApplyPreview();
        }

        public void Save()
        {
            GameSettingsComponent.Instance.SetSettings(_settings, true);
            _settings = GameSettingsComponent.Instance.GetSettingsCopy();
            PublishChanged();
        }

        private void ApplyPreview()
        {
            _settings.Clamp();
            GameSettingsComponent.Instance.SetSettings(_settings);
            _settings = GameSettingsComponent.Instance.GetSettingsCopy();
            PublishChanged();
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
