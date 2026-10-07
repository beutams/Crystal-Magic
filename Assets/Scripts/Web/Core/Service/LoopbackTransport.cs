using System;
using System.Collections.Generic;

namespace Server
{
    /// <summary>本机帧状态经过序列化后立即入对端帧队列；控制消息仍由 Update 派发。</summary>
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
        private bool deliveringBattleFrame;
        private LoopbackTransport(NetworkEndpoint identity) => this.identity = identity;

        // Both peers run in the same Unity main loop. A loading/compilation stall
        // pauses both queues; elapsed silence cannot indicate a dead local peer.
        // Shutdown and explicit disconnect still propagate through ClosePeer.
        protected override bool UsesHeartbeatTimeout => false;

        public static void CreatePair(ulong hostId, out LoopbackTransport server, out LoopbackTransport client)
        {
            server = new LoopbackTransport(new LoopbackEndpoint(hostId));
            client = new LoopbackTransport(new LoopbackEndpoint(hostId));
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
                incoming.ImmediateBattleFrameSender = packet => TrySendBattleFrame(incoming, packet);
                outgoing.ImmediateBattleFrameSender = packet => remote.TrySendBattleFrame(outgoing, packet);
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

        private bool TrySendBattleFrame(Peer peer, byte[] packet)
        {
            // Never overtake queued control messages, send drains or a reentrant
            // handler. Immediate delivery only fills the other FrameManager queue.
            if (!active || deliveringBattleFrame || peer.Connection.State != ConnectState.Connected ||
                peer.DrainDeadline != 0 || peer.Outgoing.Count != 0 ||
                peer.Native is not Link link || !link.Owner.active ||
                link.Owner.deliveringBattleFrame || link.Peer.Connection.State != ConnectState.Connected ||
                link.Peer.Incoming.Count != 0)
                return false;

            deliveringBattleFrame = true;
            try
            {
                if (!link.Peer.Connection.TryReceive(packet, out DisconnectInfo failure))
                {
                    link.Peer.Connection.LastDisconnectInfo = failure;
                    link.Owner.Fail(link.Peer, failure.Reason, failure.Phase, failure.Exception);
                }
                else
                    link.Owner.NotifyImmediateReceive(link.Peer.Connection);
                NotifyImmediateSend(peer.Connection);
                return true;
            }
            finally { deliveringBattleFrame = false; }
        }
        protected override void ClosePeer(Peer peer)
        {
            if (peer.Native is Link link)
                link.Owner.Fail(link.Peer, DisconnectReason.RemoteClosed, "LoopbackPeerClosed");
            peer.Native = null;
        }
        protected override void Close() => pending.Clear();
    }

    public sealed class LoopbackEndpoint : NetworkEndpoint
    {
        public ulong AccountId { get; }
        public LoopbackEndpoint(ulong accountId) => AccountId = accountId;
        public override string ToString() => "local:" + AccountId;
    }
}
