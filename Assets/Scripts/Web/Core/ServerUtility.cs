using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

namespace Server
{
    public static class ServerUtility
    {
        private static long roomIdCounter;
        private static long battleRoomIdCounter;

        public static string lobbyIP = "127.0.0.1";
        public static int lobbyPort = 10002;
        public static NetworkEndpoint GetLobbyEndpoint()
        {
            return new TcpEndpoint(new IPEndPoint(IPAddress.Parse(lobbyIP), lobbyPort));
        }
        public static string battleIP = "127.0.0.1";
        public static int battlePort = 10003;
        public static NetworkEndpoint GetBattleEndpoint()
        {
            return new TcpEndpoint(new IPEndPoint(IPAddress.Parse(battleIP), battlePort));
        }
        public static int battleLobbyPort = 10004;
        public static NetworkEndpoint GetBattleLobbyEndpoint()
        {
            return new TcpEndpoint(new IPEndPoint(IPAddress.Parse(battleIP), battleLobbyPort));
        }

        public static ulong CreateRoomId()
        {
            return (ulong)Interlocked.Increment(ref roomIdCounter);
        }
        public static ulong CreateBattleRoomId()
        {
            return (ulong)Interlocked.Increment(ref battleRoomIdCounter);
        }

        public static string CreateBattleTicket()
        {
            byte[] bytes = new byte[32];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        public static int CreateBattleSeed()
        {
            byte[] bytes = new byte[4];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            int seed = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            return seed == 0 ? 1 : seed;
        }


        public const long PingInterval = 3_000;
        public const long Timeout = 10_000;
        public const long ConnectTimeout = 10_000;
        public const long DrainTimeout = 2_000;
        public const long HostStartTimeout = 15_000;
        public const long SettlementTimeout = 5_000;
        public const long ReconnectGrace = 60_000;
        public const int MaxQueuedBytes = 4 * MessageCodec.MaxMessageLength;
        public const long BattleLobbyReconnectInterval = 1_000;
        public const long BattleEnterTimeout = 45_000;
        public const long BattleInitializeTimeout = 45_000;
        public const long BattleReadyTimeout = 30_000;
    }
}
