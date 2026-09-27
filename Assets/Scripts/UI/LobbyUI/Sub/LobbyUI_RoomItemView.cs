using CrystalMagic.Core;
using Server;
using System;

public class LobbyUI_RoomItemView : UISubView<LobbyUI_RoomItemData>
{
    public event Action<ulong> Clicked;

    private ulong roomId;
    private bool buttonEventsBound;

    public void Render(SimpleRoomData room)
    {
        Rebind();
        EnsureButtonEventsBound();
        roomId = room.roomId;
        UI.Open_Index.TextMeshProUGUI.text = room.roomName;
        UI.Open_RoomName.TextMeshProUGUI.text = $"Theme {room.themeKey}";
        UI.Open_Player.TextMeshProUGUI.text = $"{room.enterNum} / {room.maxNum}";
        UI.Open_Delete.GameObject.SetActive(false);
    }

    public void ClearListeners()
    {
        Clicked = null;
    }

    public void SetInteractable(bool interactable)
    {
        GetComponent<ButtonPlus>().enabled = interactable;
    }

    private void EnsureButtonEventsBound()
    {
        if (buttonEventsBound)
            return;

        GetComponent<ButtonPlus>().onClick.AddListener(OnClicked);
        buttonEventsBound = true;
    }

    private void OnClicked()
    {
        Clicked?.Invoke(roomId);
    }
}
