using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Resource
{
    public static class BundleBuildUtility
    {
        public const string ConfigPath = "Assets/Res/Config/BundleBuildConfig.json";

        public static BundleBuildConfigData LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                return CreateDefaultConfig();
            }

            string json = File.ReadAllText(ConfigPath);
            BundleBuildConfigData config = JsonUtility.FromJson<BundleBuildConfigData>(json) ?? CreateDefaultConfig();
            EnsureRuleList(config);
            return config;
        }

        public static void SaveConfig(BundleBuildConfigData config)
        {
            EnsureRuleList(config);
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(ConfigPath, JsonUtility.ToJson(config, true));
            AssetDatabase.Refresh();
        }

        public static bool Build(BundleBuildConfigData config)
        {
            if (config == null)
            {
                Debug.LogError("[BundleBuildUtility] Config is null.");
                return false;
            }

            EnsureRuleList(config);
            Dictionary<string, string> assetToBundle = CollectAssetAssignments(config);
            if (assetToBundle.Count == 0)
            {
                Debug.LogError("[BundleBuildUtility] No assets matched build rules.");
                return false;
            }

            string tempCatalogAssetPath = null;
            try
            {
                BundleCatalogData catalogData = BuildCatalogData(assetToBundle);
                tempCatalogAssetPath = CreateCatalogAsset(config, catalogData);

                Dictionary<string, List<string>> bundleToAssets = BuildBundleAssetMap(assetToBundle);
                string catalogBundleName = AssetBundlePlatformUtility.SanitizeBundleName(config.CatalogBundleName);
                if (!bundleToAssets.TryGetValue(catalogBundleName, out List<string> catalogAssets))
                {
                    catalogAssets = new List<string>();
                    bundleToAssets[catalogBundleName] = catalogAssets;
                }

                catalogAssets.Add(tempCatalogAssetPath);
                AssetBundleBuild[] builds = CreateBuilds(bundleToAssets);
                string outputPath = GetOutputPath(config);
                Directory.CreateDirectory(outputPath);

                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                    outputPath,
                    builds,
                    config.BuildOptions,
                    config.BuildTarget);

                bool success = manifest != null;
                Debug.Log(success
                    ? $"[BundleBuildUtility] Build completed: {outputPath}"
                    : "[BundleBuildUtility] Build failed.");
                return success;
            }
            finally
            {
                CleanupTempCatalogAsset(tempCatalogAssetPath);
            }
        }

        public static string GetOutputPath(BundleBuildConfigData config)
        {
            string root = string.IsNullOrWhiteSpace(config.OutputRootFolder)
                ? "Assets/StreamingAssets/AssetBundles"
                : config.OutputRootFolder.Trim();
            return Path.Combine(root, AssetBundlePlatformUtility.GetBuildTargetFolderName(config.BuildTarget));
        }

        private static BundleBuildConfigData CreateDefaultConfig()
        {
            return new BundleBuildConfigData
            {
                Rules = new List<BundleBuildRuleData>
                {
                    new() { FolderPath = "Assets/Res/UI", BundleName = "ui", PackingMode = BundlePackingMode.OneAssetOneBundle },
                    new() { FolderPath = "Assets/Res/Data", BundleName = "data", PackingMode = BundlePackingMode.SingleBundle },
                    new() { FolderPath = "Assets/Res/Config", BundleName = "config", PackingMode = BundlePackingMode.SingleBundle },
                }
            };
        }

        private static void EnsureRuleList(BundleBuildConfigData config)
        {
            config.Rules ??= new List<BundleBuildRuleData>();
        }

        private static Dictionary<string, string> CollectAssetAssignments(BundleBuildConfigData config)
        {
            Dictionary<string, string> assetToBundle = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < config.Rules.Count; i++)
            {
                BundleBuildRuleData rule = config.Rules[i];
                if (rule == null || !rule.Enabled || string.IsNullOrWhiteSpace(rule.FolderPath))
                    continue;

                string normalizedFolderPath = AssetBundlePlatformUtility.NormalizeAssetPath(rule.FolderPath);
                if (!AssetDatabase.IsValidFolder(normalizedFolderPath))
                {
                    Debug.LogWarning($"[BundleBuildUtility] Folder not found: {normalizedFolderPath}");
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { normalizedFolderPath });
                for (int j = 0; j < guids.Length; j++)
                {
                    string assetPath = AssetBundlePlatformUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[j]));
                    if (!ShouldIncludeAsset(assetPath, normalizedFolderPath, rule.IncludeSubfolders) || assetToBundle.ContainsKey(assetPath))
                        continue;

                    string bundleName = ResolveBundleName(assetPath, rule);
                    if (string.IsNullOrWhiteSpace(bundleName))
                        continue;

                    assetToBundle[assetPath] = bundleName;
                }
            }

            return assetToBundle;
        }

        private static bool ShouldIncludeAsset(string assetPath, string folderPath, bool includeSubfolders)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || Directory.Exists(assetPath))
                return false;

            if (assetPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            string extension = Path.GetExtension(assetPath);
            if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!includeSubfolders)
            {
                string parent = AssetBundlePlatformUtility.NormalizeAssetPath(Path.GetDirectoryName(assetPath));
                if (!string.Equals(parent, folderPath, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (assetType == null || typeof(MonoScript).IsAssignableFrom(assetType))
                return false;

            return AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
        }

        private static string ResolveBundleName(string assetPath, BundleBuildRuleData rule)
        {
            string baseName = AssetBundlePlatformUtility.SanitizeBundleName(rule.BundleName);
            if (string.IsNullOrWhiteSpace(baseName))
                return string.Empty;

            if (rule.PackingMode == BundlePackingMode.SingleBundle)
                return baseName;

            string assetName = AssetBundlePlatformUtility.SanitizeBundleName(Path.GetFileNameWithoutExtension(assetPath));
            return $"{baseName}_{assetName}";
        }

        private static BundleCatalogData BuildCatalogData(Dictionary<string, string> assetToBundle)
        {
            Dictionary<string, int> assetIds = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<int, string> usedAssetIds = new();
            foreach (string assetPath in assetToBundle.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                int id = AssetBundlePlatformUtility.CreateStableId(assetPath);
                if (usedAssetIds.TryGetValue(id, out string existingPath) && !string.Equals(existingPath, assetPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Asset id collision: '{existingPath}' and '{assetPath}' => {id}");

                usedAssetIds[id] = assetPath;
                assetIds[assetPath] = id;
            }

            Dictionary<string, int> bundleIds = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<int, string> usedBundleIds = new();
            foreach (string bundleName in assetToBundle.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                int id = AssetBundlePlatformUtility.CreateStableId($"bundle::{bundleName}");
                if (usedBundleIds.TryGetValue(id, out string existingBundle) && !string.Equals(existingBundle, bundleName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Bundle id collision: '{existingBundle}' and '{bundleName}' => {id}");

                usedBundleIds[id] = bundleName;
                bundleIds[bundleName] = id;
            }

            List<BundleCatalogAssetEntry> assetEntries = new();
            foreach (string assetPath in assetToBundle.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                List<int> dependencyIds = new();
                for (int i = 0; i < dependencies.Length; i++)
                {
                    string dependencyPath = AssetBundlePlatformUtility.NormalizeAssetPath(dependencies[i]);
                    if (string.Equals(dependencyPath, assetPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (assetIds.TryGetValue(dependencyPath, out int dependencyId))
                        dependencyIds.Add(dependencyId);
                }

                dependencyIds.Sort();
                assetEntries.Add(new BundleCatalogAssetEntry
                {
                    Id = assetIds[assetPath],
                    Path = assetPath,
                    BundleId = bundleIds[assetToBundle[assetPath]],
                    DependencyAssetIds = dependencyIds.ToArray(),
                });
            }

            Dictionary<string, List<int>> bundleAssetIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in assetToBundle)
            {
                if (!bundleAssetIds.TryGetValue(pair.Value, out List<int> ids))
                {
                    ids = new List<int>();
                    bundleAssetIds[pair.Value] = ids;
                }

                ids.Add(assetIds[pair.Key]);
            }

            List<BundleCatalogBundleEntry> bundleEntries = new();
            foreach (KeyValuePair<string, List<int>> pair in bundleAssetIds.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                string bundleName = pair.Key;
                List<int> ids = pair.Value;
                ids.Sort();
                bundleEntries.Add(new BundleCatalogBundleEntry
                {
                    Id = bundleIds[bundleName],
                    Name = bundleName,
                    AssetIds = ids.ToArray(),
                });
            }

            return new BundleCatalogData
            {
                Version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                Assets = assetEntries.ToArray(),
                Bundles = bundleEntries.ToArray(),
            };
        }

        private static string CreateCatalogAsset(BundleBuildConfigData config, BundleCatalogData catalogData)
        {
            string assetPath = AssetBundlePlatformUtility.NormalizeAssetPath(config.TempCatalogAssetPath);
            string directory = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursively(directory);
            }

            AssetDatabase.DeleteAsset(assetPath);

            BundleCatalogAsset asset = ScriptableObject.CreateInstance<BundleCatalogAsset>();
            asset.name = string.IsNullOrWhiteSpace(config.CatalogAssetName) ? "BundleCatalogAsset" : config.CatalogAssetName.Trim();
            asset.Data = catalogData;
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return assetPath;
        }

        private static void CleanupTempCatalogAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            AssetDatabase.DeleteAsset(assetPath);
        }

        private static void CreateFolderRecursively(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static Dictionary<string, List<string>> BuildBundleAssetMap(Dictionary<string, string> assetToBundle)
        {
            Dictionary<string, List<string>> bundleToAssets = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in assetToBundle)
            {
                if (!bundleToAssets.TryGetValue(pair.Value, out List<string> assets))
                {
                    assets = new List<string>();
                    bundleToAssets[pair.Value] = assets;
                }

                assets.Add(pair.Key);
            }

            return bundleToAssets;
        }

        private static AssetBundleBuild[] CreateBuilds(Dictionary<string, List<string>> bundleToAssets)
        {
            List<AssetBundleBuild> builds = new();
            foreach (KeyValuePair<string, List<string>> pair in bundleToAssets.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                string bundleName = pair.Key;
                List<string> assets = pair.Value;
                assets.Sort(StringComparer.OrdinalIgnoreCase);
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = assets.ToArray(),
                });
            }

            return builds.ToArray();
        }
    }
}
