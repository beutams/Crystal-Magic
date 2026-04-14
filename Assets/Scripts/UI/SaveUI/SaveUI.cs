using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using UnityEngine;

public class SaveUI : UIBase<SaveUIData>
{
    private readonly List<SaveUI_SaveItemView> _itemViews = new();

    public event Action BackClicked;
    public event Action<int> SaveItemClicked;

    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        UI.Back.Button.onClick.AddListener(OnBackButtonClicked);
    }

    public override void OnClose()
    {
        UI.Back.Button.onClick.RemoveListener(OnBackButtonClicked);
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

        for (int i = 0; i < UI.Content.GameObject.transform.childCount; i++)
        {
            SaveUI_SaveItemView itemView = UI.Content.GameObject.transform.GetChild(i).GetComponent<SaveUI_SaveItemView>();
            itemView.Rebind();
            itemView.Clicked -= HandleItemClicked;

            if (_itemViews.Count < slotCount)
            {
                itemView.gameObject.SetActive(true);
                itemView.Clicked += HandleItemClicked;
                _itemViews.Add(itemView);
            }
            else
            {
                itemView.gameObject.SetActive(false);
            }
        }

        while (_itemViews.Count < slotCount)
        {
            GameObject clone = Instantiate(UI.SaveItem.GameObject, UI.Content.GameObject.transform);
            clone.name = UI.SaveItem.GameObject.name;
            clone.SetActive(true);

            SaveUI_SaveItemView itemView = clone.GetComponent<SaveUI_SaveItemView>();
            itemView.Rebind();
            itemView.Clicked += HandleItemClicked;
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
}
