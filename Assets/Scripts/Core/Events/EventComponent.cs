using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {

    public class EventComponent : GameComponent<EventComponent>
    {
        private Dictionary<System.Type, List<Delegate>> _subscribers = new();
        private Dictionary<string, List<Action<CommonGameEvent>>> _commonEventSubscribers = new();
        private Dictionary<string, List<OwnedSubscription>> _ownedSubscriptions = new();

        public override int Priority => 10;

        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
                return;

            System.Type eventType = typeof(T);
            if (!_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType] = new List<Delegate>();
            }

            _subscribers[eventType].Add(handler);
        }

        public void Subscribe<T>(string ownerKey, Action<T> handler) where T : IGameEvent
        {
            Subscribe(handler);
            TrackOwnedSubscription(ownerKey, typeof(T), null, handler);
        }

        public void Subscribe(CommonGameEvent gameEvent, Action<CommonGameEvent> handler)
        {
            if (string.IsNullOrEmpty(gameEvent.EventName) || handler == null)
                return;

            AddCommonSubscriber(_commonEventSubscribers, gameEvent.EventName, handler);
        }

        public void Subscribe(string ownerKey, CommonGameEvent gameEvent, Action<CommonGameEvent> handler)
        {
            Subscribe(gameEvent, handler);
            TrackOwnedSubscription(ownerKey, null, gameEvent.EventName, handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
                return;

            System.Type eventType = typeof(T);
            if (_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType].Remove(handler);
            }

            RemoveOwnedSubscription(eventType, null, handler);
        }

        public void Unsubscribe(CommonGameEvent gameEvent, Action<CommonGameEvent> handler)
        {
            if (string.IsNullOrEmpty(gameEvent.EventName) || handler == null)
                return;

            RemoveCommonSubscriber(_commonEventSubscribers, gameEvent.EventName, handler);
            RemoveOwnedSubscription(null, gameEvent.EventName, handler);
        }

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            System.Type eventType = typeof(T);
            if (_subscribers.TryGetValue(eventType, out List<Delegate> handlers))
            {
                Delegate[] handlersSnapshot = handlers.ToArray();

                foreach (Delegate handler in handlersSnapshot)
                {
                    if (handler is Action<T> typedHandler)
                    {
                        try
                        {
                            typedHandler.Invoke(gameEvent);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"Error publishing event {eventType.Name}: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
            }
        }

        public void Publish(CommonGameEvent gameEvent)
        {
            PublishCommonEvent(_commonEventSubscribers, gameEvent.EventName, gameEvent);
        }

        public override void Cleanup()
        {
            _subscribers.Clear();
            _commonEventSubscribers.Clear();
            _ownedSubscriptions.Clear();
            base.Cleanup();
        }

        public void ReleaseOwner(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || !_ownedSubscriptions.TryGetValue(ownerKey, out List<OwnedSubscription> subscriptions))
                return;

            OwnedSubscription[] snapshot = subscriptions.ToArray();
            _ownedSubscriptions.Remove(ownerKey);

            foreach (OwnedSubscription subscription in snapshot)
            {
                if (subscription.EventType != null)
                {
                    RemoveTypedSubscriber(subscription.EventType, subscription.Handler);
                    continue;
                }

                if (!string.IsNullOrEmpty(subscription.CommonEventName) && subscription.Handler is Action<CommonGameEvent> commonHandler)
                {
                    RemoveCommonSubscriber(_commonEventSubscribers, subscription.CommonEventName, commonHandler);
                }
            }
        }

        private static void AddCommonSubscriber<TEvent>(
            Dictionary<string, List<Action<TEvent>>> subscribers,
            string eventName,
            Action<TEvent> handler)
        {
            if (!subscribers.TryGetValue(eventName, out List<Action<TEvent>> handlers))
            {
                handlers = new List<Action<TEvent>>();
                subscribers[eventName] = handlers;
            }

            handlers.Add(handler);
        }

        private static void RemoveCommonSubscriber<TEvent>(
            Dictionary<string, List<Action<TEvent>>> subscribers,
            string eventName,
            Action<TEvent> handler)
        {
            if (!subscribers.TryGetValue(eventName, out List<Action<TEvent>> handlers))
                return;

            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                subscribers.Remove(eventName);
            }
        }

        private static void PublishCommonEvent<TEvent>(
            Dictionary<string, List<Action<TEvent>>> subscribers,
            string eventName,
            TEvent gameEvent)
        {
            if (string.IsNullOrEmpty(eventName))
                return;

            if (!subscribers.TryGetValue(eventName, out List<Action<TEvent>> handlers))
                return;

            Action<TEvent>[] handlersSnapshot = handlers.ToArray();
            foreach (Action<TEvent> handler in handlersSnapshot)
            {
                try
                {
                    handler.Invoke(gameEvent);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error publishing common event {eventName}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        private void TrackOwnedSubscription(string ownerKey, Type eventType, string commonEventName, Delegate handler)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || handler == null)
                return;

            if (!_ownedSubscriptions.TryGetValue(ownerKey, out List<OwnedSubscription> subscriptions))
            {
                subscriptions = new List<OwnedSubscription>();
                _ownedSubscriptions[ownerKey] = subscriptions;
            }

            subscriptions.Add(new OwnedSubscription
            {
                EventType = eventType,
                CommonEventName = commonEventName,
                Handler = handler,
            });
        }

        private void RemoveOwnedSubscription(Type eventType, string commonEventName, Delegate handler)
        {
            if (handler == null || _ownedSubscriptions.Count == 0)
                return;

            List<string> emptyOwners = null;
            foreach (KeyValuePair<string, List<OwnedSubscription>> pair in _ownedSubscriptions)
            {
                List<OwnedSubscription> subscriptions = pair.Value;
                subscriptions.RemoveAll(subscription =>
                    subscription.EventType == eventType
                    && subscription.CommonEventName == commonEventName
                    && subscription.Handler == handler);

                if (subscriptions.Count > 0)
                    continue;

                emptyOwners ??= new List<string>();
                emptyOwners.Add(pair.Key);
            }

            if (emptyOwners == null)
                return;

            for (int i = 0; i < emptyOwners.Count; i++)
            {
                _ownedSubscriptions.Remove(emptyOwners[i]);
            }
        }

        private void RemoveTypedSubscriber(Type eventType, Delegate handler)
        {
            if (eventType == null || handler == null)
                return;

            if (_subscribers.TryGetValue(eventType, out List<Delegate> handlers))
            {
                handlers.Remove(handler);
            }
        }

        private sealed class OwnedSubscription
        {
            public Type EventType { get; set; }
            public string CommonEventName { get; set; }
            public Delegate Handler { get; set; }
        }
    }
}
