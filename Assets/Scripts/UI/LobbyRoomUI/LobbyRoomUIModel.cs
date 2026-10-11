namespace CrystalMagic.UI
{
    public sealed class LobbyRoomUIModel : UIModelBase
    {
        public const string RoomChangedEventName = "LobbyRoomUIModel.RoomChanged";
        public override string ChangedEventName => RoomChangedEventName;

        public Server.RoomData Room { get; private set; }
        public ulong LocalAccountId { get; private set; }
        public bool IsMember => Room != null && Room.players.ContainsKey(LocalAccountId);
        public bool IsHost => IsMember && Room.ownerAccountId == LocalAccountId;
        public bool IsReady => IsMember && Room.playerready.TryGetValue(LocalAccountId, out bool ready) && ready;
        public int UnreadyCount
        {
            get
            {
                if (Room == null) return 0;
                int count = 0;
                foreach (ulong accountId in Room.players.Keys)
                    if (accountId != Room.ownerAccountId &&
                        (!Room.playerready.TryGetValue(accountId, out bool ready) || !ready)) count++;
                return count;
            }
        }
        public bool CanConfirm => IsMember && !Room.start && (!IsHost || UnreadyCount == 0);
        public bool CanLeave => IsMember && !Room.start && !IsReady;
        public string ConfirmKey => Room != null && Room.start ? "ui.room.entering" :
            IsHost ? "ui.room.start" : IsReady ? "ui.room.cancel_ready" : "ui.room.ready";
        public string HintKey => Room == null ? "ui.room.no_room" : Room.start ? "ui.room.entering_hint" :
            !IsHost ? IsReady ? "ui.room.guest_ready_hint" : "ui.room.guest_hint" :
            UnreadyCount > 0 ? "ui.room.unready_hint" : Room.players.Count == 1 ? "ui.room.solo_hint" : "ui.room.all_ready_hint";

        public void SetRoom(Server.RoomData room, ulong localAccountId)
        {
            Room = room;
            LocalAccountId = localAccountId;
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(RoomChangedEventName, this));
        }
    }
}
