namespace CrystalMagic.UI
{
    public sealed class LobbyRoomUIModel : UIModelBase
    {
        public const string RoomChangedEventName = "LobbyRoomUIModel.RoomChanged";
        public override string ChangedEventName => RoomChangedEventName;

        public Server.RoomData Room { get; private set; }

        public void SetRoom(Server.RoomData room)
        {
            Room = room;
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(RoomChangedEventName, this));
        }
    }
}
