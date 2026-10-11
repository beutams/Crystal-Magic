using CrystalMagic.Core;
using Server;

namespace CrystalMagic.UI
{
    public sealed class CreateRoomUIModel : UIModelBase
    {
        public override string ChangedEventName => "CreateRoomUIModel.Changed";
        public string DisplayName { get; private set; } = string.Empty;
        public string RoomName { get; private set; } = string.Empty;
        public int MaxPlayers { get; private set; } = RoomCreationRules.MaxPlayers;
        public bool IsSubmitting { get; private set; }
        public string ErrorKey { get; private set; } = string.Empty;
        private bool roomNameEdited;

        public void InitializeName(string accountName)
        {
            DisplayName = RoomCreationRules.DefaultDisplayName(accountName);
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = LocalizationComponent.Resolve("ui.lobby.player_fallback");
            roomNameEdited = false;
            RoomName = DefaultRoomName();
            MaxPlayers = RoomCreationRules.MaxPlayers;
            IsSubmitting = false;
            ErrorKey = string.Empty;
            Notify();
        }

        public void SetDisplayName(string value)
        {
            if (IsSubmitting) return;
            DisplayName = value ?? string.Empty;
            if (!roomNameEdited) RoomName = DefaultRoomName();
            ErrorKey = string.Empty;
            Notify();
        }

        public void SetRoomName(string value)
        {
            if (IsSubmitting) return;
            RoomName = value ?? string.Empty;
            roomNameEdited = true;
            ErrorKey = string.Empty;
            Notify();
        }

        public void SetMaxPlayers(int value)
        {
            if (IsSubmitting) return;
            MaxPlayers = value;
            ErrorKey = string.Empty;
            Notify();
        }

        public bool TryBuildRequest(out C2L_CreateRoom request)
        {
            request = null;
            if (IsSubmitting) return false;
            if (!RoomCreationRules.TryNormalize(RoomName, DisplayName, MaxPlayers,
                out string name, out string nickname, out RoomCreationError error))
            {
                ErrorKey = error switch
                {
                    RoomCreationError.DisplayName => "ui.lobby.error.nickname",
                    RoomCreationError.RoomName => "ui.lobby.error.room_name",
                    _ => "ui.lobby.error.capacity",
                };
                Notify();
                return false;
            }
            request = new C2L_CreateRoom { roomName = name, displayName = nickname, maxPlayers = MaxPlayers };
            return true;
        }

        public void SetSubmitting(bool value, string errorKey = "")
        {
            IsSubmitting = value;
            ErrorKey = errorKey;
            Notify();
        }

        public void RefreshLocalization()
        {
            if (!roomNameEdited && !IsSubmitting) RoomName = DefaultRoomName();
            Notify();
        }

        private string DefaultRoomName() => string.Format(
            LocalizationComponent.Resolve("ui.lobby.default_room_name"), DisplayName.Trim());
        private void Notify() => EventComponent.Instance.Publish(new CommonGameEvent(ChangedEventName, this));
    }
}
