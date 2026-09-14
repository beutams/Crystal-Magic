using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CrystalMagic.Core
{
    public static class AssetBundlePlatformUtility
    {
        public static string GetRuntimePlatformFolderName()
        {
            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor => "Windows",
                RuntimePlatform.WindowsPlayer => "Windows",
                RuntimePlatform.Android => "Android",
                RuntimePlatform.IPhonePlayer => "iOS",
                RuntimePlatform.OSXEditor => "OSX",
                RuntimePlatform.OSXPlayer => "OSX",
                _ => Application.platform.ToString(),
            };
        }

#if UNITY_EDITOR
        public static string GetBuildTargetFolderName(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows => "Windows",
                BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "iOS",
                BuildTarget.StandaloneOSX => "OSX",
                _ => target.ToString(),
            };
        }
#endif

        public static string CombineNormalized(params string[] parts)
        {
            return string.Join("/", parts).Replace("\\", "/");
        }

        public static string SanitizeBundleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string sanitized = name.Replace("\\", "/").Trim().ToLowerInvariant();
            sanitized = sanitized.Replace(" ", "_");
            return sanitized;
        }

        public static int CreateStableId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return 0;

            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;
                uint hash = offsetBasis;
                string normalized = input.Replace("\\", "/").Trim().ToLowerInvariant();
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= normalized[i];
                    hash *= prime;
                }

                return (int)(hash & int.MaxValue);
            }
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace("\\", "/").Trim();
        }

        public static bool TrySplitSubAssetPath(string path, out string assetPath, out string subAssetName)
        {
            string normalizedPath = NormalizeAssetPath(path);
            int separatorIndex = normalizedPath.LastIndexOf('|');
            if (separatorIndex <= 0 || separatorIndex >= normalizedPath.Length - 1)
            {
                assetPath = normalizedPath;
                subAssetName = string.Empty;
                return false;
            }

            assetPath = normalizedPath[..separatorIndex].Trim();
            subAssetName = normalizedPath[(separatorIndex + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(assetPath) && !string.IsNullOrWhiteSpace(subAssetName);
        }

        public static string GetBundleRootPath(string rootFolderName)
        {
            string folderName = string.IsNullOrWhiteSpace(rootFolderName) ? "AssetBundles" : rootFolderName.Trim().Trim('/', '\\');
            return Path.Combine(Application.streamingAssetsPath, folderName, GetRuntimePlatformFolderName());
        }
    }
}
