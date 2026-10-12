using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LobbyDnsConnectionTests
{
    [TestCase("127.0.0.1")]
    [TestCase("::1")]
    public void LiteralAddressesDoNotQueryDns(string literal)
    {
        bool queried = false;
        var endpoints = TcpEndpointResolver.ResolveAsync(literal, 10002, _ =>
        { queried = true; throw new Exception("DNS must not run"); }).GetAwaiter().GetResult();
        Assert.That(queried, Is.False);
        Assert.That(endpoints.Single().Address, Is.EqualTo(IPAddress.Parse(literal)));
        Assert.That(endpoints.Single().Port, Is.EqualTo(10002));
    }

    [Test]
    public void DomainLookupPrefersIpv4AndRemovesDuplicatesAndUnspecifiedAddresses()
    {
        string queried = null;
        var addresses = new[] { IPAddress.IPv6Loopback, IPAddress.Any, IPAddress.Loopback, IPAddress.IPv6Any, IPAddress.Loopback };
        var endpoints = TcpEndpointResolver.ResolveAsync(" lobby.mpsqaq.top ", 10002, host =>
        { queried = host; return Task.FromResult(addresses); }).GetAwaiter().GetResult();
        Assert.That(queried, Is.EqualTo("lobby.mpsqaq.top"));
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }, endpoints.Select(e => e.Address));
        Assert.That(endpoints.All(e => e.Port == 10002), Is.True);
    }

    [TestCase("http://lobby.mpsqaq.top")]
    [TestCase("lobby.mpsqaq.top:10002")]
    [TestCase("lobby.mpsqaq.top/path")]
    [TestCase("")]
    public void UrlsAndPortsCannotBeMistakenForHostname(string invalid)
    {
        Assert.Throws<ArgumentException>(() => TcpEndpointResolver.ResolveAsync(invalid, 10002,
            _ => Task.FromResult(new[] { IPAddress.Loopback })).GetAwaiter().GetResult());
    }

    [Test]
    public void InvalidPortsEmptyRecordsAndUnspecifiedClientAddressesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TcpEndpointResolver.ResolveAsync("localhost", 0).GetAwaiter().GetResult());
        Assert.Throws<ArgumentOutOfRangeException>(() => TcpEndpointResolver.CreateListenEndpoint("0.0.0.0", 65536));
        Assert.Throws<InvalidOperationException>(() => TcpEndpointResolver.BuildEndpoints(Array.Empty<IPAddress>(), 10002));
        Assert.Throws<InvalidOperationException>(() => TcpEndpointResolver.ResolveAsync("0.0.0.0", 10002).GetAwaiter().GetResult());
        Assert.Throws<ArgumentException>(() => TcpEndpointResolver.CreateListenEndpoint("lobby.mpsqaq.top", 10002));
    }

    [Test]
    public void ClientAndServerDefaultsAreSeparateAndLaunchArgumentsOverrideBoth()
    {
        var client = new LobbyAccountConfig();
        Assert.That(client.lobbyAddress, Is.EqualTo("lobby.mpsqaq.top"));
        var server = ClientAccountPolicy.ForLaunch(new LobbyAccountConfig { lobbyAddress = ServerUtility.lobbyIP, lobbyPort = ServerUtility.lobbyPort },
            new[] { "-lobbyAddress", "127.0.0.1", "-lobbyPort", "15000" }, false);
        Assert.That(ServerUtility.lobbyIP, Is.EqualTo("0.0.0.0"));
        Assert.That(server.lobbyAddress, Is.EqualTo("127.0.0.1"));
        Assert.That(server.lobbyPort, Is.EqualTo(15000));
        Assert.That(TcpEndpointResolver.CreateListenEndpoint("0.0.0.0", 10002).Address.Address, Is.EqualTo(IPAddress.Any));
        var config = JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "Res/Config/LobbyAccountConfig.json")));
        Assert.That((string)config["lobbyAddress"], Is.EqualTo(client.lobbyAddress));
        Assert.That((int)config["lobbyPort"], Is.EqualTo(client.lobbyPort));
    }

    [Test]
    public void InitializeIsIdempotentDuringDnsAndOnlyUpdateCreatesTheConnection()
    {
        var completion = new TaskCompletionSource<IPEndPoint[]>();
        int queries = 0;
        using var fixture = new Fixture((_, _) => { queries++; return completion.Task; });
        fixture.Lobby.Initialize(); fixture.Lobby.Initialize();
        Assert.That(queries, Is.EqualTo(1));
        Assert.That(fixture.Lobby.IsConnecting, Is.True);
        Assert.That(fixture.Transport.Created.Count, Is.Zero);
        completion.SetResult(new[] { Endpoint() });
        Assert.That(fixture.Transport.Created.Count, Is.Zero);
        fixture.Lobby.Update();
        Assert.That(fixture.Transport.Created.Count, Is.EqualTo(1));
        Assert.That(fixture.Lobby.HasConnection, Is.True);
        fixture.Lobby.Initialize();
        Assert.That(fixture.Transport.Created.Count, Is.EqualTo(1));
    }

    [Test]
    public void CleanupAndReopenIgnoreAnOldDnsResult()
    {
        var old = new TaskCompletionSource<IPEndPoint[]>();
        var current = new TaskCompletionSource<IPEndPoint[]>();
        int queries = 0;
        using var fixture = new Fixture((_, _) => ++queries == 1 ? old.Task : current.Task);
        fixture.Lobby.Initialize(); fixture.Lobby.Cleanup(); fixture.Lobby.Initialize();
        old.SetResult(new[] { Endpoint(10003) }); fixture.Lobby.Update();
        Assert.That(fixture.Transport.Created.Count, Is.Zero);
        current.SetResult(new[] { Endpoint(10004) }); fixture.Lobby.Update();
        Assert.That(fixture.Transport.Created.Single().RemoteEndpoint.ToString(), Is.EqualTo(Endpoint(10004).ToString()));
    }

    [Test]
    public void DnsTimeoutReportsFailureOnceAndNeverConnectsTheLateResult()
    {
        var completion = new TaskCompletionSource<IPEndPoint[]>();
        using var fixture = new Fixture((_, _) => completion.Task);
        int failures = 0; fixture.Lobby.onDisconnected += () => failures++;
        fixture.Lobby.Initialize();
        Set(fixture.Lobby, "resolutionDeadline", 0L);
        fixture.Lobby.Update(); fixture.Lobby.Update();
        Assert.That(failures, Is.EqualTo(1));
        Assert.That(fixture.Lobby.ConnectionError, Does.Contain("超时"));
        Assert.That(fixture.Lobby.IsConnecting, Is.False);
        completion.SetResult(new[] { Endpoint() }); fixture.Lobby.Update();
        Assert.That(fixture.Transport.Created.Count, Is.Zero);
    }

    [Test]
    public void DnsFailureIsNotMisreportedAsServerLoginRejectionAndCanRetry()
    {
        int queries = 0;
        using var fixture = new Fixture((_, _) => ++queries == 1
            ? Task.FromException<IPEndPoint[]>(new SocketException((int)SocketError.HostNotFound))
            : Task.FromResult(new[] { Endpoint() }));
        int failures = 0; fixture.Lobby.onDisconnected += () => failures++;
        fixture.Lobby.Initialize();
        Assert.That(failures, Is.EqualTo(1));
        Assert.That(fixture.Lobby.LoginFailure, Is.EqualTo(LobbyRequestType.Unknown));
        Assert.That(fixture.Lobby.ConnectionError, Does.Contain("lobby.mpsqaq.top"));
        fixture.Lobby.Initialize();
        Assert.That(fixture.Lobby.ConnectionError, Is.Empty);
        Assert.That(fixture.Transport.Created.Count, Is.EqualTo(1));
    }

    [Test]
    public void DisconnectDuringDnsIsExplicitAndDoesNotBecomeAConnectionLater()
    {
        var completion = new TaskCompletionSource<IPEndPoint[]>();
        using var fixture = new Fixture((_, _) => completion.Task);
        int callbacks = 0; fixture.Lobby.onDisconnected += () => callbacks++;
        fixture.Lobby.Initialize(); fixture.Lobby.Disconnect();
        Assert.That(fixture.Lobby.DisconnectRequested, Is.True);
        Assert.That(callbacks, Is.EqualTo(1));
        completion.SetResult(new[] { Endpoint() }); fixture.Lobby.Update();
        Assert.That(fixture.Transport.Created, Is.Empty);
    }

    [Test]
    public void FailedDnsAddressFallsBackButExplicitDisconnectDoesNot()
    {
        using var fixture = new Fixture((_, _) => Task.FromResult(new[] { Endpoint(10003), Endpoint(10004) }));
        int failures = 0; fixture.Lobby.onDisconnected += () => failures++;
        fixture.Lobby.Initialize();
        fixture.Transport.Fail(fixture.Transport.Created[0]);
        Assert.That(fixture.Transport.Created.Count, Is.EqualTo(2));
        Assert.That(failures, Is.Zero);
        fixture.Lobby.Disconnect();
        Assert.That(fixture.Transport.Created.Count, Is.EqualTo(2));
        Assert.That(failures, Is.EqualTo(1));
    }

    [Test]
    public void ExhaustingDnsAddressesReportsOneFailureAndAllowsReinitialization()
    {
        using var fixture = new Fixture((_, _) => Task.FromResult(new[] { Endpoint(10003), Endpoint(10004) }));
        int failures = 0; fixture.Lobby.onDisconnected += () => failures++;
        fixture.Lobby.Initialize();
        fixture.Transport.Fail(fixture.Transport.Created[0]); fixture.Transport.Fail(fixture.Transport.Created[1]);
        Assert.That(failures, Is.EqualTo(1));
        Assert.That(fixture.Lobby.HasConnection, Is.False);
        Assert.That(fixture.Lobby.ConnectionError, Does.Contain("TCP"));
        fixture.Lobby.Initialize();
        Assert.That(fixture.Transport.Created.Count, Is.EqualTo(3));
    }

    private static IPEndPoint Endpoint(int port = 10002) => new(IPAddress.Loopback, port);
    [Test]
    public void ResolvedHostnameConnectsThroughTheRealTcpTransport()
    {
        MessageCodec.Init();
        var server = new ServerService(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
        var client = new ClientService();
        try
        {
            server.Init(); client.Init();
            int port = ((TcpEndpoint)server.LocalEndpoint).Address.Port;
            var endpoints = TcpEndpointResolver.ResolveAsync("localhost", port).GetAwaiter().GetResult();
            client.Connect(new TcpEndpoint(endpoints.First(e => e.AddressFamily == AddressFamily.InterNetwork)), out Connect connection);
            bool connected = false; connection.OnConnected += _ => connected = true;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!connected && clock.ElapsedMilliseconds < 5000)
            {
                server.Update(); client.Update(); NetworkTimer.Instance.Update(); System.Threading.Thread.Sleep(1);
            }
            Assert.That(connected, Is.True);
            Assert.That(connection.RemoteEndpoint.ToString(), Does.EndWith(":" + port));
        }
        finally { client.Shutdown(); server.Shutdown(); }
    }
    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new("Lobby DNS test components");
        public readonly FakeTransport Transport = new();
        public readonly ClientLobbyManager Lobby;
        public Fixture(Func<string, int, Task<IPEndPoint[]>> resolver)
        {
            MessageCodec.Init();
            var network = root.AddComponent<NetworkComponent>(); network.lobbyTransport = Transport;
            var steam = root.AddComponent<SteamComponent>(); Set(steam, "accountConfig", new LobbyAccountConfig());
            Lobby = new ClientLobbyManager(resolver);
        }
        public void Dispose() { Lobby.Cleanup(); Object.DestroyImmediate(root); }
    }

    private sealed class FakeTransport : IClientTransport
    {
        public readonly List<Connect> Created = new();
        public event Action<Connect> OnConnecting { add { } remove { } }
        public event Action<Connect> OnConnectedFail { add { } remove { } }
        public event Action<Connect> OnSend { add { } remove { } }
        public event Action<Connect> OnRecv { add { } remove { } }
        public event Action<Connect> OnDisconnected { add { } remove { } }
        public void Connect(NetworkEndpoint endpoint, out Connect connect)
        {
            connect = new Connect(); connect.Init(new Peer(endpoint)); Created.Add(connect);
        }
        public void Fail(Connect connect)
        {
            var failure = new DisconnectInfo();
            typeof(DisconnectInfo).GetProperty(nameof(DisconnectInfo.Reason)).SetValue(failure, DisconnectReason.ConnectFailed);
            typeof(Server.Connect).GetProperty(nameof(Server.Connect.LastDisconnectInfo)).SetValue(connect, failure);
            connect.State = ConnectState.Close; connect.OnDisconnected?.Invoke(connect);
        }
        public void Disconnect(Connect connect) { connect.State = ConnectState.Close; connect.OnDisconnected?.Invoke(connect); }
        public void DisconnectAfterSend(Connect connect) => Disconnect(connect);
        public void Init() { }
        public void Update() { }
        public void Shutdown() { }
    }
    private sealed class Peer : IConnectionTransport
    {
        public NetworkEndpoint RemoteEndpoint { get; }
        public Peer(NetworkEndpoint endpoint) => RemoteEndpoint = endpoint;
        public void Send(byte[] message) { }
    }
}
