#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define CRYSTAL_MAGIC_DISABLE_STEAMWORKS
#endif
using System;
using System.Collections.Generic;
#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
using System.Runtime.InteropServices;
using CrystalMagic.Core;
using Steamworks;
#endif

namespace Server
{
    public sealed class SteamP2PTransport : MessageTransport
    {
        private readonly HashSet<ulong> members;
        public SteamP2PTransport(SteamEndpoint listenEndpoint = null, IEnumerable<ulong> allowedMembers = null)
        {
            LocalEndpoint = listenEndpoint;
            members = allowedMembers == null ? new HashSet<ulong>() : new HashSet<ulong>(allowedMembers);
        }
#if !CRYSTAL_MAGIC_DISABLE_STEAMWORKS
        private sealed class SocketPeer
        {
            public HSteamNetConnection Handle;
            public readonly ReliableMessageFragments Fragments = new();
            public int SendOffset;
        }
        private HSteamListenSocket listener;
        private Callback<SteamNetConnectionStatusChangedCallback_t> statusCallback;
        private readonly Dictionary<HSteamNetConnection, Peer> handles = new();
        private readonly Queue<SteamNetConnectionStatusChangedCallback_t> changes = new();
        private readonly IntPtr[] receivePointers = new IntPtr[32];
        private int sendBudget;

        protected override void Open()
        {
            if (!SteamComponent.Instance.IsInitialized)
                throw new InvalidOperationException("Steam is unavailable.");
            SteamNetworkingUtils.InitRelayNetworkAccess();
            statusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatus);
            if (LocalEndpoint is SteamEndpoint endpoint)
            {
                if (endpoint.SteamId != SteamComponent.Instance.SteamId || members.Count == 0 || members.Count > 8)
                    throw new ArgumentException("Invalid Steam host roster.");
                listener = SteamNetworkingSockets.CreateListenSocketP2P(endpoint.VirtualPort, 0, null);
                if (listener == HSteamListenSocket.Invalid)
                    throw new InvalidOperationException("Steam P2P listen failed.");
            }
        }

        private void OnStatus(SteamNetConnectionStatusChangedCallback_t change)
        {
            if (!active) return;
            bool incoming = listener != HSteamListenSocket.Invalid && change.m_info.m_hListenSocket == listener;
            if (!incoming && !handles.ContainsKey(change.m_hConn)) return;
            // Steam 回调仅记录状态；所有业务回调在本传输的 Update 中运行。
            if (changes.Count >= 128)
            {
                SteamNetworkingSockets.CloseConnection(change.m_hConn, 0, "Status queue limit", false);
                if (handles.TryGetValue(change.m_hConn, out Peer peer))
                    Fail(peer, DisconnectReason.ReceiveError, "StatusQueueLimit");
                return;
            }
            changes.Enqueue(change);
        }

        protected override void BeginConnect(Peer peer)
        {
            if (peer.RemoteEndpoint is not SteamEndpoint endpoint || endpoint.SteamId == 0)
                throw new ArgumentException("Steam P2P requires a Steam endpoint.");
            SteamNetworkingIdentity identity = new();
            identity.SetSteamID64(endpoint.SteamId);
            HSteamNetConnection handle = SteamNetworkingSockets.ConnectP2P(ref identity, endpoint.VirtualPort, 0, null);
            if (handle == HSteamNetConnection.Invalid)
            { Fail(peer, DisconnectReason.ConnectFailed, "SteamConnect"); return; }
            peer.Native = new SocketPeer { Handle = handle };
            handles.Add(handle, peer);
        }

