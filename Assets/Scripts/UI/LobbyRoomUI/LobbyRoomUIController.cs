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
            CrystalMagic.Core.SceneComponent.Instance.PreloadOnlineWorlds();
            View.BindModel(Model);
            Bindings.Bind(() => View.LeaveClicked += OnLeaveClicked, () => View.LeaveClicked -= OnLeaveClicked);
            Bindings.Bind(() => View.ConfirmClicked += OnConfirmClicked, () => View.ConfirmClicked -= OnConfirmClicked);
            Bindings.Bind(() => Lobby.onRoomInfoRefresh += OnRoomInfoRefresh, () => Lobby.onRoomInfoRefresh -= OnRoomInfoRefresh);
            Bindings.Bind(() => CrystalMagic.Core.LocalizationComponent.LanguageChanged += RefreshLocalization,
                () => CrystalMagic.Core.LocalizationComponent.LanguageChanged -= RefreshLocalization);

            Model.SetRoom(Lobby.room, Lobby.accountId);
        }

        private Server.ClientLobbyManager Lobby => Server.NetworkComponent.Instance.clientLobbyManager;

        private void OnLeaveClicked()
        {
            if (!Model.CanLeave)
                return;

            Lobby.LeaveRoom();
        }

        private void OnConfirmClicked()
        {
            if (!Model.CanConfirm)
                return;

            if (Model.IsHost)
                Lobby.StartRoom();
            else
                Lobby.Ready(!Model.IsReady);
        }

        private void OnRoomInfoRefresh(Server.RoomData room)
        {
            Model.SetRoom(room, Lobby.accountId);
        }

        private void RefreshLocalization() => Model.SetRoom(Lobby.room, Lobby.accountId);
    }
}
