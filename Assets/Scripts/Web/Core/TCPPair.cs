using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Server
{
    public class TCPPair : IConnectionTransport, IDisposable
    {
        public Socket socket;
        public Connect connect;
        public IPEndPoint IPEndPoint;
        public NetworkEndpoint RemoteEndpoint { get; private set; }
        public readonly MemoryStream ReadStream = new();
        public readonly MemoryStream SendStream = new();
        protected Guid id;
        public static TCPPair CreateTCPPair(Socket socket,IPEndPoint ip, out Guid id)
        {
            TCPPair pair = new TCPPair();
            pair.socket = socket;
            pair.connect = new Connect();
            pair.IPEndPoint = ip;
            pair.RemoteEndpoint = new TcpEndpoint(ip);

            id = Guid.NewGuid();
            pair.id = id;

            pair.connect.Init(pair);

            return pair;
        }

        public void Send(byte[] message)
        {
            if (SendStream.Length + message.Length + 4 > ServerUtility.MaxQueuedBytes)
                throw new InvalidOperationException("TCP send queue exceeded its limit.");
            byte[] packet = TCPPacketCode.Pack(message);
            SendStream.Position = SendStream.Length;
            SendStream.Write(packet, 0, packet.Length);
        }

        public void Dispose()
        {
            try { socket.Shutdown(SocketShutdown.Both); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                socket.Dispose();
                ReadStream.Dispose();
                SendStream.Dispose();
                connect.Dispose();
            }
        }
    }
}
