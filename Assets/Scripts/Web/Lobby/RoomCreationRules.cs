using System.Text;

namespace Server
{
    public enum RoomCreationError { None, DisplayName, RoomName, Capacity }

    public static class RoomCreationRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;
        public const int DisplayNameLimit = 24;
        public const int RoomNameLimit = 32;

        public static string DefaultDisplayName(string accountName)
        {
            StringBuilder result = new();
            foreach (char character in accountName ?? string.Empty)
                if (!char.IsControl(character)) result.Append(character);
            string name = result.ToString().Trim();
            if (name.Length > DisplayNameLimit)
            {
                int length = DisplayNameLimit;
                if (char.IsHighSurrogate(name[length - 1])) length--;
                name = name.Substring(0, length);
            }
            return name;
        }

        public static bool TryNormalize(string roomName, string displayName, int maxPlayers,
            out string normalizedRoomName, out string normalizedDisplayName, out RoomCreationError error)
        {
            normalizedRoomName = roomName?.Trim() ?? string.Empty;
            normalizedDisplayName = displayName?.Trim() ?? string.Empty;
            error = !ValidName(normalizedDisplayName, DisplayNameLimit) ? RoomCreationError.DisplayName
                : !ValidName(normalizedRoomName, RoomNameLimit) ? RoomCreationError.RoomName
                : maxPlayers < MinPlayers || maxPlayers > MaxPlayers ? RoomCreationError.Capacity
                : RoomCreationError.None;
            return error == RoomCreationError.None;
        }

        private static bool ValidName(string value, int limit)
        {
            if (value.Length == 0 || value.Length > limit) return false;
            foreach (char character in value)
                if (char.IsControl(character)) return false;
            return true;
        }
    }
}
