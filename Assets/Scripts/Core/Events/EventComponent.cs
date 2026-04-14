using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 閸忋劌鐪禍瀣╂閹崵鍤庣紒鍕
    /// 閻劋绨憴锝堚偓锔鹃兇缂佺喖妫块惃鍕偓姘繆
    /// 
    /// 鐎电懓绨查妴濠冾攱閺嬫儼顔曠拋鈩冩瀮濡楋絻鈧顑?1.6 閼哄偊绱版禍瀣╂缁撅箑鐣鹃敍鍫Ｐ掗懓锔肩礆
    /// </summary>
    public class EventComponent : GameComponent<EventComponent>
    {
        private Dictionary<System.Type, List<Delegate>> _subscribers = new();
        private Dictionary<string, List<Action<CommonGameEvent>>> _commonEventSubscribers = new();

        public override int Priority => 10;

        /// <summary>
        /// 鐠併垽妲勬禍瀣╂
        /// </summary>
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

        /// <summary>
        /// 閸欐牗绉风拋銏ゆ娴滃娆?
        /// </summary>
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

        /// <summary>
        /// 閸欐垵绔锋禍瀣╂
        /// </summary>
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

        /// <summary>
        /// 濞撳懐鈹栭幍鈧張澶庮吂闂?
        /// </summary>
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
