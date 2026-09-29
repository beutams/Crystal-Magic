using System;
using System.Collections.Generic;

namespace Server
{
    /// <summary>房主客户端也经过序列化和下一次 Update 的消息队列，不直接递归调用权威逻辑。</summary>
    public sealed class LoopbackTransport : MessageTransport
    {
        private sealed class Link
        {
            public LoopbackTransport Owner;
            public Peer Peer;
        }
        private readonly Queue<Peer> pending = new();
        private LoopbackTransport remote;
        private readonly NetworkEndpoint identity;
        private LoopbackTransport(NetworkEndpoint identity) => this.identity = identity;

        public static void CreatePair(ulong hostId, out LoopbackTransport server, out LoopbackTransport client)
        {
            server = new LoopbackTransport(new SteamEndpoint(hostId, 0));
            client = new LoopbackTransport(new SteamEndpoint(hostId, 0));
            server.LocalEndpoint = server.identity;
            server.remote = client;
            client.remote = server;
        }

        protected override void Open() { }
        protected override void BeginConnect(Peer peer)
        {
            if (!remote.active) { Fail(peer, DisconnectReason.ConnectFailed, "LoopbackHostClosed"); return; }
            remote.pending.Enqueue(peer);
        }
        protected override void Poll()
        {
            while (pending.Count > 0)
            {
                Peer outgoing = pending.Dequeue();
                if (outgoing.Connection.State == ConnectState.Close || !remote.active) continue;
                Peer incoming = AddPeer(remote.identity, true);
                incoming.Native = new Link { Owner = remote, Peer = outgoing };
                outgoing.Native = new Link { Owner = this, Peer = incoming };
                incoming.Ready = outgoing.Ready = true;
            }
        }
        protected override bool TrySendPacket(Peer peer, byte[] packet)
        {
            Link link = (Link)peer.Native;
            if (!link.Owner.active || link.Peer.Connection.State == ConnectState.Close)
            { Fail(peer, DisconnectReason.RemoteClosed, "LoopbackPeerClosed"); return false; }
            link.Owner.EnqueueIncoming(link.Peer, packet);
            return true;
        }
        protected override bool IsNativeDrained(Peer peer) =>
            peer.Native is Link link && link.Peer.Incoming.Count == 0;
        protected override void ClosePeer(Peer peer)
        {
            if (peer.Native is Link link)
                link.Owner.Fail(link.Peer, DisconnectReason.RemoteClosed, "LoopbackPeerClosed");
            peer.Native = null;
        }
        protected override void Close() => pending.Clear();
    }
}
