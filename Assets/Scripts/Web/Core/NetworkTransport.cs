using System;

namespace Server
{
    /// <summary>由具体传输实现解释的地址；业务层不需要知道 IP、端口或平台账号格式。</summary>
    public abstract class NetworkEndpoint
    {
        public abstract override string ToString();
    }

    /// <summary>
    /// 单条连接的消息发送端。Send 接收一条完整的 MessageCodec 消息，负责可靠、有序发送。
    /// 实现只能读取消息字节，不得修改；入队和回调均由主线程驱动。
    /// TCP 长度头、流缓冲和原生连接句柄均由实现持有。
    /// </summary>
    public interface IConnectionTransport
    {
        NetworkEndpoint RemoteEndpoint { get; }
        void Send(byte[] message);
    }

    /// <summary>
    /// 传输服务拥有连接资源；Update 在主线程派发消息和连接事件。
    /// Shutdown 释放全部连接和回调，可重复调用，不再派发业务回调。
    /// </summary>
    public interface INetworkTransport
    {
        // OnSend 表示数据已交给底层，不代表对端已收到。
        event Action<Connect> OnSend;
        event Action<Connect> OnRecv;
        event Action<Connect> OnDisconnected;

        void Init();
        void Update();
        void Shutdown();
        void Disconnect(Connect connect);
        // 先排空已入队消息，再关闭；不保证对端应用已经处理这些消息。
        void DisconnectAfterSend(Connect connect);
    }

    public interface IClientTransport : INetworkTransport
    {
        event Action<Connect> OnConnecting;
        event Action<Connect> OnConnectedFail;

        // 返回 Pending 连接；所有事件延迟到 Update，供调用者先绑定协议回调。
        void Connect(NetworkEndpoint endpoint, out Connect connect);
    }

    public interface IServerTransport : INetworkTransport
    {
        event Action OnListening;
        event Action OnListeningFail;
        event Action<Connect> OnAccept;
        NetworkEndpoint LocalEndpoint { get; }
    }
}
