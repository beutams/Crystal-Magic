using System;
using CrystalMagic.Core;
using TMPro;
using UnityEngine.UI;

public class ShopBuyUI : UIBase<ShopBuyUIData, CrystalMagic.UI.ShopBuyUIModel>
{
    public event Action AddRequested;
    public event Action ReduceRequested;
    public event Action<string> QuantityInputChanged;
    public event Action ConfirmRequested;
    public event Action CancelRequested;

    public override void OnOpen()
    {
        UI.Add.ButtonPlus.onClick.AddListener(OnAddButton);
        UI.Reduce.ButtonPlus.onClick.AddListener(OnReduceButton);
        UI.Sure.ButtonPlus.onClick.AddListener(OnConfirmButton);
        UI.Cancel.ButtonPlus.onClick.AddListener(OnCancelButton);
        AddQuantityInputListener();
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Add.ButtonPlus.onClick.RemoveListener(OnAddButton);
        UI.Reduce.ButtonPlus.onClick.RemoveListener(OnReduceButton);
        UI.Sure.ButtonPlus.onClick.RemoveListener(OnConfirmButton);
        UI.Cancel.ButtonPlus.onClick.RemoveListener(OnCancelButton);
        RemoveQuantityInputListener();
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        UI.IconBack_Icon.Image.sprite = LoadIcon(Model.IconPath);
        UI.Name.TextMeshProUGUI.text = Model.Name;
        UI.HaveCount.TextMeshProUGUI.text = Model.HaveCount.ToString();
        UI.Description.TextMeshProUGUI.text = Model.Description;
        SetQuantityInputText(Model.Quantity.ToString());
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
