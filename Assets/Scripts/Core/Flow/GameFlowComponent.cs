using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 游戏流程控制组件
    /// 职责：管理游戏状态机的转移
    /// </summary>
    public class GameFlowComponent : GameComponent<GameFlowComponent>
    {
        private GameState _currentState;
        private Dictionary<Type, GameState> _stateCache = new();

        public override int Priority => 30;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Update()
        {
            _currentState?.OnUpdate();
        }

        /// <summary>
        /// 切换到指定状态（泛型版本），可传入数据
        /// 数据会设置到新状态，在 OnEnter/Update/OnExit 中都可访问
        /// </summary>
        public void SetState<T>(object data = null) where T : GameState, new()
        {
            GameState newState = GetOrCreateState<T>();
            SetState(newState, data);
        }

        /// <summary>
        /// 切换到指定状态，可传入数据
        /// 数据会通过 SetData() 设置到新状态，在 OnEnter/Update/OnExit 中都可访问
        /// </summary>
        private void SetState(GameState newState, object data = null)
        {
            if (_currentState == newState)
                return;

            GameState oldState = _currentState;

            // 退出旧状态
            _currentState?.OnExit();

            // 进入新状态：先设置数据，再调用 OnEnter
            _currentState = newState;
            _currentState.SetData(data);
            _currentState.OnEnter();

            OnStateChanged(oldState, newState);
        }

        /// <summary>
        /// 通过 Type 切换到指定状态，由 TransitionState 内部使用
        /// </summary>
        public void SetState(Type stateType, object data = null)
        {
            if (!_stateCache.ContainsKey(stateType))
            {
                _stateCache[stateType] = Activator.CreateInstance(stateType) as GameState;
            }

            SetState(_stateCache[stateType], data);
        }

        /// <summary>
        /// 获取或创建状态实例
        /// </summary>
        private T GetOrCreateState<T>() where T : GameState, new()
        {
            Type stateType = typeof(T);
            
            if (!_stateCache.ContainsKey(stateType))
            {
                _stateCache[stateType] = new T();
            }

            return (T)_stateCache[stateType];
        }

        /// <summary>
        /// 检查当前是否在某个状态
        /// </summary>
        public bool IsInState<T>() where T : GameState
        {
            return _currentState is T;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public GameState GetCurrentState()
        {
            return _currentState;
        }

        protected virtual void OnStateChanged(GameState oldState, GameState newState)
        {
            string oldName = oldState?.GetType().Name ?? "None";
            string newName = newState?.GetType().Name ?? "None";
            Debug.Log($"[GameFlow] State changed: {oldName} → {newName}");
        }

        public override void Cleanup()
        {
            _currentState?.OnExit();
            _currentState = null;
            
            // 清理所有缓存的状态
            foreach (var state in _stateCache.Values)
            {
                state?.OnExit();
            }
            _stateCache.Clear();
            
            base.Cleanup();
        }
    }
}
