using UnityEngine;
using System.Collections.Generic;

namespace CrystalMagic.Core {
    /// <summary>
    /// 资源管理组件
    /// </summary>
    public class ResourceComponent : GameComponent<ResourceComponent>
    {
        [SerializeField] private ResourceLoadMode _loadMode = ResourceLoadMode.Editor;

        private IResourceLoader _loader;
        private readonly Dictionary<string, Object> _pathToResource = new();
        private readonly Dictionary<Object, int> _resourceRefCounts = new();
        private readonly Dictionary<string, Dictionary<Object, int>> _ownerResourceRefCounts = new();

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
            if (_loader == null || string.IsNullOrWhiteSpace(path))
                return null;

            if (_pathToResource.TryGetValue(path, out Object cachedResource))
            {
                if (cachedResource is not T typedCachedResource)
                {
                    Debug.LogError($"[ResourceComponent] Cached resource type mismatch for '{path}'. Requested '{typeof(T).Name}', cached '{cachedResource.GetType().Name}'.");
                    return null;
                }

                AddReference(typedCachedResource);
                return typedCachedResource;
            }

            T resource = _loader.Load<T>(path);
            if (resource != null)
            {
                TrackLoadedResource(path, resource);
            }

            return resource;
        }

        public T Load<T>(string path, string ownerKey) where T : Object
        {
            T resource = Load<T>(path);
            TrackOwnerReference(ownerKey, resource);
            return resource;
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public void LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object
        {
            if (_loader == null || string.IsNullOrWhiteSpace(path))
            {
                onComplete?.Invoke(null);
                return;
            }

            if (_pathToResource.TryGetValue(path, out Object cachedResource))
            {
                if (cachedResource is not T typedCachedResource)
                {
                    Debug.LogError($"[ResourceComponent] Cached resource type mismatch for '{path}'. Requested '{typeof(T).Name}', cached '{cachedResource.GetType().Name}'.");
                    onComplete?.Invoke(null);
                    return;
                }

                AddReference(typedCachedResource);
                onComplete?.Invoke(typedCachedResource);
                return;
            }

            StartCoroutine(_loader.LoadAsync<T>(path, resource =>
            {
                if (resource != null)
                {
                    TrackLoadedResource(path, resource);
                }
                onComplete?.Invoke(resource);
            }));
        }

        public void LoadAsync<T>(string path, string ownerKey, System.Action<T> onComplete) where T : Object
        {
            LoadAsync<T>(path, resource =>
            {
                TrackOwnerReference(ownerKey, resource);
                onComplete?.Invoke(resource);
            });
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(Object resource)
        {
            if (resource == null || !_resourceRefCounts.TryGetValue(resource, out int refCount))
                return;

            refCount--;
            if (refCount > 0)
            {
                _resourceRefCounts[resource] = refCount;
                return;
            }

            _loader?.Unload(resource);
            _resourceRefCounts.Remove(resource);
            RemoveResourcePaths(resource);
        }

        public void Unload(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !_pathToResource.TryGetValue(path, out Object resource))
                return;

            Unload(resource);
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        public void UnloadAll()
        {
            _loader?.UnloadAll();
            _pathToResource.Clear();
            _resourceRefCounts.Clear();
            _ownerResourceRefCounts.Clear();
        }

        public void ReleaseOwner(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)
                || !_ownerResourceRefCounts.TryGetValue(ownerKey, out Dictionary<Object, int> ownedResources))
            {
                return;
            }

            List<KeyValuePair<Object, int>> ownedEntries = new(ownedResources);
            _ownerResourceRefCounts.Remove(ownerKey);

            for (int i = 0; i < ownedEntries.Count; i++)
            {
                KeyValuePair<Object, int> entry = ownedEntries[i];
                for (int j = 0; j < entry.Value; j++)
                {
                    Unload(entry.Key);
                }
            }
        }

        public override void Cleanup()
        {
            UnloadAll();
            base.Cleanup();
        }

        public int GetReferenceCount(Object resource)
        {
            return resource != null && _resourceRefCounts.TryGetValue(resource, out int refCount) ? refCount : 0;
        }

        public int GetReferenceCount(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && _pathToResource.TryGetValue(path, out Object resource)
                ? GetReferenceCount(resource)
                : 0;
        }

        private void TrackLoadedResource(string path, Object resource)
        {
            if (_pathToResource.TryGetValue(path, out Object existingResource))
            {
                if (existingResource == resource)
                {
                    AddReference(resource);
                    return;
                }

                Debug.LogWarning($"[ResourceComponent] Resource path '{path}' was remapped from '{existingResource.name}' to '{resource.name}'.");
            }

            _pathToResource[path] = resource;
            AddReference(resource);
        }

        private void AddReference(Object resource)
        {
            if (_resourceRefCounts.TryGetValue(resource, out int refCount))
            {
                _resourceRefCounts[resource] = refCount + 1;
                return;
            }

            _resourceRefCounts[resource] = 1;
        }

        private void RemoveResourcePaths(Object resource)
        {
            List<string> pathsToRemove = null;
            foreach (KeyValuePair<string, Object> pair in _pathToResource)
            {
                if (pair.Value != resource)
                    continue;

                pathsToRemove ??= new List<string>();
                pathsToRemove.Add(pair.Key);
            }

            if (pathsToRemove == null)
                return;

            for (int i = 0; i < pathsToRemove.Count; i++)
            {
                _pathToResource.Remove(pathsToRemove[i]);
            }
        }

        private void TrackOwnerReference(string ownerKey, Object resource)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || resource == null)
                return;

            if (!_ownerResourceRefCounts.TryGetValue(ownerKey, out Dictionary<Object, int> ownedResources))
            {
                ownedResources = new Dictionary<Object, int>();
                _ownerResourceRefCounts[ownerKey] = ownedResources;
            }

            if (ownedResources.TryGetValue(resource, out int refCount))
            {
                ownedResources[resource] = refCount + 1;
                return;
            }

            ownedResources[resource] = 1;
        }
    }
}
