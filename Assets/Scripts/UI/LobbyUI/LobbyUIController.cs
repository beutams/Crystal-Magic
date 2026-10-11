namespace CrystalMagic.UI
{
    using CrystalMagic.Core;

    public sealed class LobbyUIController : UIControllerBase<LobbyUI, LobbyUIModel>
    {
        private bool closing;

        public LobbyUIController(LobbyUI view, LobbyUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            CrystalMagic.Core.SceneComponent.Instance.PreloadOnlineWorlds();
            closing = false;
            View.BindModel(Model);
            Bindings.Bind(() => View.BackClicked += OnBackClicked, () => View.BackClicked -= OnBackClicked);
            Bindings.Bind(() => View.CreateRoomClicked += OnCreateRoomClicked, () => View.CreateRoomClicked -= OnCreateRoomClicked);
            Bindings.Bind(() => View.RefreshClicked += Lobby.RefreshRoomList, () => View.RefreshClicked -= Lobby.RefreshRoomList);
            Bindings.Bind(() => View.RoomClicked += OnRoomClicked, () => View.RoomClicked -= OnRoomClicked);
            Bindings.Bind(() => Lobby.onRoomRefresh += OnRoomRefresh, () => Lobby.onRoomRefresh -= OnRoomRefresh);
            Bindings.Bind(() => Lobby.onRoomInfoRefresh += OnRoomInfoRefresh, () => Lobby.onRoomInfoRefresh -= OnRoomInfoRefresh);
            Bindings.Bind(() => Lobby.onRequestFail += OnRequestFail, () => Lobby.onRequestFail -= OnRequestFail);
            Bindings.Bind(() => Lobby.onDisconnected += OnDisconnected, () => Lobby.onDisconnected -= OnDisconnected);
            Bindings.Bind(() => Lobby.onRequestFinished += RefreshConnection, () => Lobby.onRequestFinished -= RefreshConnection);
            Bindings.Bind(() => LocalizationComponent.LanguageChanged += Model.RefreshLocalization,
                () => LocalizationComponent.LanguageChanged -= Model.RefreshLocalization);

            Model.SetAccount(CrystalMagic.Core.SteamComponent.Instance.Account.Name,
                CrystalMagic.Core.SteamComponent.Instance.Account.IsLocalTestAccount);
            Model.SetRooms(Lobby.roomList);
            RefreshConnection();
            Lobby.Initialize();
        }

        protected override void OnClose()
        {
            if (!Lobby.BattleControlActive) Lobby.Cleanup();
        }

        private Server.ClientLobbyManager Lobby => Server.NetworkComponent.Instance.clientLobbyManager;

        private void OnBackClicked()
        {
            RequestClose();
        }

        private void OnCreateRoomClicked()
        {
            if (!Model.CanEnterRoom) return;
            foreach (CrystalMagic.Core.UIBase child in CrystalMagic.Core.UIComponent.Instance.GetChildren(View))
                if (child is CreateRoomUI) return;
            CrystalMagic.Core.UIComponent.Instance.OpenChild<CreateRoomUI>(View);
        }

        private void OnRoomClicked(ulong roomId)
        {
            Lobby.JoinRoom(roomId);
            RefreshConnection();
        }

        private void OnRoomRefresh(Server.RoomListData roomList)
        {
            Model.SetRooms(roomList);
            RefreshConnection();
        }

        private void OnRoomInfoRefresh(Server.RoomData roomData)
        {
            CrystalMagic.Core.UIBase roomView = null;
            foreach (CrystalMagic.Core.UIBase child in CrystalMagic.Core.UIComponent.Instance.GetChildren(View))
            {
                if (child is CreateRoomUI && roomData != null)
                    CrystalMagic.Core.UIComponent.Instance.ReleaseUI(child);
                if (child is LobbyRoomUI lobbyRoomUI)
                {
                    roomView = lobbyRoomUI;
                    break;
                }
            }

            if (roomData == null)
            {
                RefreshConnection();
                if (roomView != null)
                    CrystalMagic.Core.UIComponent.Instance.ReleaseUI(roomView);

                return;
            }

            Model.SetAccount(Lobby.DisplayName, CrystalMagic.Core.SteamComponent.Instance.Account.IsLocalTestAccount);
            RefreshConnection();
            if (roomView == null)
                CrystalMagic.Core.UIComponent.Instance.OpenChild<LobbyRoomUI>(View);
        }

        private void OnRequestFail(LobbyRequestType type)
        {
            RefreshConnection();
            if (type == LobbyRequestType.CreateFail || type == LobbyRequestType.CreateTimeout)
                foreach (CrystalMagic.Core.UIBase child in CrystalMagic.Core.UIComponent.Instance.GetChildren(View))
                    if (child is CreateRoomUI) return;
            string content = type switch
            {
                LobbyRequestType.CreateFail => "创建房间失败",
                LobbyRequestType.CreateTimeout => "创建房间超时",
                LobbyRequestType.JoinFail => "加入房间失败",
                LobbyRequestType.JoinRoomClosed => "房间已关闭",
                LobbyRequestType.JoinRoomFull => "房间已满",
                LobbyRequestType.JoinTimeout => "加入房间超时",
                LobbyRequestType.LeaveFail => "离开房间失败",
                LobbyRequestType.LeaveTimeout => "离开房间超时",
                LobbyRequestType.StartFail => "开始房间失败",
                LobbyRequestType.StartTimeout => "开始房间超时",
                _ => "大厅请求失败",
            };

            CrystalMagic.Core.UIComponent.Instance.OpenChild<ConfirmSingleUI>(View, new ConfirmUIOpenData(
                "Lobby",
                content,
                null,
                null,
                "OK"));
        }

        private void OnDisconnected()
        {
            Model.SetConnection(false, false);
            if (closing || Lobby.DisconnectRequested)
            {
                View.Close();
                return;
            }

            LobbyRequestType loginFailure = Lobby.LoginFailure;
            if (loginFailure != LobbyRequestType.Unknown)
            {
                string content = loginFailure == LobbyRequestType.LoginSaveMismatch
                    ? "该账号正在进行的联机战斗属于另一份存档，请加载原存档后重试。"
                    : "大厅拒绝了本次登录；账号可能已在线，或当前存档身份无效。";
                View.Close();
                CrystalMagic.Core.UIComponent.Instance.Open<ConfirmSingleUI>(new ConfirmUIOpenData(
                    "大厅登录失败",
                    content,
                    null,
                    null,
                    "确定"));
                return;
            }

            CrystalMagic.Core.UIComponent.Instance.OpenChild<ConfirmUI>(View, new ConfirmUIOpenData(
                "Lobby disconnected",
                "The connection to the lobby was lost.",
                () => Lobby.Initialize(),
                RequestClose,
                "Reconnect"));
        }

        private void RequestClose()
        {
            if (closing)
                return;

            closing = true;
            if (Lobby.HasConnection)
            {
                Lobby.Disconnect();
                return;
            }

            View.Close();
        }
        private void RefreshConnection() => Model.SetConnection(Lobby.IsLoggedIn,
            Lobby.IsLoggedIn && !Lobby.HasPendingRequest && Lobby.room == null);
    }
}
