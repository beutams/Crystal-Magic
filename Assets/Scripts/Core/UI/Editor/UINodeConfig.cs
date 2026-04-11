using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrystalMagic.Editor.UI
{
    /// <summary>
    /// UINode 组件配置
    /// 存储哪些 Unity 组件类型会被包含在生成的 UINode 中
    /// 文件路径：Assets/Scripts/Core/UI/Editor/UINodeConfig.json
    /// </summary>
    [Serializable]
    public class UINodeConfig
    {
        public List<UINodeComponentEntry> Components = new()
        {
            new UINodeComponentEntry { TypeName = "RectTransform",    Namespace = "UnityEngine" },
            new UINodeComponentEntry { TypeName = "Image",            Namespace = "UnityEngine.UI" },
            new UINodeComponentEntry { TypeName = "Button",           Namespace = "UnityEngine.UI" },
            new UINodeComponentEntry { TypeName = "Slider",           Namespace = "UnityEngine.UI" },
            new UINodeComponentEntry { TypeName = "Toggle",           Namespace = "UnityEngine.UI" },
            new UINodeComponentEntry { TypeName = "InputField",       Namespace = "UnityEngine.UI" },
            new UINodeComponentEntry { TypeName = "TextMeshProUGUI",  Namespace = "TMPro" },
        };

        // ─── 文件路径 ─────────────────────────────
        public static readonly string FilePath =
            "Assets/Scripts/Core/UI/Editor/UINodeConfig.json";

        public static UINodeConfig Load()
        {
            if (!File.Exists(FilePath))
                return new UINodeConfig();

            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<UINodeConfig>(json) ?? new UINodeConfig();
        }

        public void Save()
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true), Encoding.UTF8);
        }
    }

    [Serializable]
    public class UINodeComponentEntry
    {
        public string TypeName;
        public string Namespace;
    }
}
