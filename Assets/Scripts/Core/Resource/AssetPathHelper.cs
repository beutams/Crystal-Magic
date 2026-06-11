using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 资源路径工具，只提供最终资源路径。
    /// </summary>
    public static class AssetPathHelper
    {
        public static string GetUIAsset(string uiName)
        {
            return $"Assets/Res/UI/{uiName}.prefab";
        }
        public static string GetDataAsset(string tableName)
        {
            return $"Assets/Res/Data/{tableName}.json";
        }

        public static string GetUnitPrefabAsset(string prefabName)
        {
            return $"Assets/Res/Prefab/Unit/{prefabName}.prefab";
        }

        public static string GetProjectilePrefabAsset(string prefabName)
        {
            return $"Assets/Res/Prefab/Projectile/{prefabName}.prefab";
        }

        public static string GetDropPrefabAsset(string prefabName)
        {
            return $"Assets/Res/Prefab/Drop/{prefabName}.prefab";
        }

        public static string GetEnvironmentPrefabAsset(string prefabName)
        {
            return $"Assets/Res/Prefab/Environment/{prefabName}.prefab";
        }

        public static string GetVfxPrefabAsset(string prefabName)
        {
            return $"Assets/Res/Prefab/VFX/{prefabName}.prefab";
        }

        public static string GetImageAsset(string imagePathOrFileName)
        {
            return $"Assets/Res/Images/{imagePathOrFileName}.png";
        }

        public static string GetConfigAsset(string configName)
        {
            return $"Assets/Res/Config/{configName}.json";
        }

        public static string GetBgmAudioAsset(string audioFileName)
        {
            return $"Assets/Res/Audio/BGM/{audioFileName}";
        }

        public static string GetSfxAudioAsset(string audioFileName)
        {
            return $"Assets/Res/Audio/SFX/{audioFileName}";
        }
    }
}
