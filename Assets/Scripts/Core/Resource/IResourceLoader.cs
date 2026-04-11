using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 资源加载器接口
    /// </summary>
    public interface IResourceLoader
    {
        /// <summary>
        /// 加载资源
        /// </summary>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载资源
        /// </summary>
        System.Collections.IEnumerator LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object;

        /// <summary>
        /// 卸载资源
        /// </summary>
        void Unload(Object resource);

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        void UnloadAll();
    }
}
