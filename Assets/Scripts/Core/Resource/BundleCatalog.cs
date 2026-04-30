using System.Collections.Generic;

namespace CrystalMagic.Core
{
    public sealed class BundleCatalog
    {
        private readonly BundleCatalogData _data;
        private readonly Dictionary<string, BundleRuntimeAssetEntry> _assetsByPath = new();

        public BundleCatalog(BundleCatalogData data)
        {
            _data = data ?? new BundleCatalogData();
            BuildIndices();
        }

        public BundleCatalogData Data => _data;

        public bool TryGetAsset(string path, out BundleRuntimeAssetEntry assetEntry)
        {
            return _assetsByPath.TryGetValue(AssetBundlePlatformUtility.NormalizeAssetPath(path), out assetEntry);
        }

        private void BuildIndices()
        {
            _assetsByPath.Clear();

            Dictionary<int, BundleCatalogAssetEntry> assetsById = new();
            Dictionary<int, string> bundleNamesById = new();

            if (_data.Bundles != null)
            {
                for (int i = 0; i < _data.Bundles.Length; i++)
                {
                    BundleCatalogBundleEntry bundleEntry = _data.Bundles[i];
                    if (bundleEntry == null || string.IsNullOrWhiteSpace(bundleEntry.Name))
                        continue;

                    bundleNamesById[bundleEntry.Id] = AssetBundlePlatformUtility.SanitizeBundleName(bundleEntry.Name);
                }
            }

            if (_data.Assets == null)
                return;

            for (int i = 0; i < _data.Assets.Length; i++)
            {
                BundleCatalogAssetEntry assetEntry = _data.Assets[i];
                if (assetEntry == null || string.IsNullOrWhiteSpace(assetEntry.Path))
                    continue;

                assetsById[assetEntry.Id] = assetEntry;
            }

            foreach (KeyValuePair<int, BundleCatalogAssetEntry> pair in assetsById)
            {
                BundleCatalogAssetEntry assetEntry = pair.Value;
                string normalizedPath = AssetBundlePlatformUtility.NormalizeAssetPath(assetEntry.Path);
                if (!bundleNamesById.TryGetValue(assetEntry.BundleId, out string bundleName))
                    continue;

                List<string> dependencyPaths = new();
                if (assetEntry.DependencyAssetIds != null)
                {
                    for (int i = 0; i < assetEntry.DependencyAssetIds.Length; i++)
                    {
                        int dependencyId = assetEntry.DependencyAssetIds[i];
                        if (!assetsById.TryGetValue(dependencyId, out BundleCatalogAssetEntry dependencyEntry)
                            || string.IsNullOrWhiteSpace(dependencyEntry.Path))
                        {
                            continue;
                        }

                        dependencyPaths.Add(AssetBundlePlatformUtility.NormalizeAssetPath(dependencyEntry.Path));
                    }
                }

                _assetsByPath[normalizedPath] = new BundleRuntimeAssetEntry
                {
                    Path = normalizedPath,
                    BundleName = bundleName,
                    DependencyPaths = dependencyPaths.ToArray(),
                };
            }
        }
    }

    public sealed class BundleRuntimeAssetEntry
    {
        public string Path { get; set; }
        public string BundleName { get; set; }
        public string[] DependencyPaths { get; set; }
    }
}
