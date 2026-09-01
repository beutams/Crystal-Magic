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

        public static string loginIP = "127.0.0.1";
        public static int loginPort = 10001;
        public static IPEndPoint GetLoginInIPEndPoint()
        {
            return new IPEndPoint(IPAddress.Parse(loginIP), loginPort);
        }
        public static string lobbyIP = "127.0.0.1";
        public static int lobbyPort = 10002;
        public static IPEndPoint GetLobbyIPEndPoint()
        {
            return new IPEndPoint(IPAddress.Parse(lobbyIP), lobbyPort);
        }
        public static string battleIP = "127.0.0.1";
        public static int battlePort = 10002;
        public static IPEndPoint GetBattleIPEndPoint()
        {
            return new IPEndPoint(IPAddress.Parse(battleIP), battlePort);
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


        public const long PingInterval = 3_000;
        public const long Timeout = 10_000;
    }
}
