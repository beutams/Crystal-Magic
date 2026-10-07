using System;
using System.Linq;
using CrystalMagic.Core;

namespace Server
{
    /// <summary>Transport choice is fixed for a session; gameplay still uses the same account IDs and messages.</summary>
    public static class HostedBattlePolicy
    {
        public static bool IsHostRequest(L2C_HostBattleStart request, ulong localAccountId, ulong localSteamId)
        {
            BattleConnectionInfo connection = request?.connection;
            ulong[] players = request?.room?.players;
            if (connection == null || players == null || players.Length == 0 || players.Length > 8 ||
                players.Any(id => id == 0 || id == ClientAccountIdentity.LocalNamespace) || players.Distinct().Count() != players.Length ||
                localAccountId == 0 || connection.hostAccountId != localAccountId || !players.Contains(localAccountId) ||
                !players.Contains(request.room.ownerAccountId) || connection.protocolVersion != BattleConnectionInfo.CurrentProtocolVersion ||
                !Guid.TryParse(connection.sessionId, out Guid session) || session == Guid.Empty || request.room.sessionId != connection.sessionId)
                return false;
            ulong[] steamMembers = players.Where(id => !ClientAccountIdentity.IsLocalNetworkId(id)).OrderBy(id => id).ToArray();
            bool steamHost = steamMembers.Length > 0;
            return request.steamMembers != null && request.steamMembers.OrderBy(id => id).SequenceEqual(steamMembers) &&
                request.tcpRequired == (steamMembers.Length < players.Length) &&
                connection.kind == (steamHost ? BattleTransportKind.Steam : BattleTransportKind.Tcp) &&
                (steamHost ? localSteamId == localAccountId && connection.hostSteamId == localSteamId && connection.HasSteamEndpoint
                           : connection.hostSteamId == 0 && ClientAccountIdentity.IsLocalNetworkId(localAccountId)) &&
                (!request.tcpRequired || (BattleConnectionInfo.IsConnectAddress(connection.address) && connection.port >= 0 && connection.port <= 65535));
        }

        public static Player SelectHost(Room room)
        {
            if (room == null || !room.players.TryGetValue(room.ownerAccountId, out Player owner)) return null;
            if (owner.steamP2PAvailable) return owner;
            // A Steam member must own the native Steam listener. The lobby owner need not change.
            return room.players.Values.Where(p => p.steamP2PAvailable).OrderBy(p => p.accountId).FirstOrDefault() ?? owner;
        }

        public static bool IsReadyDescriptor(BattleConnectionInfo expected, BattleConnectionInfo actual, bool tcpRequired)
        {
            return expected != null && actual != null && actual.IsValid &&
                actual.sessionId == expected.sessionId && actual.protocolVersion == expected.protocolVersion &&
                actual.hostAccountId == expected.hostAccountId && actual.hostSteamId == expected.hostSteamId &&
                actual.kind == expected.kind && actual.steamPort == expected.steamPort &&
                actual.address == expected.address &&
                (tcpRequired ? actual.HasTcpEndpoint && (expected.port == 0 || actual.port == expected.port) : actual.port == expected.port);
        }

        public static bool MatchesPeer(BattleConnectionInfo connection, ulong accountId, NetworkEndpoint peer)
        {
            if (connection == null || accountId == 0) return false;
            if (peer is LoopbackEndpoint loopback) return loopback.AccountId == accountId;
            if (peer is SteamEndpoint steam) return steam.SteamId == accountId && !ClientAccountIdentity.IsLocalNetworkId(accountId);
            // Steam members cannot bypass their platform peer identity by entering through the TCP listener.
            return peer is TcpEndpoint && (!connection.IsHosted || ClientAccountIdentity.IsLocalNetworkId(accountId));
        }
    }
}
