using System;
using CrystalMagic.Core;
using UnityEngine;

public sealed class CharacterUI_SettingVolumeView : UISubView<CharacterUI_SettingVolumeData>
{
    [SerializeField] private Sprite _activeDot;
    [SerializeField] private Sprite _idleDot;
    private bool _initialized;

    public event Action<float> ValueChanged;
    public event Action EditCompleted;

    public void InitializeBindings()
    {
        if (_initialized)
            return;
        _initialized = true;
        UI.Slider.Slider.onValueChanged.AddListener(value => ValueChanged?.Invoke(value));
        ((CharacterUI_SettingSlider)UI.Slider.Slider).EditCompleted += () => EditCompleted?.Invoke();
    }

    public void Render(float value)
    {
        value = Mathf.Clamp01(value);
        UI.Slider.Slider.SetValueWithoutNotify(value);
        UI.Value.TextMeshProUGUI.text = $"{Mathf.RoundToInt(value * 100f)}%";
        int filled = Mathf.RoundToInt(value * 10f);
        for (int i = 0; i < 10; i++)
            GetDot(i).Image.sprite = i < filled ? _activeDot : _idleDot;
    }

    private UINode GetDot(int index) => index switch
    {
        0 => UI.Slider_Dot1, 1 => UI.Slider_Dot2, 2 => UI.Slider_Dot3,
        3 => UI.Slider_Dot4, 4 => UI.Slider_Dot5, 5 => UI.Slider_Dot6,
        6 => UI.Slider_Dot7, 7 => UI.Slider_Dot8, 8 => UI.Slider_Dot9,
        _ => UI.Slider_Dot10,
    };
}
