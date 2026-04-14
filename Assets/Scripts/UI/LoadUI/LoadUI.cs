using CrystalMagic.Core;

using CrystalMagic.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LoadUI : UIBase<LoadUIData>
{
    private readonly List<LoadUI_SaveItemView> _itemViews = new();
    private LoadUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;

    public event Action BackClicked;
    public event Action<int> SaveItemClicked;
    public event Action<int> SaveItemDeleteClicked;

    public void BindModel(LoadUIModel model)
    {
        if (_model == model)
            return;

        if (_model != null && _isOpened)
        {
            UnsubscribeModelEvents();
        }

        _model = model;

        if (_model != null && _isOpened)
        {
            SubscribeModelEvents();
            RenderSlots(_model.SaveRecords, _model.SlotCountValue);
        }
    }

    public override void OnOpen()
    {
        _isOpened = true;
        UI.Back.ButtonPlus.onClick.AddListener(OnBackButtonClicked);
        SubscribeModelEvents();

        if (_model != null)
        {
            RenderSlots(_model.SaveRecords, _model.SlotCountValue);
        }
    }

    public override void OnClose()
    {
        UI.Back.ButtonPlus.onClick.RemoveListener(OnBackButtonClicked);
        UnsubscribeModelEvents();
        _isOpened = false;
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

    private void OnSaveRecordsChanged(CommonGameEvent gameEvent)
    {
        LoadUIModel eventModel = gameEvent.GetData<LoadUIModel>();
        if (eventModel != _model)
            return;

        RenderSlots(_model.SaveRecords, _model.SlotCountValue);
    }

    private void SubscribeModelEvents()
    {
        if (_isModelEventSubscribed || _model == null)
            return;

        EventComponent.Instance.Subscribe(new CommonGameEvent(LoadUIModel.SaveRecordsChangedEventName), OnSaveRecordsChanged);
        _isModelEventSubscribed = true;
    }

    private void UnsubscribeModelEvents()
    {
        if (!_isModelEventSubscribed)
            return;

        EventComponent.Instance.Unsubscribe(new CommonGameEvent(LoadUIModel.SaveRecordsChangedEventName), OnSaveRecordsChanged);
        _isModelEventSubscribed = false;
    }
}
