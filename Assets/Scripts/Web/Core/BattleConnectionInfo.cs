using System;
using System.Net;

namespace Server
{
    public enum BattleTransportKind { Tcp, Steam }

    [Serializable]
    public sealed class BattleConnectionInfo
    {
        public const int CurrentProtocolVersion = 1;
        public int protocolVersion = CurrentProtocolVersion;
        public string sessionId;
        public BattleTransportKind kind;
        public string address;
        public int port;
        public ulong hostSteamId;

        public bool IsValid => protocolVersion == CurrentProtocolVersion &&
            Guid.TryParse(sessionId, out Guid session) && session != Guid.Empty &&
            (kind == BattleTransportKind.Tcp
                ? IPAddress.TryParse(address, out _) && port > 0 && port <= 65535
                : kind == BattleTransportKind.Steam && hostSteamId != 0 && port >= 0 && port <= 65535);

        public NetworkEndpoint ToEndpoint()
        {
            if (!IsValid) throw new ArgumentException("Invalid battle endpoint or incompatible protocol version.");
            return kind == BattleTransportKind.Tcp
                ? new TcpEndpoint(new IPEndPoint(IPAddress.Parse(address), port))
                : new SteamEndpoint(hostSteamId, port);
        }

        public static BattleConnectionInfo Dedicated(string sessionId) => new()
        {
            sessionId = sessionId, kind = BattleTransportKind.Tcp,
            address = ServerUtility.battleIP, port = ServerUtility.battlePort,
        };
    }

    public sealed class SteamEndpoint : NetworkEndpoint
    {
        public ulong SteamId { get; }
        public int VirtualPort { get; }
        public SteamEndpoint(ulong steamId, int virtualPort)
        {
            SteamId = steamId;
            VirtualPort = virtualPort;
        }
        public override string ToString() => $"steam:{SteamId}/{VirtualPort}";
    }
}
