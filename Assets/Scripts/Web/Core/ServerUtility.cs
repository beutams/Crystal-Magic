using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using UnityEditor.VersionControl;

namespace Server
{
    public static class ServerUtility
    {
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



        public const long PingInterval = 3_000;
        public const long Timeout = 10_000;
    }
}