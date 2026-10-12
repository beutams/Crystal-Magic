using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace Server
{
    public partial class ClientLobbyManager
    {
        private readonly Func<string, int, Task<IPEndPoint[]>> endpointResolver;
        private Task<IPEndPoint[]> pendingResolution;
        private long resolutionDeadline;
        private IPEndPoint[] resolvedEndpoints = Array.Empty<IPEndPoint>();
        private int nextEndpoint;
        private bool tcpEstablished;
        private string connectionAddress;

        public ClientLobbyManager() : this(TcpEndpointResolver.ResolveAsync) { }
        public ClientLobbyManager(Func<string, int, Task<IPEndPoint[]>> resolver) =>
            endpointResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

        public bool IsConnecting => pendingResolution != null || lobbyConnect != null && !loggedIn && !disconnecting;
        public string ConnectionError { get; private set; } = string.Empty;

        private void BeginResolution(string host, int port)
        {
            connectionAddress = host + ":" + port;
            resolutionDeadline = NetworkTimer.Instance.TimeNow + ServerUtility.DnsResolveTimeout;
            pendingResolution = endpointResolver(host, port) ?? throw new InvalidOperationException("DNS 查询没有返回任务。");
            // Unity's supported DNS overload cannot cancel the OS query. Poll only the current task;
            // observe faults even if Cleanup/timeout has already discarded an older task.
            _ = pendingResolution.ContinueWith(failed => { _ = failed.Exception; },
                System.Threading.CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            Debug.Log($"[Lobby] Resolving {connectionAddress}");
        }

        private void TickResolution(long now)
        {
            Task<IPEndPoint[]> task = pendingResolution;
            if (task == null) return;
            if (now >= resolutionDeadline)
            {
                FailConnection($"解析大厅域名超时：{connectionAddress}。请检查网络和 DNS。");
                return;
            }
            if (!task.IsCompleted)
            {
                return;
            }
            pendingResolution = null;
            resolutionDeadline = 0;
            try
            {
                resolvedEndpoints = task.GetAwaiter().GetResult();
                if (resolvedEndpoints == null || resolvedEndpoints.Length == 0)
                    throw new InvalidOperationException("没有可连接的地址。");
                nextEndpoint = 0;
            }
            catch (Exception exception)
            {
                FailConnection($"无法解析或连接大厅 {connectionAddress}：{exception.GetBaseException().Message}");
                return;
            }
            TryNextEndpoint();
        }

        private bool TryNextEndpoint()
        {
            while (nextEndpoint < resolvedEndpoints.Length)
            {
                IPEndPoint endpoint = resolvedEndpoints[nextEndpoint++];
                try
                {
                    clientServic.Connect(new TcpEndpoint(endpoint), out lobbyConnect);
                    if (lobbyConnect == null) throw new InvalidOperationException("传输层没有创建连接。");
                    tcpEstablished = false;
                    loginDeadline = NetworkTimer.Instance.TimeNow + ServerUtility.ConnectTimeout + ServerUtility.Timeout;
                    RegisterLobbyCallbacks(lobbyConnect);
                    Debug.Log($"[Lobby] Connecting {connectionAddress} via {endpoint}");
                    return true;
                }
                catch (Exception exception)
                {
                    if (lobbyConnect != null)
                    {
                        UnregisterLobbyCallbacks(lobbyConnect);
                        clientServic.Disconnect(lobbyConnect);
                        lobbyConnect = null;
                    }
                    ConnectionError = $"无法连接大厅 {connectionAddress}：{exception.GetBaseException().Message}";
                    Debug.LogWarning($"[Lobby] {ConnectionError}");
                }
            }
            FailConnection(string.IsNullOrEmpty(ConnectionError)
                ? $"无法连接大厅 {connectionAddress}，请确认服务端运行并放行对应 TCP 端口。" : ConnectionError);
            return false;
        }

        private void CancelResolution()
        {
            pendingResolution = null;
            resolutionDeadline = 0;
            resolvedEndpoints = Array.Empty<IPEndPoint>();
            nextEndpoint = 0;
        }

        private void FailConnection(string error)
        {
            CancelResolution();
            loginDeadline = 0;
            reconnectAt = NetworkTimer.Instance.TimeNow + ServerUtility.BattleLobbyReconnectInterval;
            ConnectionError = error;
            loggedIn = false;
            Debug.LogWarning($"[Lobby] {error}");
            if (!BattleControlActive) onDisconnected?.Invoke();
        }

        private void UnregisterLobbyCallbacks(Connect connect)
        {
            UnregisterHostCallbacks(connect);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_LoginLobbyResult>(), OnLoginResult);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_StartReturn>(), OnStartResult);
            connect.UnRegisterCallback(MessageCodec.GetOpcode<L2C_StartTicket>(), OnStartTicket);
            connect.OnConnected -= OnLobbyConnected;
            connect.OnDisconnected -= OnDisconnected;
        }
    }
}
