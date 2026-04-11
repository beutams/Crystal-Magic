using UnityEngine;
using System.Collections.Generic;

namespace CrystalMagic.Core {
    /// <summary>
    /// 对象池接口
    /// </summary>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// 获取对象
        /// </summary>
        T Get();

        /// <summary>
        /// 归还对象
        /// </summary>
        void Return(T obj);

        /// <summary>
        /// 清空池
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取池中对象数量
        /// </summary>
        int Count { get; }
    }
}
