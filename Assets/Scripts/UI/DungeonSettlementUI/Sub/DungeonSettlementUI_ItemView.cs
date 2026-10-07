using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DungeonSettlementUI_ItemView : UISubView<DungeonSettlementUI_ItemData>, IPointerClickHandler
{
    private int _index = -1;
    private string _iconPath;
    public event Action<int> Clicked;

    public void Render(int index, DungeonSettlementItemDisplayData data, bool success, bool selected)
    {
        _index = data == null ? -1 : index;
        string path = data?.IconPath ?? string.Empty;
        if (_iconPath != path || (!string.IsNullOrEmpty(path) && UI.Socket_Icon.Image.sprite == null))
        {
            UI.Socket_Icon.Image.sprite = LoadManagedSprite(path);
            _iconPath = path;
        }
        UI.Socket_Icon.GameObject.SetActive(UI.Socket_Icon.Image.sprite != null && data != null);
        UI.Socket_Icon.Image.color = success ? Color.white : new Color(.55f, .55f, .55f);
        UI.Name.TextMeshProUGUI.text = data?.Name ?? string.Empty;
        UI.Socket_Amount.TextMeshProUGUI.text = data == null ? string.Empty : (success ? "+" : "−") + data.Count;
        UI.Socket_AmountBG.Image.color = success ? new Color(.67f, .58f, .4f) : new Color(.7f, .52f, .46f);
        UI.Selected.GameObject.SetActive(data != null && selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_index >= 0 && eventData.button == PointerEventData.InputButton.Left)
            Clicked?.Invoke(_index);
    }
}
