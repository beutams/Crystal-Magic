using CrystalMagic.Core;
using Server;
using System;

public class LobbyUI_RoomItemView : UISubView<LobbyUI_RoomItemData>
{
    public event Action<ulong> Clicked;

    private ulong roomId;
    private bool buttonEventsBound;
    private bool roomAvailable;

    public void Render(SimpleRoomData room)
    {
        Rebind();
        EnsureButtonEventsBound();
        roomId = room.roomId;
        UI.RoomName.TextMeshProUGUI.text = room.roomName;
        UI.HostName.TextMeshProUGUI.text = room.ownerName;
        UI.PlayerCount.TextMeshProUGUI.text = $"{room.enterNum} / {room.maxNum}";
        roomAvailable = !room.start && room.enterNum < room.maxNum;
        string stateKey = room.start ? "ui.lobby.started" : roomAvailable ? "ui.lobby.waiting" : "ui.lobby.full";
        UI.State.TextMeshProUGUI.text = LocalizationComponent.Resolve(stateKey);
        UI.Join_Label.TextMeshProUGUI.text = LocalizationComponent.Resolve(roomAvailable ? "ui.lobby.join" : stateKey);
        UI.Join.CanvasGroup.alpha = roomAvailable ? 1f : 0.55f;
    }

    public void ClearListeners()
    {
        Clicked = null;
    }

    public void SetInteractable(bool interactable)
    {
        UI.Join.ButtonPlus.enabled = interactable && roomAvailable;
    }

    private void EnsureButtonEventsBound()
    {
        if (buttonEventsBound)
            return;

        UI.Join.ButtonPlus.onClick.AddListener(OnClicked);
        buttonEventsBound = true;
    }

    private void OnClicked()
    {
        Clicked?.Invoke(roomId);
    }
}
