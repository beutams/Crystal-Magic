using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Server
{
    public class TCPPair
    {
        public Socket socket;
        public Connect connect;
        public IPEndPoint IPEndPoint;
        protected Guid id;
        public static TCPPair CreateTCPPair(Socket socket,IPEndPoint ip, out Guid id)
        {
            TCPPair pair = new TCPPair();
            pair.socket = socket;
            pair.connect = new Connect();
            pair.IPEndPoint = ip;

            id = Guid.NewGuid();
            pair.id = id;

            pair.connect.Init(ip);

            return pair;
        }
    }
}
