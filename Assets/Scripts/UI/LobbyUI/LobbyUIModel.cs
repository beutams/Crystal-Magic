namespace CrystalMagic.UI
{
    public sealed class LobbyUIModel : UIModelBase
    {
        public const string RoomsChangedEventName = "LobbyUIModel.RoomsChanged";
        public override string ChangedEventName => RoomsChangedEventName;

        private readonly System.Collections.Generic.List<Server.SimpleRoomData> rooms = new();
        public System.Collections.Generic.IReadOnlyList<Server.SimpleRoomData> Rooms => rooms;
        public string DisplayName { get; private set; } = string.Empty;
        public bool LocalAccount { get; private set; }
        public bool Connected { get; private set; }
        public bool CanEnterRoom { get; private set; }

        public void SetAccount(string displayName, bool localAccount)
        {
            DisplayName = displayName;
            LocalAccount = localAccount;
            Notify();
        }

        public void SetConnection(bool connected, bool canEnterRoom)
        {
            Connected = connected;
            CanEnterRoom = canEnterRoom;
            Notify();
        }

        public void RefreshLocalization() => Notify();

        public void SetRooms(Server.RoomListData roomList)
        {
            rooms.Clear();
            if (roomList?.roomList != null)
                rooms.AddRange(roomList.roomList);

            Notify();
        }
        private void Notify() => CrystalMagic.Core.EventComponent.Instance.Publish(
            new CrystalMagic.Core.CommonGameEvent(RoomsChangedEventName, this));
    }
}
