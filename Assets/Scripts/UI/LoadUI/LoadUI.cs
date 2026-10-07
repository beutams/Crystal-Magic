using CrystalMagic.Core;

using CrystalMagic.UI;
using System;

public class LoadUI : UIBase<LoadUIData, LoadUIModel>
{
    private LoadUI_SaveItemView[] _itemViews;

    public event Action BackClicked;
    public event Action<int> SaveItemClicked;
    public event Action<int> SaveItemDeleteClicked;

    protected override void OnInit()
    {
        base.OnInit();
        _itemViews = new[]
        {
            UI.Panel_Slots_Slot1.GameObject.GetComponent<LoadUI_SaveItemView>(),
            UI.Panel_Slots_Slot2.GameObject.GetComponent<LoadUI_SaveItemView>(),
            UI.Panel_Slots_Slot3.GameObject.GetComponent<LoadUI_SaveItemView>(),
        };
    }

    public override void OnOpen()
    {
        UI.Back.ButtonPlus.onClick.AddListener(OnBackButtonClicked);
        foreach (LoadUI_SaveItemView item in _itemViews)
        {
            item.Clicked += HandleItemClicked;
            item.DeleteClicked += HandleItemDeleteClicked;
        }
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Back.ButtonPlus.onClick.RemoveListener(OnBackButtonClicked);
        foreach (LoadUI_SaveItemView item in _itemViews)
        {
            item.Clicked -= HandleItemClicked;
            item.DeleteClicked -= HandleItemDeleteClicked;
        }
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model != null)
            RenderSlots(Model.SaveRecords);
    }

    public void RenderSlots(SaveRecord[] records)
    {
        for (int i = 0; i < _itemViews.Length; i++)
        {
            SaveRecord record = records != null && i < records.Length ? records[i] : null;
            _itemViews[i].Render(i, record);
        }
    }

    private void HandleItemClicked(int slotIndex)
    {
        SaveItemClicked?.Invoke(slotIndex);
    }

    private void OnBackButtonClicked()
    {
        BackClicked?.Invoke();
    }

    private void HandleItemDeleteClicked(int slotIndex)
    {
        SaveItemDeleteClicked?.Invoke(slotIndex);
    }

}
