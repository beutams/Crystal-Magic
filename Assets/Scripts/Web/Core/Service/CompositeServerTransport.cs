using System;
using System.Collections.Generic;

namespace Server
{
    /// <summary>同一权威房间同时接受本机队列、TCP 和 Steam 连接；所有监听成功后才报告就绪。</summary>
    public sealed class CompositeServerTransport : IServerTransport
    {
        private readonly IServerTransport[] children;
        private readonly Dictionary<Connect, IServerTransport> owners = new();
        private bool active;
        public CompositeServerTransport(params IServerTransport[] children)
        {
            if (children == null || children.Length == 0 || Array.Exists(children, child => child == null))
                throw new ArgumentException("At least one valid server transport is required.", nameof(children));
            this.children = children;
        }
        public NetworkEndpoint LocalEndpoint => children[0].LocalEndpoint;
        public event Action OnListening;
        public event Action OnListeningFail;
        public event Action<Connect> OnAccept;
        public event Action<Connect> OnSend;
        public event Action<Connect> OnRecv;
        public event Action<Connect> OnDisconnected;
        public void Init()
        {
            if (active) return;
            active = true;
            try
            {
                foreach (IServerTransport child in children)
                {
                    child.OnAccept += connect => { owners[connect] = child; OnAccept?.Invoke(connect); };
                    child.OnDisconnected += connect => { owners.Remove(connect); OnDisconnected?.Invoke(connect); };
                    child.OnSend += connect => OnSend?.Invoke(connect);
                    child.OnRecv += connect => OnRecv?.Invoke(connect);
                    bool listening = false;
                    bool failed = false;
                    Action onListening = () => listening = true;
                    Action onFailure = () => failed = true;
                    child.OnListening += onListening;
                    child.OnListeningFail += onFailure;
                    try
                    {
                        child.Init();
                        if (!active || failed || !listening)
                            throw new InvalidOperationException("A battle listener failed to start.");
                    }
                    finally
                    {
                        child.OnListening -= onListening;
                        child.OnListeningFail -= onFailure;
                    }
                }
                OnListening?.Invoke();
            }
            catch
            {
                Action failed = OnListeningFail;
                Shutdown();
                failed?.Invoke();
                throw;
            }
        }
        public void Update()
        {
            foreach (IServerTransport child in children)
            {
                if (!active) break;
                child.Update();
            }
        }
        public void Disconnect(Connect connect)
        {
            if (connect != null && owners.TryGetValue(connect, out IServerTransport child)) child.Disconnect(connect);
        }
        public void DisconnectAfterSend(Connect connect)
        {
            if (connect != null && owners.TryGetValue(connect, out IServerTransport child)) child.DisconnectAfterSend(connect);
        }
        public void Shutdown()
        {
            active = false;
            foreach (IServerTransport child in children)
                try { child.Shutdown(); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            owners.Clear();
            OnListening = OnListeningFail = null;
            OnAccept = OnSend = OnRecv = OnDisconnected = null;
        }
    }
}
