using System;
using System.Net;

namespace Server
{
    public sealed class TcpEndpoint : NetworkEndpoint
    {
        public IPEndPoint Address { get; }

        public TcpEndpoint(IPEndPoint address)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }

        public override string ToString() => Address.ToString();
    }
}
