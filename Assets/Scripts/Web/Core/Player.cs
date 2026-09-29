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
        public bool steamP2PAvailable;

        public Connect connect;
    }
}
