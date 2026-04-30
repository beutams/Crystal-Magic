using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CrystalMagic.Core
{
    public class AssetBundleResourceLoader : IResourceLoader
    {
        private readonly string _rootFolderName;
        private readonly string _catalogBundleName;
        private readonly string _catalogAssetName;

        private readonly Dictionary<string, AssetBundle> _loadedBundles = new();
        private readonly Dictionary<string, int> _bundleRefCounts = new();
        private readonly Dictionary<string, Object> _loadedAssets = new();
        private readonly Dictionary<string, int> _assetRefCounts = new();

        private BundleCatalog _catalog;
        private string _bundleRootPath;

        public AssetBundleResourceLoader(string rootFolderName, string catalogBundleName, string catalogAssetName)
        {
            _rootFolderName = rootFolderName;
            _catalogBundleName = AssetBundlePlatformUtility.SanitizeBundleName(string.IsNullOrWhiteSpace(catalogBundleName) ? "catalog" : catalogBundleName);
            _catalogAssetName = string.IsNullOrWhiteSpace(catalogAssetName) ? "BundleCatalogAsset" : catalogAssetName;
        }

        public void Initialize()
        {
            _bundleRootPath = AssetBundlePlatformUtility.GetBundleRootPath(_rootFolderName);
            EnsureCatalogLoaded();
        }

        public T Load<T>(string path) where T : Object
        {
            string normalizedPath = AssetBundlePlatformUtility.NormalizeAssetPath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath) || !EnsureCatalogLoaded())
                return null;

            if (_loadedAssets.TryGetValue(normalizedPath, out Object cachedAsset))
            {
                if (cachedAsset is not T typedCachedAsset)
                {
                    Debug.LogError($"[AssetBundleResourceLoader] Cached asset type mismatch for '{normalizedPath}'. Requested '{typeof(T).Name}', cached '{cachedAsset.GetType().Name}'.");
                    return null;
                }

                _assetRefCounts[normalizedPath] = _assetRefCounts.TryGetValue(normalizedPath, out int cachedCount)
                    ? cachedCount + 1
                    : 1;
                return typedCachedAsset;
            }

            if (!_catalog.TryGetAsset(normalizedPath, out BundleRuntimeAssetEntry assetEntry))
            {
                Debug.LogError($"[AssetBundleResourceLoader] Catalog entry not found for '{normalizedPath}'.");
                return null;
            }

            AcquireBundlesForAsset(assetEntry);

            if (!_loadedBundles.TryGetValue(assetEntry.BundleName, out AssetBundle bundle) || bundle == null)
            {
                Debug.LogError($"[AssetBundleResourceLoader] Bundle '{assetEntry.BundleName}' is not loaded for asset '{normalizedPath}'.");
                ReleaseBundlesForAsset(assetEntry);
                return null;
            }

            T asset = bundle.LoadAsset<T>(normalizedPath);
            if (asset == null)
            {
                asset = bundle.LoadAsset<T>(Path.GetFileNameWithoutExtension(normalizedPath));
            }

            if (asset == null)
            {
                Debug.LogError($"[AssetBundleResourceLoader] Failed to load asset '{normalizedPath}' from bundle '{assetEntry.BundleName}'.");
                ReleaseBundlesForAsset(assetEntry);
                return null;
            }

            _loadedAssets[normalizedPath] = asset;
            _assetRefCounts[normalizedPath] = 1;
            return asset;
        }

        public IEnumerator LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object
        {
            T asset = Load<T>(path);
            onComplete?.Invoke(asset);
            yield return null;
        }

        public void Release(string path)
        {
            string normalizedPath = AssetBundlePlatformUtility.NormalizeAssetPath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath) || !_assetRefCounts.TryGetValue(normalizedPath, out int refCount))
                return;

            refCount--;
            if (refCount > 0)
            {
                _assetRefCounts[normalizedPath] = refCount;
                return;
            }

            _assetRefCounts.Remove(normalizedPath);
            _loadedAssets.Remove(normalizedPath);

            if (_catalog != null && _catalog.TryGetAsset(normalizedPath, out BundleRuntimeAssetEntry assetEntry))
            {
                ReleaseBundlesForAsset(assetEntry);
            }
        }

        public void ReleaseAll()
        {
            _assetRefCounts.Clear();
            _loadedAssets.Clear();

            foreach (KeyValuePair<string, AssetBundle> pair in _loadedBundles)
            {
                pair.Value?.Unload(false);
            }

            _loadedBundles.Clear();
            _bundleRefCounts.Clear();
            _catalog = null;
        }

        private bool EnsureCatalogLoaded()
        {
            if (_catalog != null)
                return true;

            string catalogBundlePath = Path.Combine(_bundleRootPath, _catalogBundleName);
            if (!File.Exists(catalogBundlePath))
            {
                Debug.LogError($"[AssetBundleResourceLoader] Catalog bundle not found: {catalogBundlePath}");
                return false;
            }

            AssetBundle catalogBundle = AssetBundle.LoadFromFile(catalogBundlePath);
            if (catalogBundle == null)
            {
                Debug.LogError($"[AssetBundleResourceLoader] Failed to load catalog bundle: {catalogBundlePath}");
                return false;
            }

            BundleCatalogAsset catalogAsset = catalogBundle.LoadAsset<BundleCatalogAsset>(_catalogAssetName);
            if (catalogAsset == null)
            {
                Debug.LogError($"[AssetBundleResourceLoader] Failed to load catalog asset '{_catalogAssetName}' from bundle '{_catalogBundleName}'.");
                catalogBundle.Unload(false);
                return false;
            }

            _catalog = new BundleCatalog(catalogAsset.Data);
            catalogBundle.Unload(false);
            return true;
        }

        private void AcquireBundlesForAsset(BundleRuntimeAssetEntry assetEntry)
        {
            HashSet<string> bundleNames = CollectBundleNames(assetEntry);
            foreach (string bundleName in bundleNames)
            {
                AcquireBundle(bundleName);
            }
        }

        private void ReleaseBundlesForAsset(BundleRuntimeAssetEntry assetEntry)
        {
            HashSet<string> bundleNames = CollectBundleNames(assetEntry);
            foreach (string bundleName in bundleNames)
            {
                ReleaseBundle(bundleName);
            }
        }

        private HashSet<string> CollectBundleNames(BundleRuntimeAssetEntry rootAsset)
        {
            HashSet<string> bundleNames = new();
            HashSet<string> visitedPaths = new();
            CollectBundleNamesRecursive(rootAsset, bundleNames, visitedPaths);
            return bundleNames;
        }

        private void CollectBundleNamesRecursive(BundleRuntimeAssetEntry assetEntry, HashSet<string> bundleNames, HashSet<string> visitedPaths)
        {
            if (assetEntry == null || string.IsNullOrWhiteSpace(assetEntry.Path) || !visitedPaths.Add(assetEntry.Path))
                return;

            if (!string.IsNullOrWhiteSpace(assetEntry.BundleName))
            {
                bundleNames.Add(assetEntry.BundleName);
            }

            if (assetEntry.DependencyPaths == null)
                return;

            for (int i = 0; i < assetEntry.DependencyPaths.Length; i++)
            {
                string dependencyPath = assetEntry.DependencyPaths[i];
                if (_catalog.TryGetAsset(dependencyPath, out BundleRuntimeAssetEntry dependencyAsset))
                {
                    CollectBundleNamesRecursive(dependencyAsset, bundleNames, visitedPaths);
                }
            }
        }

        private void AcquireBundle(string bundleName)
        {
            if (string.IsNullOrWhiteSpace(bundleName))
                return;

            if (_bundleRefCounts.TryGetValue(bundleName, out int refCount))
            {
                _bundleRefCounts[bundleName] = refCount + 1;
                return;
            }

            string bundlePath = Path.Combine(_bundleRootPath, bundleName);
            if (!File.Exists(bundlePath))
            {
                Debug.LogError($"[AssetBundleResourceLoader] Bundle file not found: {bundlePath}");
                return;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Debug.LogError($"[AssetBundleResourceLoader] Failed to load bundle: {bundlePath}");
                return;
            }

            _loadedBundles[bundleName] = bundle;
            _bundleRefCounts[bundleName] = 1;
        }

        private void ReleaseBundle(string bundleName)
        {
            if (string.IsNullOrWhiteSpace(bundleName) || !_bundleRefCounts.TryGetValue(bundleName, out int refCount))
                return;

            refCount--;
            if (refCount > 0)
            {
                _bundleRefCounts[bundleName] = refCount;
                return;
            }

            _bundleRefCounts.Remove(bundleName);
            if (_loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
            {
                bundle?.Unload(false);
                _loadedBundles.Remove(bundleName);
            }
        }
    }
}
