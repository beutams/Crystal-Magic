using CrystalMagic.Core;

public class LobbyUI : UIBase<LobbyUIData, CrystalMagic.UI.LobbyUIModel>
{
    private readonly System.Collections.Generic.List<LobbyUI_RoomItemView> roomItemViews = new();

    public event System.Action BackClicked;
    public event System.Action CreateRoomClicked;
    public event System.Action RefreshClicked;
    public event System.Action<ulong> RoomClicked;

    public override void OnOpen()
    {
        UI.Panel_Back.ButtonPlus.onClick.AddListener(OnBackButtonClicked);
        UI.Panel_CreateRoom.ButtonPlus.onClick.AddListener(OnCreateRoomButtonClicked);
        UI.Panel_Refresh.ButtonPlus.onClick.AddListener(OnRefreshButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Panel_Back.ButtonPlus.onClick.RemoveListener(OnBackButtonClicked);
        UI.Panel_CreateRoom.ButtonPlus.onClick.RemoveListener(OnCreateRoomButtonClicked);
        UI.Panel_Refresh.ButtonPlus.onClick.RemoveListener(OnRefreshButtonClicked);

        for (int i = 0; i < roomItemViews.Count; i++)
            roomItemViews[i].ClearListeners();

        UISubViewBase.ReleaseAllToPool(roomItemViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        RenderRooms(Model.Rooms);
        UI.Panel_PlayerName.TextMeshProUGUI.text = Model.DisplayName;
        UI.Panel_AccountSource.TextMeshProUGUI.text = LocalizationComponent.Resolve(
            Model.LocalAccount ? "ui.lobby.local_account" : "ui.lobby.steam_name");
        UI.Panel_Connection.TextMeshProUGUI.text = LocalizationComponent.Resolve(
            Model.Connected ? "ui.lobby.connected" : "ui.lobby.connecting");
        UI.Panel_RoomCount.TextMeshProUGUI.text = string.Format(LocalizationComponent.Resolve("ui.lobby.room_count"), Model.Rooms.Count);
        UI.Panel_Empty.GameObject.SetActive(Model.Rooms.Count == 0);
        UI.Panel_CreateRoom.ButtonPlus.enabled = Model.CanEnterRoom;
        UI.Panel_Refresh.ButtonPlus.enabled = Model.Connected;
    }

    private void RenderRooms(System.Collections.Generic.IReadOnlyList<Server.SimpleRoomData> rooms)
    {
        int roomCount = rooms?.Count ?? 0;
        int viewCount = roomCount;
        LobbyUI_RoomItemView templateView = UI.Panel_RoomScroll_Viewport_Content_RoomItem.GameObject.GetComponent<LobbyUI_RoomItemView>();
        UI.Panel_RoomScroll_Viewport_Content_RoomItem.GameObject.SetActive(false);
        UISubViewBase.EnsurePoolCapacity(templateView, viewCount, viewCount);

        while (roomItemViews.Count > viewCount)
        {
            int lastIndex = roomItemViews.Count - 1;
            LobbyUI_RoomItemView roomItemView = roomItemViews[lastIndex];
            roomItemView.ClearListeners();
            UISubViewBase.ReleaseToPool(roomItemView);
            roomItemViews.RemoveAt(lastIndex);
        }

        while (roomItemViews.Count < viewCount)
        {
            LobbyUI_RoomItemView roomItemView = UISubViewBase.AcquireFromPool(templateView, UI.Panel_RoomScroll_Viewport_Content.GameObject.transform);
            roomItemView.Clicked += OnRoomItemClicked;
            roomItemViews.Add(roomItemView);
        }

        for (int i = 0; i < roomCount; i++)
            roomItemViews[i].Render(rooms[i]);

        for (int i = 0; i < roomItemViews.Count; i++)
            roomItemViews[i].SetInteractable(Model.CanEnterRoom);
    }

    private void OnRoomItemClicked(ulong roomId)
    {
        RoomClicked?.Invoke(roomId);
    }

    private void OnCreateRoomButtonClicked()
    {
        CreateRoomClicked?.Invoke();
    }

    private void OnBackButtonClicked()
    {
        BackClicked?.Invoke();
    }
    private void OnRefreshButtonClicked() => RefreshClicked?.Invoke();
}
