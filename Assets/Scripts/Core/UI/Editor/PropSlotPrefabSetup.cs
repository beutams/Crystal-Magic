using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.Editor.UI
{
    public static class PropSlotPrefabSetup
    {
        private const string SessionKey = "CrystalMagic.PropSlotPrefabSetup.Applied";

        [InitializeOnLoadMethod]
        private static void ApplyOnce()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += Apply;
        }

        public static void Apply()
        {
            SetupCharacterUI();
            SetupBattleUI();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetupCharacterUI()
        {
            const string path = "Assets/Res/UI/CharacterUI.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Transform existing = root.transform.Find("PropSlots");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            RectTransform bar = CreateBar(root.transform, "PropSlots", new Vector2(0.5f, 0f), new Vector2(0f, 92f));
            CreateCharacterSlot(bar, "PropSlot1", "Z", 0);
            CreateCharacterSlot(bar, "PropSlot2", "X", 1);
            CreateCharacterSlot(bar, "PropSlot3", "C", 2);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UIDataGenerator.GenerateForPrefab(root);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetupBattleUI()
        {
            const string path = "Assets/Res/UI/BattleUI.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Transform existing = root.transform.Find("PropShortcuts");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            RectTransform bar = CreateBar(root.transform, "PropShortcuts", new Vector2(1f, 0f), new Vector2(-190f, 92f));
            CreateBattleSlot(bar, "PropSlot1", "Z", 0);
            CreateBattleSlot(bar, "PropSlot2", "X", 1);
            CreateBattleSlot(bar, "PropSlot3", "C", 2);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UIDataGenerator.GenerateForPrefab(root);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static RectTransform CreateBar(Transform parent, string name, Vector2 anchor, Vector2 position)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(210f, 64f);

            HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private static void CreateCharacterSlot(Transform parent, string name, string key, int slotIndex)
        {
            RectTransform slot = CreateSlot(parent, name, key, false);
            CharacterUI_PropSlotDragHandler handler = slot.gameObject.AddComponent<CharacterUI_PropSlotDragHandler>();
            handler.Initialize(slotIndex);
        }

        private static void CreateBattleSlot(Transform parent, string name, string key, int slotIndex)
        {
            RectTransform slot = CreateSlot(parent, name, key, true);
            BattleUI_PropSlotClickHandler handler = slot.gameObject.AddComponent<BattleUI_PropSlotClickHandler>();
            handler.Initialize(slotIndex);
        }

        private static RectTransform CreateSlot(Transform parent, string name, string key, bool includeCooldown)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(64f, 64f);
            Image background = gameObject.GetComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.08f, 0.78f);
            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 64f;
            layout.preferredHeight = 64f;

            CreateImage(rect, "Icon", new Color(1f, 1f, 1f, 0.2f), new Vector2(6f, 6f), new Vector2(-6f, -6f));
            if (includeCooldown)
                CreateImage(rect, "Cooldown", new Color(1f, 1f, 1f, 0.48f), Vector2.zero, Vector2.zero);
            CreateText(rect, "Count", string.Empty, 18f, TextAlignmentOptions.BottomRight, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            CreateText(rect, "Key", key, 16f, TextAlignmentOptions.TopLeft, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            return rect;
        }

        private static void CreateImage(Transform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateText(
            Transform parent,
            string name,
            string value,
            float fontSize,
            TextAlignmentOptions alignment,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
        }
    }
}
