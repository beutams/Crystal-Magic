using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public sealed class CharacterUI_SettingView : UISubView<CharacterUI_SettingData>
{
    private static readonly Color Idle = new Color32(175, 121, 87, 255);
    private static readonly Color Selected = new Color32(117, 166, 65, 255);
    private bool _initialized;

    public event Action<CharacterSettingsSection> SectionRequested;
    public event Action<CharacterSettingValue, float> ValueChanged;
    public event Action<GameLanguage> LanguageRequested;
    public event Action EditCompleted;
    public event Action SaveRequested;
    public event Action ReturnMainMenuRequested;

    public void InitializeBindings()
    {
        if (_initialized)
            return;
        _initialized = true;
        UI.Navigation_Audio.ButtonPlus.onClick.AddListener(() => SectionRequested?.Invoke(CharacterSettingsSection.Audio));
        UI.Navigation_Language.ButtonPlus.onClick.AddListener(() => SectionRequested?.Invoke(CharacterSettingsSection.Language));
        UI.Navigation_Save.ButtonPlus.onClick.AddListener(() => SaveRequested?.Invoke());
        UI.Navigation_ReturnMainMenu.ButtonPlus.onClick.AddListener(() => ReturnMainMenuRequested?.Invoke());
        UI.Language_Chinese.ButtonPlus.onClick.AddListener(() => LanguageRequested?.Invoke(GameLanguage.ChineseSimplified));
        UI.Language_English.ButtonPlus.onClick.AddListener(() => LanguageRequested?.Invoke(GameLanguage.English));
        for (int i = 0; i < 3; i++)
        {
            var field = (CharacterSettingValue)i;
            CharacterUI_SettingVolumeView row = GetVolume(field);
            row.InitializeBindings();
            row.ValueChanged += value => ValueChanged?.Invoke(field, value);
            row.EditCompleted += () => EditCompleted?.Invoke();
        }
    }

    public void Render(GameSettingsData settings, CharacterSettingsSection section)
    {
        UI.Audio.GameObject.SetActive(section == CharacterSettingsSection.Audio);
        UI.Language.GameObject.SetActive(section == CharacterSettingsSection.Language);
        UI.Navigation_Audio_Label.TextMeshProUGUI.color = section == CharacterSettingsSection.Audio ? Selected : Idle;
        UI.Navigation_Language_Label.TextMeshProUGUI.color = section == CharacterSettingsSection.Language ? Selected : Idle;
        ((LocalizedTextMeshProUGUI)UI.DetailTitle.TextMeshProUGUI).LocalizationKey = section switch
        {
            CharacterSettingsSection.Audio => "ui.character.settings.audio",
            _ => "ui.settings.language",
        };
        UI.Language_Chinese_Label.TextMeshProUGUI.color = settings.Language == GameLanguage.ChineseSimplified ? Selected : Idle;
        UI.Language_English_Label.TextMeshProUGUI.color = settings.Language == GameLanguage.English ? Selected : Idle;
        UI.Language_Chinese_Selected.GameObject.SetActive(settings.Language == GameLanguage.ChineseSimplified);
        UI.Language_English_Selected.GameObject.SetActive(settings.Language == GameLanguage.English);
        GetVolume(CharacterSettingValue.MasterVolume).Render(settings.MasterVolume);
        GetVolume(CharacterSettingValue.BgmVolume).Render(settings.BgmVolume);
        GetVolume(CharacterSettingValue.SfxVolume).Render(settings.SfxVolume);
    }

    private CharacterUI_SettingVolumeView GetVolume(CharacterSettingValue field) => (field switch
    {
        CharacterSettingValue.MasterVolume => UI.Audio_MasterVolume,
        CharacterSettingValue.BgmVolume => UI.Audio_BgmVolume,
        _ => UI.Audio_SfxVolume,
    }).GameObject.GetComponent<CharacterUI_SettingVolumeView>();
}
