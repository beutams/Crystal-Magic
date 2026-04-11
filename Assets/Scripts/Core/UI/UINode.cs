// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Use Tools/UI/UINode Config → Generate UINode to regenerate

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CrystalMagic.Core {
    /// <summary>
    /// UI 子节点引用容器
    /// 包含该节点上可能挂载的常用组件，组件不存在时为 null
    /// </summary>
    public class UINode
    {
        public GameObject GameObject;
        public RectTransform RectTransform;
        public Image Image;
        public Button Button;
        public Slider Slider;
        public Toggle Toggle;
        public InputField InputField;
        public TextMeshProUGUI TextMeshProUGUI;

        public static UINode From(GameObject go)
        {
            if (go == null) return null;
            var node = new UINode { GameObject = go };
            node.RectTransform = go.GetComponent<RectTransform>();
            node.Image = go.GetComponent<Image>();
            node.Button = go.GetComponent<Button>();
            node.Slider = go.GetComponent<Slider>();
            node.Toggle = go.GetComponent<Toggle>();
            node.InputField = go.GetComponent<InputField>();
            node.TextMeshProUGUI = go.GetComponent<TextMeshProUGUI>();
            return node;
        }
    }
}
