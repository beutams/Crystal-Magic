using CrystalMagic.Core;
using Server;

namespace CrystalMagic.UI
{
    public sealed class CreateRoomUIController : UIControllerBase<CreateRoomUI, CreateRoomUIModel>
    {
        private ClientLobbyManager Lobby => NetworkComponent.Instance.clientLobbyManager;

        public CreateRoomUIController(CreateRoomUI view, CreateRoomUIModel model) : base(view, model) { }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Model.InitializeName(string.IsNullOrEmpty(Lobby.DisplayName)
                ? SteamComponent.Instance.Account.Name : Lobby.DisplayName);
            Bindings.Bind(() => View.DisplayNameChanged += Model.SetDisplayName, () => View.DisplayNameChanged -= Model.SetDisplayName);
            Bindings.Bind(() => View.RoomNameChanged += Model.SetRoomName, () => View.RoomNameChanged -= Model.SetRoomName);
            Bindings.Bind(() => View.CapacityChanged += Model.SetMaxPlayers, () => View.CapacityChanged -= Model.SetMaxPlayers);
            Bindings.Bind(() => View.SubmitClicked += Submit, () => View.SubmitClicked -= Submit);
            Bindings.Bind(() => View.CancelClicked += Cancel, () => View.CancelClicked -= Cancel);
            Bindings.Bind(() => Lobby.onRequestFail += OnRequestFailed, () => Lobby.onRequestFail -= OnRequestFailed);
            Bindings.Bind(() => Lobby.onDisconnected += OnDisconnected, () => Lobby.onDisconnected -= OnDisconnected);
            Bindings.Bind(() => LocalizationComponent.LanguageChanged += Model.RefreshLocalization,
                () => LocalizationComponent.LanguageChanged -= Model.RefreshLocalization);
        }

        private void Submit()
        {
            if (!Model.TryBuildRequest(out C2L_CreateRoom request)) return;
            Model.SetSubmitting(true);
            if (!Lobby.CreateRoom(request.roomName, request.maxPlayers, request.displayName))
                Model.SetSubmitting(false, "ui.lobby.error.not_ready");
        }

        private void Cancel()
        {
            if (!Model.IsSubmitting) View.Close();
        }

        private void OnRequestFailed(LobbyRequestType type)
        {
            if (type == LobbyRequestType.CreateFail || type == LobbyRequestType.CreateTimeout)
                Model.SetSubmitting(false, type == LobbyRequestType.CreateTimeout
                    ? "ui.lobby.error.timeout" : "ui.lobby.error.create_failed");
        }

        private void OnDisconnected() => Model.SetSubmitting(false, "ui.lobby.error.not_ready");
    }
}
