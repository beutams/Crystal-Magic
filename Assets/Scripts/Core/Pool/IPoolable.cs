using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 可池化对象接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 对象从池中取出时调用
        /// </summary>
        void OnGetFromPool();

        /// <summary>
        /// 对象归还池中时调用
        /// </summary>
        void OnReturnToPool();
    }
}
