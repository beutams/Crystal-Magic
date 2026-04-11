namespace CrystalMagic.Core
{
    /// <summary>
    /// 游戏组件接口
    /// 所有管理器都实现此接口，由 GameEntry 统一管理生命周期
    /// 
    /// 对应《框架设计文档》第 1.1 节：模块化管理
    /// </summary>
    public interface IGameComponent
    {
        /// <summary>
        /// 初始化优先级（数字越小越先初始化）
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize();

        /// <summary>
        /// 清理资源
        /// </summary>
        void Cleanup();
    }
}
