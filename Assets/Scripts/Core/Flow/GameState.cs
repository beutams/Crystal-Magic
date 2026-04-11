using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 游戏状态基类
    /// 所有状态继承此类，内部保留状态数据
    /// </summary>
    public abstract class GameState
    {
        /// <summary>
        /// 状态数据，由 GameFlowComponent 通过 SetData() 设置
        /// OnEnter/Update/OnExit 都可访问
        /// </summary>
        protected object StateData { get; private set; }

        /// <summary>
        /// 由外部（GameFlowComponent）调用以设置状态数据
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
