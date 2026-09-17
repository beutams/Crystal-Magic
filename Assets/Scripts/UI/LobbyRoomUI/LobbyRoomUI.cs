using CrystalMagic.Core;

public class LobbyRoomUI : UIBase<LobbyRoomUIData, CrystalMagic.UI.LobbyRoomUIModel>
{
    public event System.Action LeaveClicked;
    public event System.Action ReadyClicked;
    public event System.Action StartClicked;

    public override void OnOpen()
    {
        UI.Back.ButtonPlus.onClick.AddListener(OnLeaveButtonClicked);
        UI.RoomItem.ButtonPlus.onClick.AddListener(OnStartButtonClicked);
        UI.RoomItem_Player_Ready.ButtonPlus.onClick.AddListener(OnReadyButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Back.ButtonPlus.onClick.RemoveListener(OnLeaveButtonClicked);
        UI.RoomItem.ButtonPlus.onClick.RemoveListener(OnStartButtonClicked);
        UI.RoomItem_Player_Ready.ButtonPlus.onClick.RemoveListener(OnReadyButtonClicked);
        base.OnClose();
    }

    public void SetInteraction(bool canStart, bool canReady, bool canLeave)
    {
        UI.RoomItem.ButtonPlus.enabled = canStart;
        UI.RoomItem_Player_Ready.ButtonPlus.enabled = canReady;
        UI.Back.ButtonPlus.enabled = canLeave;
    }

    protected override void RefreshView()
    {
        Server.RoomData room = Model.Room;
        if (room == null)
            return;

        UI.RoomItem_RoomName.TextMeshProUGUI.text = $"{room.roomName}  Theme {room.themeKey}\nHost: click this card to start";

        System.Text.StringBuilder playerNames = new();
        System.Text.StringBuilder readyStates = new();
        foreach (var player in room.players)
        {
            if (playerNames.Length > 0)
            {
                playerNames.Append('\n');
                readyStates.Append('\n');
            }

            playerNames.Append(player.Value);
            bool ready = room.playerready.TryGetValue(player.Key, out bool isReady) && isReady;
            readyStates.Append(ready ? "READY" : "WAITING");
        }

        UI.RoomItem_Player_PlayerName.TextMeshProUGUI.text = playerNames.ToString();
        UI.RoomItem_Player_Ready.TextMeshProUGUI.text = readyStates.ToString();
        UI.RoomItem_Player_KickOut.GameObject.SetActive(false);
    }

    private void OnLeaveButtonClicked()
    {
        LeaveClicked?.Invoke();
    }

    private void OnReadyButtonClicked()
    {
        ReadyClicked?.Invoke();
    }

    private void OnStartButtonClicked()
    {
        StartClicked?.Invoke();
    }
}
