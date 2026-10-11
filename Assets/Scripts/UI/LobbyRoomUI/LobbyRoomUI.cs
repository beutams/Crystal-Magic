using CrystalMagic.Core;
using System;
using System.Linq;
using UnityEngine;

public class LobbyRoomUI : UIBase<LobbyRoomUIData, CrystalMagic.UI.LobbyRoomUIModel>
{
    private PlayerRow[] playerRows;

    public event Action LeaveClicked;
    public event Action ConfirmClicked;

    protected override void OnInit()
    {
        base.OnInit();
        playerRows = new[] {
            new PlayerRow(UI.Panel_Players_One.GameObject),
            new PlayerRow(UI.Panel_Players_Two.GameObject),
            new PlayerRow(UI.Panel_Players_Three.GameObject),
            new PlayerRow(UI.Panel_Players_Four.GameObject),
        };
    }

    public override void OnOpen()
    {
        UI.Panel_Leave.ButtonPlus.onClick.AddListener(OnLeaveButtonClicked);
        UI.Panel_Confirm.ButtonPlus.onClick.AddListener(OnConfirmButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Panel_Leave.ButtonPlus.onClick.RemoveListener(OnLeaveButtonClicked);
        UI.Panel_Confirm.ButtonPlus.onClick.RemoveListener(OnConfirmButtonClicked);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        Server.RoomData room = Model.Room;
        UI.Panel_RoomName.TextMeshProUGUI.text = room?.roomName ?? LocalizationComponent.Resolve("ui.room.no_room");
        string host = room != null && room.players.TryGetValue(room.ownerAccountId, out string name) ? name : "";
        UI.Panel_HostName.TextMeshProUGUI.text = Format("ui.room.host_name", host);
        UI.Panel_PlayerCount.TextMeshProUGUI.text = Format("ui.room.player_count", room?.players.Count ?? 0, room?.maxNum ?? 0);
        UI.Panel_ReadyCount.TextMeshProUGUI.text = Format("ui.room.ready_count",
            (room?.players.Count ?? 0) - Model.UnreadyCount, room?.players.Count ?? 0);
        UI.Panel_Hint.TextMeshProUGUI.text = Format(Model.HintKey, Model.UnreadyCount);
        UI.Panel_State.TextMeshProUGUI.text = LocalizationComponent.Resolve(room != null && room.start ? "ui.room.entering" :
            Model.IsHost ? Model.UnreadyCount > 0 ? "ui.room.waiting_ready" : "ui.room.can_start" :
            Model.IsReady ? "ui.room.waiting_host" : "ui.room.not_ready");
        UI.Panel_Confirm_Label.TextMeshProUGUI.text = LocalizationComponent.Resolve(Model.ConfirmKey);
        SetButton(UI.Panel_Confirm, Model.CanConfirm);
        SetButton(UI.Panel_Leave, Model.CanLeave);

        var players = room?.players.OrderByDescending(player => player.Key == room.ownerAccountId)
            .ThenBy(player => player.Key).ToArray();
        for (int i = 0; i < playerRows.Length; i++)
        {
            bool occupied = players != null && i < players.Length;
            ulong id = occupied ? players[i].Key : 0;
            bool isHost = occupied && id == room.ownerAccountId;
            bool isReady = occupied && (isHost || room.playerready.TryGetValue(id, out bool ready) && ready);
            playerRows[i].Render(i + 1, i < (room?.maxNum ?? 0), occupied ? players[i].Value : null,
                isHost, occupied && id == Model.LocalAccountId, isReady);
        }
    }

    private static string Format(string key, params object[] args) => string.Format(LocalizationComponent.Resolve(key), args);

    private static void SetButton(UINode node, bool enabled)
    {
        node.ButtonPlus.enabled = enabled;
        node.CanvasGroup.alpha = enabled ? 1f : .5f;
    }

    private void OnLeaveButtonClicked()
    {
        LeaveClicked?.Invoke();
    }

    private void OnConfirmButtonClicked()
    {
        ConfirmClicked?.Invoke();
    }

    private sealed class PlayerRow
    {
        private readonly CanvasGroup canvasGroup;
        public PlayerRow(GameObject gameObject)
        {
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            UI = new LobbyRoomUI_PlayerData();
            UI.Bind(gameObject.transform);
        }

        private LobbyRoomUI_PlayerData UI { get; }

        public void Render(int number, bool open, string playerName, bool isHost, bool isLocal, bool isReady)
        {
            bool occupied = playerName != null;
            canvasGroup.alpha = open ? 1f : .4f;
            UI.Number.TextMeshProUGUI.text = number.ToString("00");
            UI.Selected.GameObject.SetActive(occupied && isLocal);
            UI.HostIcon.GameObject.SetActive(occupied && isHost);
            string role = LocalizationComponent.Resolve(occupied ? isHost ? "ui.lobby.host" : "ui.room.member" :
                open ? "ui.room.open_slot" : "ui.room.closed_slot");
            UI.Role.TextMeshProUGUI.text = isLocal ? Format("ui.room.you", role) : role;
            UI.PlayerName.TextMeshProUGUI.text = occupied ? playerName : LocalizationComponent.Resolve(
                open ? "ui.room.empty_slot" : "ui.room.closed_slot_hint");
            UI.Status.TextMeshProUGUI.text = occupied ? LocalizationComponent.Resolve(
                isHost ? "ui.room.host_status" : isReady ? "ui.room.is_ready" : "ui.room.not_ready") : "";
            UI.Status.TextMeshProUGUI.color = isReady ? new Color32(83, 100, 71, 255) : new Color32(129, 98, 81, 255);
        }
    }
}
