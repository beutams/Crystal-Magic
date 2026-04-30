using System;
using System.Collections.Generic;
using UnityEditor;

namespace CrystalMagic.Editor.Resource
{
    [Serializable]
    public sealed class BundleBuildConfigData
    {
        public string OutputRootFolder = "Assets/StreamingAssets/AssetBundles";
        public BuildTarget BuildTarget = BuildTarget.StandaloneWindows64;
        public BuildAssetBundleOptions BuildOptions = BuildAssetBundleOptions.ChunkBasedCompression;
        public string CatalogBundleName = "catalog";
        public string CatalogAssetName = "BundleCatalogAsset";
        public string TempCatalogAssetPath = "Assets/__BundleBuildTemp/BundleCatalogAsset.asset";
        public List<BundleBuildRuleData> Rules = new();
    }

    public enum BundlePackingMode
    {
        SingleBundle,
        OneAssetOneBundle,
    }

    [Serializable]
    public sealed class BundleBuildRuleData
    {
        public bool Enabled = true;
        public string FolderPath = "Assets/Res/UI";
        public string BundleName = "ui";
        public BundlePackingMode PackingMode = BundlePackingMode.SingleBundle;
        public bool IncludeSubfolders = true;
    }
}
