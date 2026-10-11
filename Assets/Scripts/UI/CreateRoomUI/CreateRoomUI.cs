using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public sealed class CreateRoomUI : UIBase<CreateRoomUIData, CreateRoomUIModel>
{
    public event Action<string> DisplayNameChanged;
    public event Action<string> RoomNameChanged;
    public event Action<int> CapacityChanged;
    public event Action SubmitClicked;
    public event Action CancelClicked;

    public override void OnOpen()
    {
        UI.Panel_DisplayName.TMP_InputField.onValueChanged.AddListener(OnDisplayNameChanged);
        UI.Panel_RoomName.TMP_InputField.onValueChanged.AddListener(OnRoomNameChanged);
        UI.Panel_CapacityTwo.ButtonPlus.onClick.AddListener(ChooseTwo);
        UI.Panel_CapacityThree.ButtonPlus.onClick.AddListener(ChooseThree);
        UI.Panel_CapacityFour.ButtonPlus.onClick.AddListener(ChooseFour);
        UI.Panel_Submit.ButtonPlus.onClick.AddListener(OnSubmit);
        UI.Panel_Cancel.ButtonPlus.onClick.AddListener(OnCancel);
        UI.Panel_Close.ButtonPlus.onClick.AddListener(OnCancel);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Panel_DisplayName.TMP_InputField.onValueChanged.RemoveListener(OnDisplayNameChanged);
        UI.Panel_RoomName.TMP_InputField.onValueChanged.RemoveListener(OnRoomNameChanged);
        UI.Panel_CapacityTwo.ButtonPlus.onClick.RemoveListener(ChooseTwo);
        UI.Panel_CapacityThree.ButtonPlus.onClick.RemoveListener(ChooseThree);
        UI.Panel_CapacityFour.ButtonPlus.onClick.RemoveListener(ChooseFour);
        UI.Panel_Submit.ButtonPlus.onClick.RemoveListener(OnSubmit);
        UI.Panel_Cancel.ButtonPlus.onClick.RemoveListener(OnCancel);
        UI.Panel_Close.ButtonPlus.onClick.RemoveListener(OnCancel);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (UI.Panel_DisplayName.TMP_InputField.text != Model.DisplayName)
            UI.Panel_DisplayName.TMP_InputField.SetTextWithoutNotify(Model.DisplayName);
        if (UI.Panel_RoomName.TMP_InputField.text != Model.RoomName)
            UI.Panel_RoomName.TMP_InputField.SetTextWithoutNotify(Model.RoomName);
        UI.Panel_DisplayName.TMP_InputField.interactable = !Model.IsSubmitting;
        UI.Panel_RoomName.TMP_InputField.interactable = !Model.IsSubmitting;
        UI.Panel_Cancel.ButtonPlus.enabled = !Model.IsSubmitting;
        UI.Panel_Close.ButtonPlus.enabled = !Model.IsSubmitting;
        UI.Panel_Submit.ButtonPlus.enabled = !Model.IsSubmitting;
        UI.Panel_CapacityTwo.ButtonPlus.enabled = !Model.IsSubmitting;
        UI.Panel_CapacityThree.ButtonPlus.enabled = !Model.IsSubmitting;
        UI.Panel_CapacityFour.ButtonPlus.enabled = !Model.IsSubmitting;
        Capacity(UI.Panel_CapacityTwo_Selected, UI.Panel_CapacityTwo_Label, Model.MaxPlayers == 2);
        Capacity(UI.Panel_CapacityThree_Selected, UI.Panel_CapacityThree_Label, Model.MaxPlayers == 3);
        Capacity(UI.Panel_CapacityFour_Selected, UI.Panel_CapacityFour_Label, Model.MaxPlayers == 4);
        UI.Panel_CapacityHint.TextMeshProUGUI.text = string.Format(
            LocalizationComponent.Resolve("ui.lobby.capacity_hint"), Model.MaxPlayers);
        UI.Panel_Submit_Label.TextMeshProUGUI.text = LocalizationComponent.Resolve(
            Model.IsSubmitting ? "ui.lobby.creating" : "ui.lobby.create_and_enter");
        UI.Panel_Error.TextMeshProUGUI.text = LocalizationComponent.Resolve(Model.ErrorKey);
        UI.Panel_Error.GameObject.SetActive(!string.IsNullOrEmpty(Model.ErrorKey));
    }

    private static void Capacity(UINode selected, UINode label, bool active)
    {
        selected.GameObject.SetActive(active);
        label.TextMeshProUGUI.color = active ? new Color32(242,228,198,255) : new Color32(74,48,39,255);
    }

    private void OnDisplayNameChanged(string value) => DisplayNameChanged?.Invoke(value);
    private void OnRoomNameChanged(string value) => RoomNameChanged?.Invoke(value);
    private void ChooseTwo() => CapacityChanged?.Invoke(2);
    private void ChooseThree() => CapacityChanged?.Invoke(3);
    private void ChooseFour() => CapacityChanged?.Invoke(4);
    private void OnSubmit() => SubmitClicked?.Invoke();
    private void OnCancel() => CancelClicked?.Invoke();
}
