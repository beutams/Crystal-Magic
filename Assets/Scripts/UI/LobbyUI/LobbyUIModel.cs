namespace CrystalMagic.UI
{
    public sealed class LobbyUIModel : UIModelBase
    {
        public const string RoomsChangedEventName = "LobbyUIModel.RoomsChanged";
        public override string ChangedEventName => RoomsChangedEventName;

        private readonly System.Collections.Generic.List<Server.SimpleRoomData> rooms = new();
        public System.Collections.Generic.IReadOnlyList<Server.SimpleRoomData> Rooms => rooms;

        public void SetRooms(Server.RoomListData roomList)
        {
            rooms.Clear();
            if (roomList?.roomList != null)
                rooms.AddRange(roomList.roomList);

            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(RoomsChangedEventName, this));
        }
    }
}
