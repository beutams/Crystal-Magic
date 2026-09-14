using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public sealed class SettingUI : UIBase<SettingUIData, SettingUIModel>
{
    public event Action<float> MasterVolumeChanged;
    public event Action<float> BgmVolumeChanged;
    public event Action<float> SfxVolumeChanged;
    public event Action PreviousLanguageRequested;
    public event Action NextLanguageRequested;
    public event Action SaveRequested;
    public event Action ResetRequested;
    public event Action BackRequested;

    public override void OnOpen()
    {
        UI.Panel_MasterVolumeSlider.Slider.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        UI.Panel_BgmVolumeSlider.Slider.onValueChanged.AddListener(OnBgmVolumeSliderChanged);
        UI.Panel_SfxVolumeSlider.Slider.onValueChanged.AddListener(OnSfxVolumeSliderChanged);
        UI.Panel_LanguagePrevious.ButtonPlus.onClick.AddListener(OnPreviousLanguageButton);
        UI.Panel_LanguageNext.ButtonPlus.onClick.AddListener(OnNextLanguageButton);
        UI.Panel_Save.ButtonPlus.onClick.AddListener(OnSaveButton);
        UI.Panel_Reset.ButtonPlus.onClick.AddListener(OnResetButton);
        UI.Panel_Back.ButtonPlus.onClick.AddListener(OnBackButton);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Panel_MasterVolumeSlider.Slider.onValueChanged.RemoveListener(OnMasterVolumeSliderChanged);
        UI.Panel_BgmVolumeSlider.Slider.onValueChanged.RemoveListener(OnBgmVolumeSliderChanged);
        UI.Panel_SfxVolumeSlider.Slider.onValueChanged.RemoveListener(OnSfxVolumeSliderChanged);
        UI.Panel_LanguagePrevious.ButtonPlus.onClick.RemoveListener(OnPreviousLanguageButton);
        UI.Panel_LanguageNext.ButtonPlus.onClick.RemoveListener(OnNextLanguageButton);
        UI.Panel_Save.ButtonPlus.onClick.RemoveListener(OnSaveButton);
        UI.Panel_Reset.ButtonPlus.onClick.RemoveListener(OnResetButton);
        UI.Panel_Back.ButtonPlus.onClick.RemoveListener(OnBackButton);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        UI.Panel_MasterVolumeSlider.Slider.SetValueWithoutNotify(Model.MasterVolume);
        UI.Panel_BgmVolumeSlider.Slider.SetValueWithoutNotify(Model.BgmVolume);
        UI.Panel_SfxVolumeSlider.Slider.SetValueWithoutNotify(Model.SfxVolume);
        UI.Panel_RowsValue.TextMeshProUGUI.text =
            $"{ToPercent(Model.MasterVolume)}\n{ToPercent(Model.BgmVolume)}\n{ToPercent(Model.SfxVolume)}\n{Model.LanguageDisplayName}";
    }

    private static string ToPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void OnMasterVolumeSliderChanged(float value) => MasterVolumeChanged?.Invoke(value);
    private void OnBgmVolumeSliderChanged(float value) => BgmVolumeChanged?.Invoke(value);
    private void OnSfxVolumeSliderChanged(float value) => SfxVolumeChanged?.Invoke(value);
    private void OnPreviousLanguageButton() => PreviousLanguageRequested?.Invoke();
    private void OnNextLanguageButton() => NextLanguageRequested?.Invoke();
    private void OnSaveButton() => SaveRequested?.Invoke();
    private void OnResetButton() => ResetRequested?.Invoke();
    private void OnBackButton() => BackRequested?.Invoke();
}
