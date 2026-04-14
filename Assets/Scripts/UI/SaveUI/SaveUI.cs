using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public class SaveUI : UIBase<SaveUIData>
{
    private readonly List<SaveUI_SaveItemView> _itemViews = new();
    private SaveUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;

    public event Action BackClicked;
    public event Action<int> SaveItemClicked;
    public event Action<int> SaveItemDeleteClicked;
    public void BindModel(SaveUIModel model)
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
        UI.Back.Button.onClick.AddListener(OnBackButtonClicked);
        SubscribeModelEvents();

        if (_model != null)
        {
            RenderSlots(_model.SaveRecords, _model.SlotCountValue);
        }
    }

    public override void OnClose()
    {
        UI.Back.Button.onClick.RemoveListener(OnBackButtonClicked);
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

        for (int i = 0; i < UI.Content.GameObject.transform.childCount; i++)
        {
            SaveUI_SaveItemView itemView = UI.Content.GameObject.transform.GetChild(i).GetComponent<SaveUI_SaveItemView>();
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
            GameObject clone = Instantiate(UI.SaveItem.GameObject, UI.Content.GameObject.transform);
            clone.name = UI.SaveItem.GameObject.name;
            clone.SetActive(true);

            SaveUI_SaveItemView itemView = clone.GetComponent<SaveUI_SaveItemView>();
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
        SaveUIModel eventModel = gameEvent.GetData<SaveUIModel>();
        if (eventModel != _model)
            return;

        RenderSlots(_model.SaveRecords, _model.SlotCountValue);
    }

    private void SubscribeModelEvents()
    {
        if (_isModelEventSubscribed || _model == null)
            return;

        EventComponent.Instance.Subscribe(new CommonGameEvent(SaveUIModel.SaveRecordsChangedEventName), OnSaveRecordsChanged);
        _isModelEventSubscribed = true;
    }

    private void UnsubscribeModelEvents()
    {
        if (!_isModelEventSubscribed)
            return;

        EventComponent.Instance.Unsubscribe(new CommonGameEvent(SaveUIModel.SaveRecordsChangedEventName), OnSaveRecordsChanged);
        _isModelEventSubscribed = false;
    }
}
