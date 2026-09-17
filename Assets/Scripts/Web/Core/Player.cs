namespace Server
{
    public class Player
    {
        public ulong accountId;
        public string username;
        public string saveGuid;

        public ulong roomId;
        public bool ready;
        public bool start;

        public Connect connect;
    }
}
