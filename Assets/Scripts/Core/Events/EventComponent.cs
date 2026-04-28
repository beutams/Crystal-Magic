using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {

    public class EventComponent : GameComponent<EventComponent>
    {
        private Dictionary<System.Type, List<Delegate>> _subscribers = new();
        private Dictionary<string, List<Action<CommonGameEvent>>> _commonEventSubscribers = new();

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

        public void Subscribe(CommonGameEvent gameEvent, Action<CommonGameEvent> handler)
        {
            if (string.IsNullOrEmpty(gameEvent.EventName) || handler == null)
                return;

            AddCommonSubscriber(_commonEventSubscribers, gameEvent.EventName, handler);
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
        }

        public void Unsubscribe(CommonGameEvent gameEvent, Action<CommonGameEvent> handler)
        {
            if (string.IsNullOrEmpty(gameEvent.EventName) || handler == null)
                return;

            RemoveCommonSubscriber(_commonEventSubscribers, gameEvent.EventName, handler);
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
            base.Cleanup();
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
    }
}
