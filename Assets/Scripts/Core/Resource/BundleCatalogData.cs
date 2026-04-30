using System;

namespace CrystalMagic.Core
{
    [Serializable]
    public sealed class BundleCatalogData
    {
        public string Version = "1.0.0";
        public BundleCatalogAssetEntry[] Assets = Array.Empty<BundleCatalogAssetEntry>();
        public BundleCatalogBundleEntry[] Bundles = Array.Empty<BundleCatalogBundleEntry>();
    }

    [Serializable]
    public sealed class BundleCatalogAssetEntry
    {
        public int Id;
        public string Path;
        public int BundleId;
        public int[] DependencyAssetIds = Array.Empty<int>();
    }

    [Serializable]
    public sealed class BundleCatalogBundleEntry
    {
        public int Id;
        public string Name;
        public int[] AssetIds = Array.Empty<int>();
    }
}
