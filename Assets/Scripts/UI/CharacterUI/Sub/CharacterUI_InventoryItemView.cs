using CrystalMagic.Core;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterUI_InventoryItemView : UISubView<CharacterUI_InventoryItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CrystalMagic.UI.CharacterInventoryDisplayData _data;

    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, PointerEventData> DragStarted;
    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, PointerEventData> Dragging;
    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, PointerEventData> DragEnded;

    public void Render(CrystalMagic.UI.CharacterInventoryDisplayData data)
    {
        Rebind();
        _data = data;

        if (data == null)
        {
            UI.Icon.Image.sprite = null;
            UI.Count.TextMeshProUGUI.text = string.Empty;
            UI.Name.TextMeshProUGUI.text = string.Empty;
            return;
        }

        UI.Icon.Image.sprite = LoadIcon(data.IconPath);
        UI.Count.TextMeshProUGUI.text = data.Count.ToString();
        UI.Name.TextMeshProUGUI.text = data.Name;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        DragStarted?.Invoke(_data, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        Dragging?.Invoke(_data, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        DragEnded?.Invoke(_data, eventData);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<Sprite>(iconPath);
    }
}
