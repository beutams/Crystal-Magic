using System;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using Server;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.Editor.UI
{
    /// <summary>Authors the lobby using the existing UI sprites and font; never modifies a scene.</summary>
    public static class LobbyUIPrefabBuilder
    {
        public const string Output = "output/Previews/LobbyRedesign";
        private const string Sprites = "Assets/Res/Sprites/UISprites/";
        private static readonly Color Ink = new Color32(74, 48, 39, 255);
        private static readonly Color Muted = new Color32(129, 98, 81, 255);
        private static readonly Color Cream = new Color32(242, 228, 198, 255);
        private static TMP_FontAsset font;

        [MenuItem("Tools/UI/Rebuild Lobby and Create Room")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build UI prefabs outside Play mode.");
            Directory.CreateDirectory(Output);
            font = GetLobbyFont();
            GameObject lobby = MakeLobby(), create = MakeCreateRoom();
            try
            {
                GameObject lobbyAsset = PrefabUtility.SaveAsPrefabAsset(lobby, "Assets/Res/UI/LobbyUI.prefab");
                GameObject createAsset = PrefabUtility.SaveAsPrefabAsset(create, "Assets/Res/UI/CreateRoomUI.prefab");
                UIDataGenerator.GenerateForPrefab(lobbyAsset);
                UIDataGenerator.GenerateForPrefab(createAsset);
                UIDataGenerator.GenerateForTransform(lobbyAsset.transform.Find("Panel/RoomScroll/Viewport/Content/RoomItem"),
                    "LobbyUI_RoomItemData", "Assets/Scripts/UI/LobbyUI/Sub");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally { UnityEngine.Object.DestroyImmediate(lobby); UnityEngine.Object.DestroyImmediate(create); }
        }

        [MenuItem("Tools/UI/Rebuild Room")]
        public static void BuildRoom()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build UI prefabs outside Play mode.");
            font = GetLobbyFont();
            GameObject room = MakeRoom();
            try
            {
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(room, "Assets/Res/UI/LobbyRoomUI.prefab");
                UIDataGenerator.GenerateForPrefab(asset);
                UIDataGenerator.GenerateForTransform(asset.transform.Find("Panel/Players/One"),
                    "LobbyRoomUI_PlayerData", "Assets/Scripts/UI/LobbyRoomUI");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally { UnityEngine.Object.DestroyImmediate(room); }
        }

        private static GameObject MakeRoom()
        {
            GameObject root = Root<LobbyRoomUI>("LobbyRoomUI");
            var serializedView = new SerializedObject(root.GetComponent<LobbyRoomUI>());
            serializedView.FindProperty("_canCloseByEscape").boolValue = false;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            Image background = Picture(root.transform, "Background", "MainMenu/MainMenuBackground.png");
            Stretch(background.rectTransform);
            Image shade = Solid(root.transform, "Shade", new Color(0, 0, 0, .33f), true);
            Stretch(shade.rectTransform);
            RectTransform panel = Paper(root.transform, 1080, 600, -10);
            Header(panel, "ui.room.title", 351, -28);
            Text(panel, "RoomName", "旅行者的房间", 80, 95, 650, 48, 30);
            Text(panel, "PlayerCount", "队伍 3 / 4", 772, 104, 216, 38, 24, TextAlignmentOptions.Right);
            Text(panel, "HostName", "房主：旅行者", 80, 155, 540, 32, 18, color: Muted);
            Text(panel, "ReadyCount", "已准备 2 / 3", 718, 155, 270, 32, 18, TextAlignmentOptions.Right, Muted);
            Picture(panel, "Divider", "MagicalUI/BodyAssets/Book Assets/Line.png", 80, 203, 908, 2);
            RectTransform players = Rect(panel, "Players", 80, 224, 908, 242);
            PlayerSeat(players, "One", 0, 0);
            PlayerSeat(players, "Two", 470, 0);
            PlayerSeat(players, "Three", 0, 128);
            PlayerSeat(players, "Four", 470, 128);
            Text(panel, "Hint", "还有 1 位队员未准备", 80, 480, 908, 32, 18, color: Muted);
            Button(panel, "Leave", 80, 526, 180, 50, "ui.room.leave").gameObject.AddComponent<CanvasGroup>();
            Text(panel, "State", "等待队员准备", 284, 531, 440, 38, 18, color: Muted);
            Button(panel, "Confirm", 754, 526, 234, 50, null, true).gameObject.AddComponent<CanvasGroup>();
            return root;
        }

        private static void PlayerSeat(Transform parent, string name, float x, float y)
        {
            RectTransform seat = Rect(parent, name, x, y, 438, 114);
            seat.gameObject.AddComponent<CanvasGroup>();
            Picture(seat, "Surface", "LoadUI/Button-Cream.png", 0, 0, 438, 114, .25f);
            Image selected = Picture(seat, "Selected", "LoadUI/Button-Sand.png", 0, 0, 438, 114, .25f);
            selected.gameObject.SetActive(false);
            Text(seat, "Number", "01", 20, 12, 42, 32, 18, color: Muted);
            Picture(seat, "HostIcon", "MagicalUI/BodyAssets/Scroll Assets/Star.png", 376, 17, 26, 26);
            Text(seat, "Role", "房主 · 你", 74, 12, 280, 32, 18, color: Muted);
            Text(seat, "PlayerName", "旅行者", 20, 46, 382, 36, 28);
            Text(seat, "Status", "已准备", 20, 85, 382, 22, 16, color: new Color32(83, 100, 71, 255));
        }

        private static TMP_FontAsset GetLobbyFont()
        {
            const string path = "Assets/Fonts/FusionPixelFont/Lobby Pixel SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Fonts/FusionPixelFont/fusion-pixel-12px-proportional-zh_hans.ttf");
            if (source == null) throw new InvalidOperationException("The existing pixel font source is missing.");
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = "Lobby Pixel SDF";
            asset.material.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D atlas in asset.atlasTextures)
            { atlas.hideFlags = HideFlags.None; AssetDatabase.AddObjectToAsset(atlas, asset); }
            var table = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Res/Data/LocalizationDataTable.json"));
            string characters = new string(Enumerable.Range(32, 95).Select(code => (char)code).ToArray()) +
                "旅行者星火地牢探索双人冒险月影";
            foreach (var row in table["Rows"])
                if (((string)row["Key"]).StartsWith("ui.lobby.", StringComparison.Ordinal))
                    characters += (string)row["ChineseSimplified"] + (string)row["English"];
            asset.TryAddCharacters(characters);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject MakeLobby()
        {
            GameObject root = Root<LobbyUI>("LobbyUI");
            Image background = Picture(root.transform, "Background", "MainMenu/MainMenuBackground.png");
            Stretch(background.rectTransform);
            Image shade = Solid(root.transform, "Shade", new Color(0, 0, 0, .33f));
            Stretch(shade.rectTransform);
            RectTransform panel = Paper(root.transform, 1080, 600, -10);
            Header(panel, "ui.lobby.title", 351, -28);
            Picture(panel, "Star", "MagicalUI/BodyAssets/Scroll Assets/Star.png", 80, 94, 28, 28);
            Text(panel, "PlayerName", "旅行者", 122, 90, 260, 40, 24);
            Text(panel, "AccountSource", "Steam 昵称", 400, 94, 260, 32, 18, color: Muted);
            Text(panel, "Connection", "已连接", 840, 94, 142, 32, 18,
                TextAlignmentOptions.Right, new Color32(83, 100, 71, 255));
            Text(panel, "ListTitle", "", 80, 149, 360, 44, 24, key: "ui.lobby.list");
            Text(panel, "RoomCount", "3 个房间", 732, 153, 250, 32, 18, TextAlignmentOptions.Right, Muted);
            Picture(panel, "Divider", "MagicalUI/BodyAssets/Book Assets/Line.png", 80, 193, 906, 2);
            Text(panel, "RoomNameHeader", "", 100, 207, 360, 38, 18, key: "ui.lobby.room_name");
            Text(panel, "HostHeader", "", 472, 207, 168, 38, 18, key: "ui.lobby.host");
            Text(panel, "PlayersHeader", "", 658, 207, 96, 38, 18, key: "ui.lobby.players");
            Text(panel, "StateHeader", "", 764, 207, 100, 38, 18, key: "ui.lobby.state");
            RectTransform scrollRoot = Rect(panel, "RoomScroll", 80, 248, 906, 228);
            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60;
            RectTransform viewport = Rect(scrollRoot, "Viewport", 0, 0, 906, 228);
            Solid(viewport.gameObject, Color.clear, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport, "Content", 0, 0, 906, 0);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24; layout.childControlWidth = true; layout.childControlHeight = false;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            RectTransform row = RoomRow(content);
            row.gameObject.SetActive(false);
            Text(panel, "Empty", "", 80, 292, 906, 110, 24, TextAlignmentOptions.Center, Muted, "ui.lobby.empty");
            Button(panel, "Back", 80, 516, 138, 52, "ui.lobby.back");
            Button(panel, "Refresh", 590, 516, 164, 52, "ui.lobby.refresh");
            Button(panel, "CreateRoom", 778, 516, 210, 52, "ui.lobby.create", true);
            return root;
        }

        private static RectTransform RoomRow(Transform parent)
        {
            Image image = Picture(parent, "RoomItem", "LoadUI/Button-Cream.png", 0, 0, 906, 68, .25f);
            image.gameObject.AddComponent<LobbyUI_RoomItemView>();
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = 136;
            RectTransform row = image.rectTransform;
            Text(row, "RoomName", "旅行者的房间", 20, 13, 340, 42, 24);
            Text(row, "HostName", "旅行者", 392, 18, 160, 32, 18);
            Text(row, "PlayerCount", "1 / 4", 578, 13, 90, 42, 24);
            Text(row, "State", "等待中", 684, 18, 98, 32, 18, color: Muted);
            RectTransform join = Button(row, "Join", 792, 10, 98, 46, "ui.lobby.join");
            join.gameObject.AddComponent<CanvasGroup>();
            return row;
        }

        private static GameObject MakeCreateRoom()
        {
            GameObject root = Root<CreateRoomUI>("CreateRoomUI");
            Image overlay = Solid(root.transform, "Overlay", new Color(0, 0, 0, .65f), true);
            Stretch(overlay.rectTransform);
            RectTransform panel = Paper(root.transform, 664, 566, -14);
            Header(panel, "ui.lobby.create_title", 143, -10);
            RectTransform close = Rect(panel, "Close", 596, 31, 28, 32);
            Solid(close.gameObject, Color.clear, true);
            Sprite[] crosses = AssetDatabase.LoadAllAssetsAtPath(Sprites + "Wood UI/Sprites/Buttons/7x8.png")
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (crosses.Length != 2) throw new InvalidOperationException("Existing close-button sprites are missing.");
            Image defaultCross = ImageWithSprite(close, "Default", crosses[0]); Stretch(defaultCross.rectTransform);
            Image pressedCross = ImageWithSprite(close, "Click", crosses[1]); Stretch(pressedCross.rectTransform);
            ConfigureButton(close, defaultCross.transform, pressedCross.transform);
            Text(panel, "DisplayNameLabel", "", 92, 101, 480, 38, 24, key: "ui.lobby.nickname");
            Input(panel, "DisplayName", 92, 143, 480, 52, RoomCreationRules.DisplayNameLimit);
            Text(panel, "DisplayNameHint", "", 92, 205, 480, 32, 18, color: Muted, key: "ui.lobby.nickname_hint");
            Text(panel, "RoomNameLabel", "", 92, 247, 480, 38, 24, key: "ui.lobby.room_name");
            Input(panel, "RoomName", 92, 289, 480, 52, RoomCreationRules.RoomNameLimit);
            Text(panel, "MaxPlayersLabel", "", 92, 361, 150, 48, 24, key: "ui.lobby.max_players");
            foreach (var option in new[] { ("Two", 2, 256), ("Three", 3, 366), ("Four", 4, 476) })
            {
                RectTransform button = Button(panel, "Capacity" + option.Item1, option.Item3, 357, 96, 54,
                    "ui.lobby.capacity_" + option.Item1.ToLowerInvariant());
                Image selected = Picture(button, "Selected", "Wood UI/Sprites/Buttons/PaperBrown.png", multiplier: 1f / 6f);
                Stretch(selected.rectTransform); selected.transform.SetSiblingIndex(2);
                selected.gameObject.SetActive(option.Item2 == 4);
                if (option.Item2 == 4) button.Find("Label").GetComponent<TMP_Text>().color = Cream;
            }
            Text(panel, "CapacityHint", "最多 4 人（包含房主）", 92, 419, 480, 32, 18, color: Muted);
            Text(panel, "Error", "", 92, 445, 480, 26, 16, color: new Color32(145, 54, 42, 255));
            Button(panel, "Cancel", 92, 472, 164, 54, "ui.confirm.cancel");
            Button(panel, "Submit", 360, 472, 212, 54, "ui.lobby.create_and_enter", true);
            return root;
        }

        private static GameObject Root<T>(string name) where T : UIBase
        {
            GameObject root = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.AddComponent<T>();
            return root;
        }

        private static RectTransform Paper(Transform parent, float width, float height, float y)
        {
            Image image = Picture(parent, "Panel", "MagicalUI/Body/Paper.png", width: width, height: height, multiplier: .5f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, y * 2);
            image.raycastTarget = true;
            return rect;
        }

        private static void Header(Transform panel, string key, float x, float y)
        {
            Image ribbon = Picture(panel, "Header", "MagicalUI/Headers/Header1.png", x, y, 378, 100);
            Text(ribbon.transform, "Label", "", 24, 23, 330, 48, 36, TextAlignmentOptions.Center, Cream, key);
        }

        private static RectTransform Button(Transform parent, string name, float x, float y, float width, float height,
            string key, bool primary = false)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            Solid(rect.gameObject, Color.clear, true);
            string sprite = primary ? "Wood UI/Sprites/Buttons/PaperBrown" : "LoadUI/Button-Cream";
            Image normal = Picture(rect, "Default", sprite + ".png", multiplier: 1f / 6f); Stretch(normal.rectTransform);
            Image pressed = Picture(rect, "Click", sprite + "Pressed.png", multiplier: 1f / 6f); Stretch(pressed.rectTransform);
            ConfigureButton(rect, normal.transform, pressed.transform);
            Text(rect, "Label", "", 8, 2, width - 16, height - 5, 24, TextAlignmentOptions.Center,
                primary ? Cream : Ink, key);
            return rect;
        }

        private static void ConfigureButton(RectTransform rect, Transform normal, Transform pressed)
        {
            ButtonPlus button = rect.gameObject.AddComponent<ButtonPlus>();
            button.onClick = new UnityEngine.Events.UnityEvent();
            var serialized = new SerializedObject(button);
            serialized.FindProperty("defaultTransforms").objectReferenceValue = normal;
            serialized.FindProperty("clickTransforms").objectReferenceValue = pressed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pressed.gameObject.SetActive(false);
        }

        private static void Input(Transform parent, string name, float x, float y, float width, float height, int limit)
        {
            Image image = Picture(parent, name, "LoadUI/Button-Sand.png", x, y, width, height, .25f);
            image.raycastTarget = true;
            RectTransform viewport = Rect(image.transform, "Viewport", 16, 4, width - 32, height - 8);
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text text = Text(viewport, "Text", "", 0, 0, width - 32, height - 8, 24);
            TMP_InputField field = image.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport; field.textComponent = (TextMeshProUGUI)text;
            field.targetGraphic = image; field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            field.selectionColor = new Color(.5f, .35f, .25f, .25f);
        }

        private static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height,
            float size, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, Color? color = null, string key = null)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            TextMeshProUGUI text = key == null ? rect.gameObject.AddComponent<TextMeshProUGUI>()
                : rect.gameObject.AddComponent<LocalizedTextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size * 2;
            text.color = color ?? Ink; text.alignment = alignment; text.richText = false; text.raycastTarget = false;
            text.enableAutoSizing = true; text.fontSizeMin = Math.Min(size, 14) * 2; text.fontSizeMax = size * 2;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = value;
            if (text is LocalizedTextMeshProUGUI localized) localized.LocalizationKey = key;
            return text;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject go = new(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x * 2, -y * 2); rect.sizeDelta = new Vector2(width * 2, height * 2);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Solid(Transform parent, string name, Color color, bool raycast = false)
            => Solid(Rect(parent, name, 0, 0, 0, 0).gameObject, color, raycast);
        private static Image Solid(GameObject go, Color color, bool raycast)
        { Image image = go.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image; }
        private static Image Picture(Transform parent, string name, string asset, float x = 0, float y = 0,
            float width = 0, float height = 0, float multiplier = 1)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + asset);
            if (sprite == null) throw new InvalidOperationException("Missing existing UI sprite: " + asset);
            Image image = ImageWithSprite(Rect(parent, name, x, y, width, height), null, sprite);
            if (sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            return image;
        }
        private static Image ImageWithSprite(Transform parent, string name, Sprite sprite)
        {
            GameObject go = name == null ? parent.gameObject : Rect(parent, name, 0, 0, 0, 0).gameObject;
            Image image = go.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; return image;
        }
    }
}
