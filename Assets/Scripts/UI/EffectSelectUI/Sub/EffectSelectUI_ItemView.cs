using CrystalMagic.Core;
using CrystalMagic.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class EffectSelectUI_ItemView : UISubView<EffectSelectUI_ItemData>, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private EffectSelectAdditionDisplayData _data;
    private UISelectableListItem _selectableItem;

    public event Action<EffectSelectAdditionDisplayData> HoverEntered;
    public event Action<EffectSelectAdditionDisplayData> HoverExited;
    public event Action<EffectSelectAdditionDisplayData> Clicked;

    public void Render(EffectSelectAdditionDisplayData data)
    {
        Rebind();
        _data = data;
        _selectableItem ??= GetComponent<UISelectableListItem>();

        if (data == null)
        {
            UI.Icon.Image.sprite = null;
            UI.Name.TextMeshProUGUI.text = string.Empty;
            if (_selectableItem != null)
                _selectableItem.SetSelected(false);
            return;
        }

        UI.Icon.Image.sprite = LoadIcon(data.IconPath);
        UI.Name.TextMeshProUGUI.text = data.Name;

        if (_selectableItem != null)
            _selectableItem.SetSelected(data.IsSelected);
        else if (UI.Background.Image != null)
            UI.Background.Image.color = data.IsSelected ? Color.white : new Color(1f, 1f, 1f, 0.85f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_data == null)
            return;

        HoverEntered?.Invoke(_data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_data == null)
            return;

        HoverExited?.Invoke(_data);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null)
            return;

        Clicked?.Invoke(_data);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedResource<Sprite>(iconPath);
    }
}
