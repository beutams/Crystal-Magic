namespace Server
{
    public class LobbyRequest
    {
        public LobbyRequestType type;
        public Connect connect;
        public long waitTimerId;
        public long timeoutTimerId;
    }
}
