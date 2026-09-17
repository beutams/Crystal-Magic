namespace CrystalMagic.UI
{
    public sealed class LobbyRoomUIController : UIControllerBase<LobbyRoomUI, LobbyRoomUIModel>
    {
        public LobbyRoomUIController(LobbyRoomUI view, LobbyRoomUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.LeaveClicked += OnLeaveClicked, () => View.LeaveClicked -= OnLeaveClicked);
            Bindings.Bind(() => View.ReadyClicked += OnReadyClicked, () => View.ReadyClicked -= OnReadyClicked);
            Bindings.Bind(() => View.StartClicked += OnStartClicked, () => View.StartClicked -= OnStartClicked);
            Bindings.Bind(() => Lobby.onRoomInfoRefresh += OnRoomInfoRefresh, () => Lobby.onRoomInfoRefresh -= OnRoomInfoRefresh);

            Model.SetRoom(Lobby.room);
            RefreshInteraction(Lobby.room);
        }

        private Server.ClientLobbyManager Lobby => Server.NetworkComponent.Instance.clientLobbyManager;

        private void OnLeaveClicked()
        {
            if (Model.Room != null
                && Model.Room.playerready.TryGetValue(Lobby.accountId, out bool ready)
                && ready)
            {
                return;
            }

            Lobby.LeaveRoom();
        }

        private void OnReadyClicked()
        {
            Server.RoomData room = Model.Room;
            if (room == null || room.start || room.ownerAccountId == Lobby.accountId
                || !room.players.ContainsKey(Lobby.accountId)
                || !room.playerready.TryGetValue(Lobby.accountId, out bool ready))
                return;

            Lobby.Ready(!ready);
        }

        private void OnStartClicked()
        {
            Server.RoomData room = Model.Room;
            if (room == null || room.start || room.ownerAccountId != Lobby.accountId)
                return;

            foreach (var player in room.playerready)
            {
                if (player.Key != room.ownerAccountId && !player.Value)
                    return;
            }

            Lobby.StartRoom();
        }

        private void OnRoomInfoRefresh(Server.RoomData room)
        {
            Model.SetRoom(room);
            RefreshInteraction(room);
        }

        private void RefreshInteraction(Server.RoomData room)
        {
            bool isCurrentPlayer = room != null && room.players.ContainsKey(Lobby.accountId);
            bool canStart = isCurrentPlayer && !room.start && room.ownerAccountId == Lobby.accountId;
            bool canReady = isCurrentPlayer && !room.start && room.ownerAccountId != Lobby.accountId;
            bool isReady = room != null && room.playerready.TryGetValue(Lobby.accountId, out bool ready) && ready;
            bool canLeave = isCurrentPlayer && !room.start && !isReady;
            View.SetInteraction(canStart, canReady, canLeave);
        }
    }
}
