using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LobbyRoomUI : UIBase<LobbyRoomUIData, CrystalMagic.UI.LobbyRoomUIModel>
{
    private readonly List<PlayerRow> playerRows = new();

    public event Action LeaveClicked;
    public event Action ConfirmClicked;

    public override void OnOpen()
    {
        UI.Cancel.ButtonPlus.onClick.AddListener(OnLeaveButtonClicked);
        UI.Confirm.ButtonPlus.onClick.AddListener(OnConfirmButtonClicked);
        UI.Content_Player.GameObject.SetActive(false);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Cancel.ButtonPlus.onClick.RemoveListener(OnLeaveButtonClicked);
        UI.Confirm.ButtonPlus.onClick.RemoveListener(OnConfirmButtonClicked);
        ReleasePlayerRows();
        base.OnClose();
    }

    public void SetInteraction(bool isHost, bool canConfirm, bool canLeave)
    {
        string confirmText = isHost ? "开始" : "准备";
        UI.Confirm_Default_Text.TextMeshProUGUI.text = confirmText;
        UI.Confirm_Click_Text.TextMeshProUGUI.text = confirmText;
        UI.Confirm.ButtonPlus.enabled = canConfirm;
        UI.Cancel.ButtonPlus.enabled = canLeave;
    }

    protected override void RefreshView()
    {
        Server.RoomData room = Model.Room;
        if (room == null)
        {
            ReleasePlayerRows();
            UI.Content_Player.GameObject.SetActive(false);
            return;
        }

        UI.RoomName.TextMeshProUGUI.text = room.roomName;
        RenderPlayers(room);
    }

    private void OnLeaveButtonClicked()
    {
        LeaveClicked?.Invoke();
    }

    private void OnConfirmButtonClicked()
    {
        ConfirmClicked?.Invoke();
    }

    private void RenderPlayers(Server.RoomData room)
    {
        ReleasePlayerRows();
        UI.Content_Player.GameObject.SetActive(false);
        PoolComponent.Instance.EnsurePool(UI.Content_Player.GameObject, room.players.Count, room.players.Count);

        foreach (KeyValuePair<ulong, string> player in room.players
                     .OrderByDescending(player => player.Key == room.ownerAccountId)
                     .ThenBy(player => player.Key))
        {
            GameObject playerObject = PoolComponent.Instance.Get(UI.Content_Player.GameObject);
            if (playerObject == null)
                continue;

            playerObject.transform.SetParent(UI.Content.GameObject.transform, false);
            playerObject.transform.SetAsLastSibling();

            PlayerRow playerRow = new(playerObject);
            bool isReady = room.playerready.TryGetValue(player.Key, out bool ready) && ready;
            bool canKickOut = room.ownerAccountId == GetLocalAccountId()
                && player.Key != room.ownerAccountId;
            playerRow.Render(player.Value, isReady, canKickOut);
            playerRows.Add(playerRow);
        }
    }

    private void ReleasePlayerRows()
    {
        for (int i = playerRows.Count - 1; i >= 0; i--)
            PoolComponent.Instance.Release(playerRows[i].GameObject);

        playerRows.Clear();
    }

    private ulong GetLocalAccountId()
    {
        return Server.NetworkComponent.Instance.clientLobbyManager.accountId;
    }

    private sealed class PlayerRow
    {
        public PlayerRow(GameObject gameObject)
        {
            GameObject = gameObject;
            UI = new LobbyRoomUI_PlayerData();
            UI.Bind(gameObject.transform);
        }

        public GameObject GameObject { get; }
        private LobbyRoomUI_PlayerData UI { get; }

        public void Render(string playerName, bool isReady, bool canKickOut)
        {
            UI.PlayerName.TextMeshProUGUI.text = playerName;
            UI.Ready_Ready.GameObject.SetActive(isReady);
            UI.Ready_Not.GameObject.SetActive(!isReady);
            UI.KickOut.GameObject.SetActive(canKickOut);
        }
    }
}
