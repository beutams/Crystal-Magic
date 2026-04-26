using System;
using CrystalMagic.Core;
using TMPro;
using UnityEngine.UI;

public class ShopSellUI : UIBase<ShopSellUIData>
{
    private CrystalMagic.UI.ShopSellUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;

    public event Action AddRequested;
    public event Action ReduceRequested;
    public event Action<string> QuantityInputChanged;
    public event Action ConfirmRequested;
    public event Action CancelRequested;

    public void BindModel(CrystalMagic.UI.ShopSellUIModel model)
    {
        if (_model == model)
            return;

        if (_model != null && _isOpened)
            UnsubscribeModelEvents();

        _model = model;

        if (_model != null && _isOpened)
        {
            SubscribeModelEvents();
            Render();
        }
    }

    public override void OnOpen()
    {
        _isOpened = true;
        UI.Add.ButtonPlus.onClick.AddListener(OnAddButton);
        UI.Reduce.ButtonPlus.onClick.AddListener(OnReduceButton);
        UI.Sure.ButtonPlus.onClick.AddListener(OnConfirmButton);
        UI.Cancel.ButtonPlus.onClick.AddListener(OnCancelButton);
        AddQuantityInputListener();
        SubscribeModelEvents();

        if (_model != null)
            Render();
    }

    public override void OnClose()
    {
        UI.Add.ButtonPlus.onClick.RemoveListener(OnAddButton);
        UI.Reduce.ButtonPlus.onClick.RemoveListener(OnReduceButton);
        UI.Sure.ButtonPlus.onClick.RemoveListener(OnConfirmButton);
        UI.Cancel.ButtonPlus.onClick.RemoveListener(OnCancelButton);
        RemoveQuantityInputListener();
        UnsubscribeModelEvents();
        _isOpened = false;
    }

    private void Render()
    {
        if (_model == null)
            return;

        UI.IconBack_Icon.Image.sprite = LoadIcon(_model.IconPath);
        UI.Name.TextMeshProUGUI.text = _model.Name;
        UI.HaveCount.TextMeshProUGUI.text = _model.HaveCount.ToString();
        UI.Description.TextMeshProUGUI.text = _model.Description;
        SetQuantityInputText(_model.Quantity.ToString());
    }

    private void OnModelChanged(CommonGameEvent gameEvent)
    {
        CrystalMagic.UI.ShopSellUIModel eventModel = gameEvent.GetData<CrystalMagic.UI.ShopSellUIModel>();
        if (eventModel != _model)
            return;

        Render();
    }

    private void SubscribeModelEvents()
    {
        if (_isModelEventSubscribed || _model == null)
            return;

        EventComponent.Instance.Subscribe(new CommonGameEvent(CrystalMagic.UI.ShopSellUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = true;
    }

    private void UnsubscribeModelEvents()
    {
        if (!_isModelEventSubscribed)
            return;

        EventComponent.Instance.Unsubscribe(new CommonGameEvent(CrystalMagic.UI.ShopSellUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = false;
    }

    private void OnAddButton() => AddRequested?.Invoke();
    private void OnReduceButton() => ReduceRequested?.Invoke();
    private void OnConfirmButton() => ConfirmRequested?.Invoke();
    private void OnCancelButton() => CancelRequested?.Invoke();
    private void OnQuantityInputChanged(string value) => QuantityInputChanged?.Invoke(value);

    private void AddQuantityInputListener()
    {
        TMP_InputField tmpInputField = UI.Input.TMP_InputField;
        if (tmpInputField != null)
        {
            tmpInputField.onValueChanged.AddListener(OnQuantityInputChanged);
            return;
        }

        InputField inputField = UI.Input.InputField;
        if (inputField != null)
            inputField.onValueChanged.AddListener(OnQuantityInputChanged);
    }

    private void RemoveQuantityInputListener()
    {
        TMP_InputField tmpInputField = UI.Input.TMP_InputField;
        if (tmpInputField != null)
        {
            tmpInputField.onValueChanged.RemoveListener(OnQuantityInputChanged);
            return;
        }

        InputField inputField = UI.Input.InputField;
        if (inputField != null)
            inputField.onValueChanged.RemoveListener(OnQuantityInputChanged);
    }

    private void SetQuantityInputText(string value)
    {
        TMP_InputField tmpInputField = UI.Input.TMP_InputField;
        if (tmpInputField != null)
        {
            tmpInputField.SetTextWithoutNotify(value ?? string.Empty);
            return;
        }

        InputField inputField = UI.Input.InputField;
        if (inputField != null)
            inputField.SetTextWithoutNotify(value ?? string.Empty);
    }

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
