using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 游戏状态基类
    /// </summary>
    public abstract class GameState
    {
        /// <summary>
        /// 状态数据
        /// </summary>
        protected object StateData { get; private set; }

        /// <summary>
        /// 由外部
        /// </summary>
        public void SetData(object data)
        {
            StateData = data;
        }

        /// <summary>
        /// 进入状态
        /// </summary>
        public virtual void OnEnter() { }

        /// <summary>
        /// 离开状态
        /// </summary>
        public virtual void OnExit() { }

        /// <summary>
        /// 状态更新
        /// </summary>
        public virtual void OnUpdate() { }
    }
}
