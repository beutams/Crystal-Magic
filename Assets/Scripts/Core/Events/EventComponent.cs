using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 全局事件总线组件
    /// 用于解耦系统间的通信
    /// 
    /// 对应《框架设计文档》第 1.6 节：事件约定（解耦）
    /// </summary>
    public class EventComponent : GameComponent<EventComponent>
    {
        private Dictionary<System.Type, List<Delegate>> _subscribers = new();

        public override int Priority => 10;

        /// <summary>
        /// 订阅事件
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

        /// <summary>
        /// 取消订阅事件
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

        /// <summary>
        /// 发布事件
        /// </summary>
        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            System.Type eventType = typeof(T);
            if (_subscribers.ContainsKey(eventType))
            {
                foreach (var handler in _subscribers[eventType])
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

        /// <summary>
        /// 清空所有订阅
        /// </summary>
        public override void Cleanup()
        {
            _subscribers.Clear();
            base.Cleanup();
        }
    }
}
