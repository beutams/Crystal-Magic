using System;
using System.Net;

namespace Server
{
    public enum BattleTransportKind { Tcp, Steam }

    [Serializable]
    public sealed class BattleConnectionInfo
    {
        public const int CurrentProtocolVersion = 2;
        public int protocolVersion = CurrentProtocolVersion;
        public string sessionId;
        public BattleTransportKind kind;
        public string address;
        public int port;
        public ulong hostSteamId;
        public ulong hostAccountId;
        public int steamPort;

        public bool IsHosted => hostAccountId != 0;
        public bool HasTcpEndpoint => IsConnectAddress(address) && port > 0 && port <= 65535;
        public bool HasSteamEndpoint => hostSteamId != 0 && steamPort >= 0 && steamPort <= 65535;
        public static bool IsConnectAddress(string value) => IPAddress.TryParse(value, out IPAddress ip) &&
            !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any) && !ip.Equals(IPAddress.Broadcast);

        public bool IsValid => protocolVersion == CurrentProtocolVersion &&
            Guid.TryParse(sessionId, out Guid session) && session != Guid.Empty &&
            (kind == BattleTransportKind.Tcp
                ? HasTcpEndpoint : kind == BattleTransportKind.Steam && HasSteamEndpoint);

        public BattleConnectionInfo ForTransport(bool steam)
        {
            var selected = new BattleConnectionInfo
            {
                protocolVersion = protocolVersion, sessionId = sessionId, kind = steam ? BattleTransportKind.Steam : BattleTransportKind.Tcp,
                address = address, port = port, hostAccountId = hostAccountId, hostSteamId = hostSteamId, steamPort = steamPort,
            };
            if (!selected.IsValid) throw new ArgumentException("Requested battle transport is unavailable.");
            return selected;
        }

        public NetworkEndpoint ToEndpoint()
        {
            if (!IsValid) throw new ArgumentException("Invalid battle endpoint or incompatible protocol version.");
            return kind == BattleTransportKind.Tcp
                ? new TcpEndpoint(new IPEndPoint(IPAddress.Parse(address), port))
                : new SteamEndpoint(hostSteamId, steamPort);
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
