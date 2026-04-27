using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class GameMenuUIModel : UIModelBase
    {
        public const string DataChangedEventName = "GameMenuUIModel.DataChanged";

        public override string ChangedEventName => DataChangedEventName;

        public GameSettingsData Settings { get; private set; } = GameSettingsData.CreateDefault();

        public void ReloadFromSettings()
        {
            if (GameSettingsComponent.Instance == null)
                return;

            Settings = GameSettingsComponent.Instance.GetSettingsCopy();
            PublishChanged();
        }

        public void SetMasterVolume(float value)
        {
            Settings.MasterVolume = value;
            Settings.Clamp();
            PublishChanged();
        }

        public void SetBgmVolume(float value)
        {
            Settings.BgmVolume = value;
            Settings.Clamp();
            PublishChanged();
        }

        public void SetUnitVolume(float value)
        {
            Settings.UnitVolume = value;
            Settings.UIVolume = value;
            Settings.Clamp();
            PublishChanged();
        }

        public void SetUIVolume(float value)
        {
            SetUnitVolume(value);
        }

        public void SetScreenShakeScale(float value)
        {
            Settings.ScreenShakeScale = value;
            Settings.Clamp();
            PublishChanged();
        }

        public void ApplySettings(bool saveToDisk = false)
        {
            if (GameSettingsComponent.Instance == null)
                return;

            GameSettingsComponent.Instance.SetSettings(Settings, saveToDisk);
            Settings = GameSettingsComponent.Instance.GetSettingsCopy();
            PublishChanged();
        }

        public void ResetToDefault()
        {
            Settings = GameSettingsData.CreateDefault();
            PublishChanged();
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
