using System;
using CrystalMagic.Core;
using CrystalMagic.UI;

public class InteractionSelectUI_OptionView : UISubView<InteractionSelectUI_OptionData>
{
    private InteractionSelectOptionDisplayData _data;
    private bool _buttonEventBound;

    public event Action<InteractionSelectOptionDisplayData> Clicked;

    public void Render(InteractionSelectOptionDisplayData data)
    {
        Rebind();
        EnsureButtonEventBound();
        _data = data;
        string displayName = data?.DisplayName ?? string.Empty;
        UI.Default_TextTMP.TextMeshProUGUI.text = displayName;
        UI.Click_TextTMP.TextMeshProUGUI.text = displayName;
    }

    private void EnsureButtonEventBound()
    {
        if (_buttonEventBound)
            return;

        GetComponent<ButtonPlus>().onClick.AddListener(OnClicked);
        _buttonEventBound = true;
    }

    private void OnClicked()
    {
        if (_data == null)
            return;

        Clicked?.Invoke(_data);
    }
}