        protected override void Poll()
        {
            sendBudget = 64;
            List<Peer> terminal = new();
            while (changes.Count > 0)
            {
                SteamNetConnectionStatusChangedCallback_t change = changes.Dequeue();
                if (!handles.TryGetValue(change.m_hConn, out Peer peer))
                {
                    if (change.m_info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
                        continue;
                    ulong account = change.m_info.m_identityRemote.GetSteamID64();
                    bool duplicate = false;
                    foreach (Peer existing in handles.Values)
                        if (existing.RemoteEndpoint is SteamEndpoint known && known.SteamId == account &&
                            existing.Connection.State != ConnectState.Close) duplicate = true;
                    if (!members.Contains(account) || duplicate || handles.Count >= members.Count ||
                        SteamNetworkingSockets.AcceptConnection(change.m_hConn) != EResult.k_EResultOK)
                    {
                        SteamNetworkingSockets.CloseConnection(change.m_hConn, 0, "Not an expected battle member", false);
                        continue;
                    }
                    peer = AddPeer(new SteamEndpoint(account, ((SteamEndpoint)LocalEndpoint).VirtualPort), true);
                    peer.Native = new SocketPeer { Handle = change.m_hConn };
                    handles.Add(change.m_hConn, peer);
                }
                if (change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                    peer.Ready = true;
                else if (change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer ||
                         change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
                    terminal.Add(peer);
            }
            foreach (Peer peer in new List<Peer>(handles.Values))
            {
                SocketPeer socket = (SocketPeer)peer.Native;
                if (peer.Connection.State == ConnectState.Close) continue;
                if (socket.Fragments.IsExpired(NetworkTimer.Instance.TimeNow))
                { Fail(peer, DisconnectReason.Timeout, "FragmentTimeout"); continue; }
                int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(socket.Handle, receivePointers, receivePointers.Length);
                if (count < 0) { Fail(peer, DisconnectReason.ReceiveError, "SteamReceive"); continue; }
                for (int i = 0; i < count; i++)
                {
                    IntPtr pointer = receivePointers[i];
                    try
                    {
                        SteamNetworkingMessage_t message = SteamNetworkingMessage_t.FromIntPtr(pointer);
                        if (message.m_cbSize <= ReliableMessageFragments.HeaderLength ||
                            message.m_cbSize > ReliableMessageFragments.FragmentSize ||
                            (message.m_nFlags & Constants.k_nSteamNetworkingSend_Reliable) == 0)
                        { Fail(peer, DisconnectReason.InvalidPacket, "SteamFragmentSize"); continue; }
                        byte[] fragment = new byte[message.m_cbSize];
                        Marshal.Copy(message.m_pData, fragment, 0, fragment.Length);
                        if (!socket.Fragments.TryReceive(fragment, NetworkTimer.Instance.TimeNow, out byte[] packet))
                            Fail(peer, DisconnectReason.InvalidPacket, "SteamFragmentSequence");
                        else if (packet != null) EnqueueIncoming(peer, packet);
                    }
                    catch (Exception exception) { Fail(peer, DisconnectReason.ReceiveError, "SteamMessage", exception); }
                    finally { SteamNetworkingMessage_t.Release(pointer); receivePointers[i] = IntPtr.Zero; }
                }
            }
            // 可靠数据在对端关闭前可能已到达；先交给 Tick 处理，再在下一次 Poll 断开。
            foreach (Peer peer in terminal)
            {
                if (peer.Incoming.Count == 0) Fail(peer, DisconnectReason.RemoteClosed, "SteamPeerClosed");
                else peer.DrainDeadline = NetworkTimer.Instance.TimeNow + ServerUtility.DrainTimeout;
            }
        }

        protected override bool TrySendPacket(Peer peer, byte[] packet)
        {
            SocketPeer socket = (SocketPeer)peer.Native;
            while (socket.SendOffset < packet.Length && sendBudget-- > 0)
            {
                byte[] fragment = ReliableMessageFragments.Pack(packet, socket.SendOffset);
                GCHandle pinned = GCHandle.Alloc(fragment, GCHandleType.Pinned);
                EResult result;
                try
                {
                    result = SteamNetworkingSockets.SendMessageToConnection(socket.Handle,
                        pinned.AddrOfPinnedObject(), (uint)fragment.Length,
                        Constants.k_nSteamNetworkingSend_ReliableNoNagle, out _);
                }
                finally { pinned.Free(); }
                if (result == EResult.k_EResultLimitExceeded) return false;
                if (result != EResult.k_EResultOK)
                { Fail(peer, DisconnectReason.SendError, "SteamSend:" + result); return false; }
                socket.SendOffset += fragment.Length - ReliableMessageFragments.HeaderLength;
            }
            if (socket.SendOffset != packet.Length) return false;
            socket.SendOffset = 0;
            return true;
        }

        protected override bool IsNativeDrained(Peer peer)
        {
            SteamNetConnectionRealTimeStatus_t status = default;
            SteamNetConnectionRealTimeLaneStatus_t lane = default;
            return peer.Native is SocketPeer socket &&
                SteamNetworkingSockets.GetConnectionRealTimeStatus(socket.Handle, ref status, 0, ref lane) == EResult.k_EResultOK &&
                status.m_cbPendingReliable == 0 && status.m_cbSentUnackedReliable == 0;
        }

        protected override void ClosePeer(Peer peer)
        {
            if (peer.Native is not SocketPeer socket) return;
            handles.Remove(socket.Handle);
            socket.Fragments.Clear();
            SteamNetworkingSockets.CloseConnection(socket.Handle, 0, "Battle connection closed", false);
            peer.Native = null;
        }

        protected override void Close()
        {
            statusCallback?.Dispose();
            statusCallback = null;
            changes.Clear();
            HSteamListenSocket previous = listener;
            listener = HSteamListenSocket.Invalid;
            if (previous != HSteamListenSocket.Invalid)
                SteamNetworkingSockets.CloseListenSocket(previous);
        }
#else
        protected override void Open() => throw new PlatformNotSupportedException("Steam networking is not available on this platform.");
        protected override void BeginConnect(Peer peer) { }
        protected override void Poll() { }
        protected override bool TrySendPacket(Peer peer, byte[] packet) => false;
        protected override void ClosePeer(Peer peer) { }
        protected override void Close() { }
#endif
    }
}
