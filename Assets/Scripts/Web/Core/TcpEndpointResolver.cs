using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    /// <summary>Pure address resolution; sockets and Unity callbacks stay on the network update thread.</summary>
    public static class TcpEndpointResolver
    {
        public static Task<IPEndPoint[]> ResolveAsync(string host, int port) => ResolveAsync(host, port, Dns.GetHostAddressesAsync);

        public static async Task<IPEndPoint[]> ResolveAsync(string host, int port, Func<string, Task<IPAddress[]>> lookup)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            host = host?.Trim();
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("大厅地址不能为空。", nameof(host));
            if (IPAddress.TryParse(host, out IPAddress literal))
                return BuildEndpoints(new[] { literal }, port);
            if (Uri.CheckHostName(host) != UriHostNameType.Dns)
                throw new ArgumentException("大厅地址只填写域名或 IP，不要包含 http://、路径或端口。", nameof(host));
            if (lookup == null) throw new ArgumentNullException(nameof(lookup));
            return BuildEndpoints(await lookup(host).ConfigureAwait(false), port);
        }

        public static IPEndPoint[] BuildEndpoints(IPAddress[] addresses, int port)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            var endpoints = new List<IPEndPoint>();
            var seen = new HashSet<IPAddress>();
            // Prefer the configured A record. IPv6 remains usable if the host also has AAAA records.
            foreach (AddressFamily family in new[] { AddressFamily.InterNetwork, AddressFamily.InterNetworkV6 })
                foreach (IPAddress address in addresses ?? Array.Empty<IPAddress>())
                    if (address != null && address.AddressFamily == family &&
                        !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any) && seen.Add(address))
                        endpoints.Add(new IPEndPoint(address, port));
            if (endpoints.Count == 0) throw new InvalidOperationException("大厅地址没有解析出可连接的 IP 地址。");
            return endpoints.ToArray();
        }

        public static TcpEndpoint CreateListenEndpoint(string address, int port)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (!IPAddress.TryParse(address?.Trim(), out IPAddress ip))
                throw new ArgumentException("服务端监听地址必须是本机 IP；公网部署通常使用 0.0.0.0，而不是域名。", nameof(address));
            return new TcpEndpoint(new IPEndPoint(ip, port));
        }
    }
}
