using UnityEngine;
using System.Collections.Generic;

namespace CrystalMagic.Core {
    /// <summary>
    /// 资源管理组件
    /// 职责：统一管理资源加载、卸载
    /// 支持编辑器模式和 AB 包模式
    /// </summary>
    public class ResourceComponent : GameComponent<ResourceComponent>
    {
        [SerializeField] private ResourceLoadMode _loadMode = ResourceLoadMode.Editor;

        private IResourceLoader _loader;
        private HashSet<Object> _loadedResources = new();

        public override int Priority => 5;

        public ResourceLoadMode LoadMode => _loadMode;

        public override void Initialize()
        {
            base.Initialize();
            InitializeLoader();
        }

        /// <summary>
        /// 初始化加载器
        /// </summary>
        private void InitializeLoader()
        {
            switch (_loadMode)
            {
                case ResourceLoadMode.Editor:
                    _loader = new EditorResourceLoader();
                    Debug.Log("[ResourceComponent] Using EditorResourceLoader");
                    break;

                case ResourceLoadMode.AssetBundle:
                    // 后续实现
                    _loader = new EditorResourceLoader();
                    Debug.LogWarning("[ResourceComponent] AssetBundle mode not implemented yet, using EditorResourceLoader");
                    break;

                default:
                    _loader = new EditorResourceLoader();
                    break;
            }
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            if (_loader == null)
                return null;

            T resource = _loader.Load<T>(path);
            if (resource != null)
            {
                _loadedResources.Add(resource);
            }

            return resource;
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public void LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object
        {
            if (_loader == null)
            {
                onComplete?.Invoke(null);
                return;
            }

            StartCoroutine(_loader.LoadAsync<T>(path, resource =>
            {
                if (resource != null)
                {
                    _loadedResources.Add(resource);
                }
                onComplete?.Invoke(resource);
            }));
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(Object resource)
        {
            if (resource != null)
            {
                _loader?.Unload(resource);
                _loadedResources.Remove(resource);
            }
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        public void UnloadAll()
        {
            _loader?.UnloadAll();
            _loadedResources.Clear();
        }

        public override void Cleanup()
        {
            UnloadAll();
            base.Cleanup();
        }
    }
}
