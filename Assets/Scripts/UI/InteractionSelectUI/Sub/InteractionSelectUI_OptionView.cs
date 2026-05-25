using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine.EventSystems;

public class InteractionSelectUI_OptionView : UISubView<InteractionSelectUI_OptionData>, IPointerClickHandler
{
    private InteractionSelectOptionDisplayData _data;

    public event Action<InteractionSelectOptionDisplayData> Clicked;

    public void Render(InteractionSelectOptionDisplayData data)
    {
        Rebind();
        _data = data;
        UI.TextTMP.TextMeshProUGUI.text = data?.DisplayName ?? string.Empty;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null)
            return;

        Clicked?.Invoke(_data);
    }
}
