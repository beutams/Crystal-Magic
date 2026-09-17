namespace CrystalMagic.UI
{
    public sealed class LobbyUIController : UIControllerBase<LobbyUI, LobbyUIModel>
    {
        private bool closing;

        public LobbyUIController(LobbyUI view, LobbyUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            closing = false;
            View.BindModel(Model);
            Bindings.Bind(() => View.BackClicked += OnBackClicked, () => View.BackClicked -= OnBackClicked);
            Bindings.Bind(() => View.CreateRoomClicked += OnCreateRoomClicked, () => View.CreateRoomClicked -= OnCreateRoomClicked);
            Bindings.Bind(() => View.RoomClicked += OnRoomClicked, () => View.RoomClicked -= OnRoomClicked);
            Bindings.Bind(() => Lobby.onRoomRefresh += OnRoomRefresh, () => Lobby.onRoomRefresh -= OnRoomRefresh);
            Bindings.Bind(() => Lobby.onRoomInfoRefresh += OnRoomInfoRefresh, () => Lobby.onRoomInfoRefresh -= OnRoomInfoRefresh);
            Bindings.Bind(() => Lobby.onRequestFail += OnRequestFail, () => Lobby.onRequestFail -= OnRequestFail);
            Bindings.Bind(() => Lobby.onDisconnected += OnDisconnected, () => Lobby.onDisconnected -= OnDisconnected);

            Model.SetRooms(Lobby.roomList);
            View.SetRoomInteraction(Lobby.room == null);
            Lobby.Initialize();
        }

        protected override void OnClose()
        {
            Lobby.Cleanup();
        }

        private Server.ClientLobbyManager Lobby => Server.NetworkComponent.Instance.clientLobbyManager;

        private void OnBackClicked()
        {
            RequestClose();
        }

        private void OnCreateRoomClicked()
        {
            Lobby.CreateRoom("Test Room");
        }

        private void OnRoomClicked(ulong roomId)
        {
            Lobby.JoinRoom(roomId);
        }

        private void OnRoomRefresh(Server.RoomListData roomList)
        {
            Model.SetRooms(roomList);
        }

        private void OnRoomInfoRefresh(Server.RoomData roomData)
        {
            CrystalMagic.Core.UIBase roomView = null;
            foreach (CrystalMagic.Core.UIBase child in CrystalMagic.Core.UIComponent.Instance.GetChildren(View))
            {
                if (child is LobbyRoomUI lobbyRoomUI)
                {
                    roomView = lobbyRoomUI;
                    break;
                }
            }

            if (roomData == null)
            {
                View.SetRoomInteraction(true);
                if (roomView != null)
                    CrystalMagic.Core.UIComponent.Instance.ReleaseUI(roomView);

                return;
            }

            View.SetRoomInteraction(false);
            if (roomView == null)
                CrystalMagic.Core.UIComponent.Instance.OpenChild<LobbyRoomUI>(View);
        }

        private void OnRequestFail(LobbyRequestType type)
        {
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
                "OK",
                false));
        }

        private void OnDisconnected()
        {
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
                    "确定",
                    false));
                return;
            }

            CrystalMagic.Core.UIComponent.Instance.OpenChild<ConfirmUI>(View, new ConfirmUIOpenData(
                "Lobby disconnected",
                "The connection to the lobby was lost.",
                () => Lobby.Initialize(),
                RequestClose,
                "Reconnect",
                true));
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
    }
}
