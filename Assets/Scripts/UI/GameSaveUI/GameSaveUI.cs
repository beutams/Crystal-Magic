using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public class GameSaveUI : UIBase<GameSaveUIData, GameSaveUIModel>
{
    private readonly List<SaveUI_SaveItemView> _itemViews = new();

    public event Action BackClicked;
    public event Action<int> SaveItemClicked;
    public event Action<int> SaveItemDeleteClicked;

    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        UI.Back.ButtonPlus.onClick.AddListener(OnBackButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Back.ButtonPlus.onClick.RemoveListener(OnBackButtonClicked);
        UISubViewBase.ReleaseAllToPool(_itemViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model != null)
            RenderSlots(Model.SaveRecords, Model.SlotCountValue);
    }

    public void RenderSlots(SaveRecord[] records, int slotCount)
    {
        EnsureItemViews(slotCount);

        for (int i = 0; i < _itemViews.Count; i++)
        {
            SaveRecord record = records != null && i < records.Length ? records[i] : null;
            _itemViews[i].Render(i, record);
        }
    }

    private void EnsureItemViews(int slotCount)
    {
        UI.ScrollView_Viewport_Content_SaveItem.GameObject.SetActive(false);

        while (_itemViews.Count > slotCount)
        {
            int lastIndex = _itemViews.Count - 1;
            SaveUI_SaveItemView itemView = _itemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _itemViews.RemoveAt(lastIndex);
        }

        while (_itemViews.Count < slotCount)
        {
            SaveUI_SaveItemView itemView = UISubViewBase.AcquireFromPool(UI.ScrollView_Viewport_Content_SaveItem.GameObject.GetComponent<SaveUI_SaveItemView>(), UI.ScrollView_Viewport_Content.GameObject.transform);
            if (itemView == null)
                break;

            itemView.Clicked -= HandleItemClicked;
            itemView.DeleteClicked -= HandleItemDeleteClicked;
            itemView.Clicked += HandleItemClicked;
            itemView.DeleteClicked += HandleItemDeleteClicked;
            _itemViews.Add(itemView);
        }
    }

    private void HandleItemClicked(int slotIndex)
    {
        SaveItemClicked?.Invoke(slotIndex);
    }

    private void HandleItemDeleteClicked(int slotIndex)
    {
        SaveItemDeleteClicked?.Invoke(slotIndex);
    }

    private void OnBackButtonClicked()
    {
        BackClicked?.Invoke();
    }

}
