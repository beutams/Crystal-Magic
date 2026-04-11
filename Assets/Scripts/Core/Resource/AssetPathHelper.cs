using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 资源路径工具
    /// 负责生成各类资源的标准路径
    /// </summary>
    public static class AssetPathHelper
    {
        private const string ResRootPath = "Assets/Res";

        // 各类资源的子目录
        private const string UIPath = "UI";
        private const string DataPath = "Data";
        private const string ConfigPath = "Config";
        private const string PrefabExt = ".prefab";
        private const string JsonExt = ".json";

        /// <summary>
        /// 获取 UI 资源路径
        /// </summary>
        /// <param name="uiName">UI 预制体名称（不包含 .prefab）</param>
        /// <returns>完整的资源路径</returns>
        public static string GetUIAsset(string uiName)
        {
            return $"{ResRootPath}/{UIPath}/{uiName}{PrefabExt}";
        }

        /// <summary>
        /// 检查 UI 资源是否存在
        /// </summary>
        public static bool HasUIAsset(string uiName)
        {
#if UNITY_EDITOR
            string path = GetUIAsset(uiName);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
#else
            return false;
#endif
        }

        /// <summary>
        /// 获取资源根路径
        /// </summary>
        public static string GetResRoot()
        {
            return ResRootPath;
        }

        /// <summary>
        /// 获取配置表资源路径（Assets/Res/Data/{tableName}.json）
        /// </summary>
        public static string GetDataAsset(string tableName)
        {
            return $"{ResRootPath}/{DataPath}/{tableName}{JsonExt}";
        }

        /// <summary>
        /// 获取 Config 资源路径（Assets/Res/Config/{configName}.json）
        /// </summary>
        public static string GetConfigAsset(string configName)
        {
            return $"{ResRootPath}/{ConfigPath}/{configName}{JsonExt}";
        }

        /// <summary>
        /// 生成自定义资源路径
        /// </summary>
        public static string GetAsset(string subFolder, string assetName, string extension = "prefab")
        {
            return $"{ResRootPath}/{subFolder}/{assetName}.{extension}";
        }
    }
}
