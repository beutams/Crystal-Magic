using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    public class ResourceComponent : GameComponent<ResourceComponent>
    {
        [SerializeField] private ResourceLoadMode _loadMode = ResourceLoadMode.Editor;
        [SerializeField] private string _assetBundleRootFolderName = "AssetBundles";
        [SerializeField] private string _catalogBundleName = "catalog";
        [SerializeField] private string _catalogAssetName = "BundleCatalogAsset";

        private IResourceLoader _loader;
        private readonly Dictionary<string, Object> _pathToResource = new();
        private readonly Dictionary<int, HashSet<string>> _resourceInstanceToPaths = new();
        private readonly Dictionary<string, int> _pathRefCounts = new();
        private readonly Dictionary<string, Dictionary<string, int>> _ownerPathRefCounts = new();

        public override int Priority => 5;

        public ResourceLoadMode LoadMode => _loadMode;
        public string AssetBundleRootFolderName => _assetBundleRootFolderName;
        public string CatalogBundleName => _catalogBundleName;
        public string CatalogAssetName => _catalogAssetName;

        public override void Initialize()
        {
            base.Initialize();
            InitializeLoader();
        }

        private void InitializeLoader()
        {
            switch (_loadMode)
            {
                case ResourceLoadMode.Editor:
                    _loader = new EditorResourceLoader();
                    Debug.Log("[ResourceComponent] Using EditorResourceLoader");
                    break;

                case ResourceLoadMode.AssetBundle:
                    _loader = new AssetBundleResourceLoader(_assetBundleRootFolderName, _catalogBundleName, _catalogAssetName);
                    Debug.Log("[ResourceComponent] Using AssetBundleResourceLoader");
                    break;

                default:
                    _loader = new EditorResourceLoader();
                    Debug.LogWarning("[ResourceComponent] Unknown load mode, fallback to EditorResourceLoader");
                    break;
            }

            _loader.Initialize();
        }

        public T Load<T>(string path) where T : Object
        {
            if (_loader == null || string.IsNullOrWhiteSpace(path))
                return null;

            T resource = _loader.Load<T>(path);
            if (resource == null)
                return null;

            TrackLoadedResource(path, resource);
            AddPathReference(path);
            return resource;
        }

        public Sprite LoadSprite(string path)
        {
            if (_loader == null || string.IsNullOrWhiteSpace(path))
                return null;

            if (!AssetBundlePlatformUtility.TrySplitSubAssetPath(path, out string assetPath, out string spriteName))
                return Load<Sprite>(path);

            Sprite sprite = _loader.LoadSprite(assetPath, spriteName);
            if (sprite == null)
                return null;

            TrackLoadedResource(path, sprite);
            AddPathReference(path);
            return sprite;
        }

        public T Load<T>(string path, string ownerKey) where T : Object
        {
            T resource = Load<T>(path);
            TrackOwnerReference(ownerKey, path);
            return resource;
        }

        public Sprite LoadSprite(string path, string ownerKey)
        {
            Sprite sprite = LoadSprite(path);
            TrackOwnerReference(ownerKey, path);
            return sprite;
        }

        public void LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object
        {
            if (_loader == null || string.IsNullOrWhiteSpace(path))
            {
                onComplete?.Invoke(null);
                return;
            }

            StartCoroutine(_loader.LoadAsync<T>(path, resource =>
            {
                if (resource != null)
                {
                    TrackLoadedResource(path, resource);
                    AddPathReference(path);
                }

                onComplete?.Invoke(resource);
            }));
        }

        public void LoadAsync<T>(string path, string ownerKey, System.Action<T> onComplete) where T : Object
        {
            LoadAsync<T>(path, resource =>
            {
                TrackOwnerReference(ownerKey, path);
                onComplete?.Invoke(resource);
            });
        }

        public void Unload(Object resource)
        {
            if (resource == null)
                return;

            if (!_resourceInstanceToPaths.TryGetValue(resource.GetInstanceID(), out HashSet<string> paths) || paths.Count == 0)
                return;

            foreach (string path in paths)
            {
                Unload(path);
                return;
            }
        }

        public void Unload(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !_pathRefCounts.TryGetValue(path, out int refCount))
                return;

            refCount--;
            if (refCount > 0)
            {
                _pathRefCounts[path] = refCount;
                return;
            }

            _pathRefCounts.Remove(path);
            _loader?.Release(path);
            RemoveTrackedResource(path);
        }

        public void UnloadAll()
        {
            _loader?.ReleaseAll();
            _pathToResource.Clear();
            _resourceInstanceToPaths.Clear();
            _pathRefCounts.Clear();
            _ownerPathRefCounts.Clear();
        }

        public void ReleaseOwner(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)
                || !_ownerPathRefCounts.TryGetValue(ownerKey, out Dictionary<string, int> ownedPaths))
            {
                return;
            }

            List<KeyValuePair<string, int>> ownedEntries = new(ownedPaths);
            _ownerPathRefCounts.Remove(ownerKey);

            for (int i = 0; i < ownedEntries.Count; i++)
            {
                KeyValuePair<string, int> entry = ownedEntries[i];
                for (int j = 0; j < entry.Value; j++)
                {
                    Unload(entry.Key);
                }
            }
        }

        public int GetReferenceCount(Object resource)
        {
            if (resource == null || !_resourceInstanceToPaths.TryGetValue(resource.GetInstanceID(), out HashSet<string> paths))
                return 0;

            int totalCount = 0;
            foreach (string path in paths)
            {
                totalCount += GetReferenceCount(path);
            }

            return totalCount;
        }

        public int GetReferenceCount(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && _pathRefCounts.TryGetValue(path, out int refCount)
                ? refCount
                : 0;
        }

        public override void Cleanup()
        {
            UnloadAll();
            base.Cleanup();
        }

        private void TrackLoadedResource(string path, Object resource)
        {
            if (_pathToResource.TryGetValue(path, out Object existingResource) && existingResource != resource)
            {
                RemovePathMapping(existingResource, path);
            }

            _pathToResource[path] = resource;

            int instanceId = resource.GetInstanceID();
            if (!_resourceInstanceToPaths.TryGetValue(instanceId, out HashSet<string> paths))
            {
                paths = new HashSet<string>();
                _resourceInstanceToPaths[instanceId] = paths;
            }

            paths.Add(path);
        }

        private void AddPathReference(string path)
        {
            if (_pathRefCounts.TryGetValue(path, out int refCount))
            {
                _pathRefCounts[path] = refCount + 1;
                return;
            }

            _pathRefCounts[path] = 1;
        }

        private void TrackOwnerReference(string ownerKey, string path)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || string.IsNullOrWhiteSpace(path))
                return;

            if (!_ownerPathRefCounts.TryGetValue(ownerKey, out Dictionary<string, int> ownedPaths))
            {
                ownedPaths = new Dictionary<string, int>();
                _ownerPathRefCounts[ownerKey] = ownedPaths;
            }

            if (ownedPaths.TryGetValue(path, out int refCount))
            {
                ownedPaths[path] = refCount + 1;
                return;
            }

            ownedPaths[path] = 1;
        }

        private void RemoveTrackedResource(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !_pathToResource.TryGetValue(path, out Object resource))
                return;

            _pathToResource.Remove(path);
            RemovePathMapping(resource, path);
        }

        private void RemovePathMapping(Object resource, string path)
        {
            if (resource == null)
                return;

            int instanceId = resource.GetInstanceID();
            if (!_resourceInstanceToPaths.TryGetValue(instanceId, out HashSet<string> paths))
                return;

            paths.Remove(path);
            if (paths.Count == 0)
            {
                _resourceInstanceToPaths.Remove(instanceId);
            }
        }
    }
}
