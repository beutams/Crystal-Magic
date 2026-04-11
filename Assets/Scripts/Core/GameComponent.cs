using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 游戏组件基类
    /// 所有管理器继承此类，实现统一的生命周期管理
    /// </summary>
    public abstract class GameComponent<T> : Singleton<T>, IGameComponent where T : GameComponent<T>
    {
        /// <summary>
        /// 初始化优先级（数字越小越先初始化）
        /// 默认为 100，子类可以重写设置不同的优先级
        /// </summary>
        public virtual int Priority => 100;

        /// <summary>
        /// 初始化方法
        /// 由 GameEntry 在启动时调用
        /// </summary>
        public virtual void Initialize()
        {
            Debug.Log($"[{typeof(T).Name}] Initialized");
        }

        /// <summary>
        /// 清理资源
        /// 由 GameEntry 在关闭时调用
        /// </summary>
        public virtual void Cleanup()
        {
            Debug.Log($"[{typeof(T).Name}] Cleaned up");
        }
    }
}
