using CrystalMagic.Core;

public class LobbyUI : UIBase<LobbyUIData, CrystalMagic.UI.LobbyUIModel>
{
    private readonly System.Collections.Generic.List<LobbyUI_RoomItemView> roomItemViews = new();
    private bool canEnterRoom = true;

    public event System.Action BackClicked;
    public event System.Action CreateRoomClicked;
    public event System.Action<ulong> RoomClicked;

    public override void OnOpen()
    {
        UI.Back.ButtonPlus.onClick.AddListener(OnBackButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Back.ButtonPlus.onClick.RemoveListener(OnBackButtonClicked);

        for (int i = 0; i < roomItemViews.Count; i++)
            roomItemViews[i].ClearListeners();

        UISubViewBase.ReleaseAllToPool(roomItemViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        RenderRooms(Model.Rooms);
    }

    public void SetRoomInteraction(bool canEnterRoom)
    {
        this.canEnterRoom = canEnterRoom;
        for (int i = 0; i < roomItemViews.Count; i++)
            roomItemViews[i].SetInteractable(canEnterRoom);
    }

    private void RenderRooms(System.Collections.Generic.IReadOnlyList<Server.SimpleRoomData> rooms)
    {
        int roomCount = rooms?.Count ?? 0;
        int viewCount = roomCount + 1;
        LobbyUI_RoomItemView templateView = UI.ScrollView_Viewport_Content_RoomItem.GameObject.GetComponent<LobbyUI_RoomItemView>();
        UI.ScrollView_Viewport_Content_RoomItem.GameObject.SetActive(false);
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
            LobbyUI_RoomItemView roomItemView = UISubViewBase.AcquireFromPool(templateView, UI.ScrollView_Viewport_Content.GameObject.transform);
            roomItemView.Clicked += OnRoomItemClicked;
            roomItemViews.Add(roomItemView);
        }

        roomItemViews[0].RenderCreateRoom();
        for (int i = 0; i < roomCount; i++)
            roomItemViews[i + 1].Render(rooms[i]);

        for (int i = 0; i < roomItemViews.Count; i++)
            roomItemViews[i].SetInteractable(canEnterRoom);
    }

    private void OnRoomItemClicked(ulong roomId)
    {
        if (roomId == 0UL)
        {
            CreateRoomClicked?.Invoke();
            return;
        }

        RoomClicked?.Invoke(roomId);
    }

    private void OnBackButtonClicked()
    {
        BackClicked?.Invoke();
    }
}
