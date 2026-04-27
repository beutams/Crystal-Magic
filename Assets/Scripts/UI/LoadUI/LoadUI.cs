using CrystalMagic.Core;

using CrystalMagic.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LoadUI : UIBase<LoadUIData, LoadUIModel>
{
    private readonly List<LoadUI_SaveItemView> _itemViews = new();

    public event Action BackClicked;
    public event Action<int> SaveItemClicked;
    public event Action<int> SaveItemDeleteClicked;

    public override void OnOpen()
    {
        UI.Back.ButtonPlus.onClick.AddListener(OnBackButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Back.ButtonPlus.onClick.RemoveListener(OnBackButtonClicked);
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
        _itemViews.Clear();

        for (int i = 0; i < UI.ScrollView_Viewport_Content.GameObject.transform.childCount; i++)
        {
            LoadUI_SaveItemView itemView = UI.ScrollView_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<LoadUI_SaveItemView>();
            itemView.Rebind();
            itemView.Clicked -= HandleItemClicked;
            itemView.DeleteClicked -= HandleItemDeleteClicked;

            if (_itemViews.Count < slotCount)
            {
                itemView.gameObject.SetActive(true);
                itemView.Clicked += HandleItemClicked;
                itemView.DeleteClicked += HandleItemDeleteClicked;
                _itemViews.Add(itemView);
            }
            else
            {
                itemView.gameObject.SetActive(false);
            }
        }

        while (_itemViews.Count < slotCount)
        {
            GameObject clone = Instantiate(UI.ScrollView_Viewport_Content_SaveItem.GameObject, UI.ScrollView_Viewport_Content.GameObject.transform);
            clone.name = UI.ScrollView_Viewport_Content_SaveItem.GameObject.name;
            clone.SetActive(true);

            LoadUI_SaveItemView itemView = clone.GetComponent<LoadUI_SaveItemView>();
            itemView.Rebind();
            itemView.Clicked += HandleItemClicked;
            itemView.DeleteClicked += HandleItemDeleteClicked;
            _itemViews.Add(itemView);
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
