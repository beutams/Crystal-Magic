using System.Collections.Generic;
using UnityEngine.UI;

namespace CrystalMagic.Core {
    /// <summary>
    /// UI 分组配置入口
    /// </summary>
    [System.Serializable]
    public class UIGroupEntry
    {
        public UIGroupType groupType = UIGroupType.Stack;
        public string groupName = "Default";
        public int order = 0;
        public List<string> uiNames = new();
    }

    /// <summary>
    /// UI 分组 JSON 配置
    /// </summary>
    [System.Serializable]
    public class UIGroupConfig
    {
        public int referenceResolutionWidth = 2560;
        public int referenceResolutionHeight = 1440;
        public CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        public float planeDistance = 0.31f;
        public float hoverInfoDelaySeconds = 2f;
        public List<UIGroupEntry> groups = new();
    }

    /// <summary>
    /// UI 配置加载器
    /// </summary>
    public static class UIConfigLoader
    {
        private const string ConfigPath = "Assets/Res/Config/ui_config.json";

        /// <summary>
        /// 从 JSON 加载配置
        /// </summary>
        public static UIGroupConfig LoadFromJson(string jsonText)
        {
            return UnityEngine.JsonUtility.FromJson<UIGroupConfig>(jsonText);
        }

        /// <summary>
        /// 将配置保存为 JSON
        /// </summary>
        public static string SaveToJson(UIGroupConfig config)
        {
            return UnityEngine.JsonUtility.ToJson(config, true);
        }
    }
}
