using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class LoadUIPaperLayoutTests
{
    private const string PrefabPath = "Assets/Res/UI/LoadUI.prefab";
    private static readonly string[] DecorativeSpritePaths =
    {
        "Assets/Res/Sprites/UISprites/MagicalUI/BodyAssets/Map Assets/compass.png",
        "Assets/Res/Sprites/UISprites/MagicalUI/BodyAssets/Scroll Assets/Star.png",
        "Assets/Res/Sprites/UISprites/MagicalUI/BodyAssets/Scroll Assets/Planets1.png",
        "Assets/Res/Sprites/UISprites/BookUI/Unique/Icon Container/4.png",
        "Assets/Res/Sprites/UISprites/BookUI/Unique/Icon Container/6.png",
    };

    private static GameObject LoadPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }

    [Test]
    public void ThreeAuthoredRowsReplaceTheScrollingTemplate()
    {
        GameObject prefab = LoadPrefab();
        Assert.That(LoadUIModel.SlotCount, Is.EqualTo(3));
        Assert.That(prefab.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<LayoutGroup>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<LoadUI_SaveItemView>(true), Has.Length.EqualTo(3));
        var data = new LoadUIData();
        data.Bind(prefab.transform);
        AssertBindings(data);
        Assert.That(data.Back.ButtonPlus, Is.Not.Null);
        Transform slots = prefab.transform.Find("Panel/Slots");
        Assert.That(slots.childCount, Is.EqualTo(3));
        for (int i = 0; i < slots.childCount; i++)
        {
            Transform row = slots.GetChild(i);
            Assert.That(row.name, Is.EqualTo("Slot" + (i + 1)));
            var rowData = new LoadUI_SaveItemData();
            rowData.Bind(row);
            AssertBindings(rowData);
            Assert.That(rowData.Open_Read.ButtonPlus, Is.Not.Null);
            Assert.That(rowData.Open_Delete.ButtonPlus, Is.Not.Null);
            var rect = (RectTransform)row;
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(1053, 126)));
            Assert.That(-rect.anchoredPosition.y + rect.sizeDelta.y, Is.LessThanOrEqualTo(442));
            Assert.That(row.GetComponent<ButtonPlus>(), Is.Null, "Only Read, not the whole row, loads a save.");
        }
    }

    private static void AssertBindings(UIData data)
    {
        foreach (FieldInfo field in data.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType != typeof(UINode))
                continue;
            UINode node = (UINode)field.GetValue(data);
            Assert.That(node.GameObject, Is.Not.Null, field.Name);
        }
    }

    [Test]
    public void ButtonsKeepLightSlicedFacesAndStableHitTargets()
    {
        GameObject prefab = LoadPrefab();
        Assert.That(prefab.GetComponentsInChildren<ButtonPlus>(true), Has.Length.EqualTo(7));
        foreach (ButtonPlus button in prefab.GetComponentsInChildren<ButtonPlus>(true))
        {
            if (button.name == "Delete")
            {
                Assert.That(button.GetComponent<LocalizedTextMeshProUGUI>().raycastTarget, Is.True);
                continue;
            }

            var serialized = new SerializedObject(button);
            Assert.That(serialized.FindProperty("defaultTransforms").objectReferenceValue,
                Is.EqualTo(button.transform.Find("Default")));
            Assert.That(serialized.FindProperty("clickTransforms").objectReferenceValue,
                Is.EqualTo(button.transform.Find("Click")));
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
            foreach (string state in new[] { "Default", "Click" })
            {
                Image face = button.transform.Find(state).GetComponent<Image>();
                Assert.That(face.sprite, Is.Not.Null);
                Assert.That(face.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(face.raycastTarget, Is.False);
                Assert.That(face.pixelsPerUnitMultiplier, Is.EqualTo(1f / 3f).Within(0.00001f));
                string spritePath = AssetDatabase.GetAssetPath(face.sprite);
                Assert.That(spritePath, Does.StartWith("Assets/Res/Sprites/UISprites/LoadUI/"));
                var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }
    }

    [Serializable] private sealed class TranslationTable { public List<Translation> Rows; }
    [Serializable] private sealed class Translation { public string Key; public string ChineseSimplified; public string English; }

    [Test]
    public void StaticLabelsHaveTranslationsAndAllDecorationsHaveSprites()
    {
        GameObject prefab = LoadPrefab();
        var table = JsonUtility.FromJson<TranslationTable>(File.ReadAllText("Assets/Res/Data/LocalizationDataTable.json"));
        foreach (LocalizedTextMeshProUGUI text in prefab.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true))
        {
            Assert.That(text.font, Is.Not.Null);
            Assert.That(text.fontSharedMaterial, Is.Not.Null);
            if (string.IsNullOrEmpty(text.LocalizationKey))
                continue;
            Translation row = table.Rows.Single(t => t.Key == text.LocalizationKey);
            Assert.That(row.ChineseSimplified, Is.Not.Empty);
            Assert.That(row.English, Is.Not.Empty);
        }

        foreach (Image image in prefab.transform.Find("Panel/Decorations").GetComponentsInChildren<Image>(true))
        {
            // Flat separator lines deliberately use Image's white texture.
            if (image.name.Contains("Line") || image.name == "Left" || image.name == "Right")
                continue;
            Assert.That(image.sprite, Is.Not.Null, image.name);
            Assert.That(image.raycastTarget, Is.False);
        }
    }

    [Test]
    public void DecorativeSpritesUsePixelArtImportSettings()
    {
        foreach (string path in DecorativeSpritePaths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
            Assert.That(importer.alphaIsTransparency, Is.True, path);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True, path);
            Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(path)), path);
            Assert.That(fileId, Is.EqualTo(21300000L), "Prefab Sprite subasset ID: " + path);
        }
    }

    [Test]
    public void EveryImageIncludingHiddenStatesResolvesOrIsAnExplicitFlatGraphic()
    {
        GameObject prefab = LoadPrefab();
        var flatPaths = new HashSet<string>
        {
            "Back",
            "Panel/Decorations/TitleLeftLine",
            "Panel/Decorations/TitleRightLine",
        };
        foreach (string divider in new[] { "HeaderDivider", "FirstDivider", "SecondDivider", "FooterDivider" })
        {
            flatPaths.Add("Panel/Decorations/" + divider + "/Left");
            flatPaths.Add("Panel/Decorations/" + divider + "/Right");
        }
        for (int i = 1; i <= 3; i++)
        {
            string row = "Panel/Slots/Slot" + i;
            flatPaths.Add(row);
            flatPaths.Add(row + "/Highlight");
            flatPaths.Add(row + "/Open/Read");
        }

        int spriteCount = 0;
        foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(image.transform, prefab.transform);
            if (flatPaths.Remove(path))
            {
                Assert.That(image.sprite, Is.Null, "Intentional flat graphic: " + path);
                continue;
            }
            Assert.That(image.sprite, Is.Not.Null, "Missing Sprite: " + path);
            spriteCount++;
        }
        Assert.That(flatPaths, Is.Empty, "The expected flat graphic hierarchy changed.");
        Assert.That(spriteCount, Is.EqualTo(41));
    }

    [Test]
    public void MissingStatisticsAreUnknownRatherThanInventedZeroes()
    {
        var record = new SaveRecord();
        Assert.That(record.MaxFloor, Is.EqualTo(-1));
        Assert.That(record.TotalRuns, Is.EqualTo(-1));
    }

    [Test]
    public void RowsRefreshEmptyStatesAndClicksDoNotDuplicateAfterReopening()
    {
        // Isolated prefab contents only: never open a real save or touch user save files.
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        LoadUI panel = contents.GetComponent<LoadUI>();
        LoadUI_SaveItemView[] rows = contents.GetComponentsInChildren<LoadUI_SaveItemView>(true);
        int loaded = 0, deleted = 0, backed = 0;
        panel.SaveItemClicked += index => { Assert.That(index, Is.EqualTo(1)); loaded++; };
        panel.SaveItemDeleteClicked += index => { Assert.That(index, Is.EqualTo(1)); deleted++; };
        panel.BackClicked += () => backed++;
        try
        {
            panel.EnsureInitialized();
            // MonoBehaviour callbacks do not normally run in EditMode; explicitly pair them.
            foreach (LoadUI_SaveItemView row in rows)
            {
                Lifecycle(row, "OnDisable");
                Lifecycle(row, "OnEnable");
            }
            var record = new SaveRecord { SaveIndex = 1, StashMoney = 2480 };
            for (int cycle = 0; cycle < 2; cycle++)
            {
                panel.OnOpen();
                panel.RenderSlots(new SaveRecord[] { null, record, null });
                Assert.That(rows[1].UI.Open_MaxFloor.TextMeshProUGUI.text, Is.EqualTo("—"));
                Assert.That(rows[1].UI.Open_TotalRuns.TextMeshProUGUI.text, Is.EqualTo("—"));
                Assert.That(rows[1].UI.Open_Money.TextMeshProUGUI.text,
                    Is.EqualTo(2480.ToString("N0", CultureInfo.CurrentCulture)));
                for (int i = 0; i < rows.Length; i++)
                {
                    Assert.That(rows[i].UI.Badge_Index.TextMeshProUGUI.text, Is.EqualTo((i + 1).ToString("00")));
                    Assert.That(rows[i].UI.Open.GameObject.activeSelf, Is.EqualTo(i == 1));
                    Assert.That(rows[i].UI.Close.GameObject.activeSelf, Is.EqualTo(i != 1));
                    rows[i].UI.Open_Read.ButtonPlus.onClick.Invoke();
                    rows[i].UI.Open_Delete.ButtonPlus.onClick.Invoke();
                }
                contents.transform.Find("Back").GetComponent<ButtonPlus>().onClick.Invoke();
                panel.OnClose();
                Assert.That(loaded, Is.EqualTo(cycle + 1));
                Assert.That(deleted, Is.EqualTo(cycle + 1));
                Assert.That(backed, Is.EqualTo(cycle + 1));
            }
            panel.RenderSlots(null);
            rows[1].UI.Open_Read.ButtonPlus.onClick.Invoke();
            rows[1].UI.Open_Delete.ButtonPlus.onClick.Invoke();
            Assert.That(loaded, Is.EqualTo(2));
            Assert.That(deleted, Is.EqualTo(2));
        }
        finally
        {
            panel.OnClose();
            foreach (LoadUI_SaveItemView row in rows)
                Lifecycle(row, "OnDisable");
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void Lifecycle(LoadUI_SaveItemView view, string method)
    {
        typeof(LoadUI_SaveItemView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    }

    [MenuItem("Tools/Crystal Magic/Validate Load UI")]
    public static void ValidateConfiguredLoadUI()
    {
        var tests = new LoadUIPaperLayoutTests();
        tests.ThreeAuthoredRowsReplaceTheScrollingTemplate();
        tests.ButtonsKeepLightSlicedFacesAndStableHitTargets();
        tests.StaticLabelsHaveTranslationsAndAllDecorationsHaveSprites();
        tests.DecorativeSpritesUsePixelArtImportSettings();
        tests.EveryImageIncludingHiddenStatesResolvesOrIsAnExplicitFlatGraphic();
        tests.MissingStatisticsAreUnknownRatherThanInventedZeroes();
        tests.RowsRefreshEmptyStatesAndClicksDoNotDuplicateAfterReopening();
        Debug.Log("[LoadUI] PASS: seven prefab, Sprite import, binding, localization and row lifecycle checks.");
    }
}
