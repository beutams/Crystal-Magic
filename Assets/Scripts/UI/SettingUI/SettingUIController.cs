using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class SettingUIController : UIControllerBase<SettingUI, SettingUIModel>
    {
        public SettingUIController(SettingUI view, SettingUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.MasterVolumeChanged += OnMasterVolumeChanged, () => View.MasterVolumeChanged -= OnMasterVolumeChanged);
            Bindings.Bind(() => View.BgmVolumeChanged += OnBgmVolumeChanged, () => View.BgmVolumeChanged -= OnBgmVolumeChanged);
            Bindings.Bind(() => View.SfxVolumeChanged += OnSfxVolumeChanged, () => View.SfxVolumeChanged -= OnSfxVolumeChanged);
            Bindings.Bind(() => View.PreviousLanguageRequested += OnPreviousLanguageRequested, () => View.PreviousLanguageRequested -= OnPreviousLanguageRequested);
            Bindings.Bind(() => View.NextLanguageRequested += OnNextLanguageRequested, () => View.NextLanguageRequested -= OnNextLanguageRequested);
            Bindings.Bind(() => View.SaveRequested += OnSaveRequested, () => View.SaveRequested -= OnSaveRequested);
            Bindings.Bind(() => View.ResetRequested += OnResetRequested, () => View.ResetRequested -= OnResetRequested);
            Bindings.Bind(() => View.BackRequested += OnBackRequested, () => View.BackRequested -= OnBackRequested);
            Model.ReloadFromSettings();
        }

        private void OnMasterVolumeChanged(float value) => Model.SetMasterVolume(value);
        private void OnBgmVolumeChanged(float value) => Model.SetBgmVolume(value);
        private void OnSfxVolumeChanged(float value) => Model.SetSfxVolume(value);
        private void OnPreviousLanguageRequested() => Model.ShiftLanguage(-1);
        private void OnNextLanguageRequested() => Model.ShiftLanguage(1);
        private void OnSaveRequested() => Model.Save();
        private void OnResetRequested() => Model.RestoreVisibleDefaults();
        private void OnBackRequested() => View.Close();
    }
}
