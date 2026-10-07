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

public sealed class SaveUIPaperLayoutTests
{
    private const string PrefabPath = "Assets/Res/UI/SaveUI.prefab";

    private static GameObject LoadPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }

    [Test]
    public void ThreeFixedRowsHaveBindingsAndSaveButtonsOutsideOccupiedOnlyContent()
    {
        GameObject prefab = LoadPrefab();
        Assert.That(prefab.GetComponent<SaveUI>(), Is.Not.Null);
        Assert.That(SaveUIModel.SlotCount, Is.EqualTo(LoadUIModel.SlotCount));
        Assert.That(new SaveUIModel().SaveRecords, Has.Length.EqualTo(3));
        Assert.That(prefab.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<LayoutGroup>(true), Is.Empty);
        var data = new SaveUIData();
        data.Bind(prefab.transform);
        AssertBindings(data);
        Transform slots = prefab.transform.Find("Panel/Slots");
        Assert.That(slots.childCount, Is.EqualTo(3));
        Assert.That(prefab.GetComponentsInChildren<SaveUI_SaveItemView>(true), Has.Length.EqualTo(3));
        for (int i = 0; i < slots.childCount; i++)
        {
            Transform row = slots.GetChild(i);
            var rowData = new SaveUI_SaveItemData();
            rowData.Bind(row);
            AssertBindings(rowData);
            Assert.That(rowData.Save.GameObject.transform.parent, Is.EqualTo(row));
            Assert.That(rowData.Save.ButtonPlus, Is.Not.Null);
            Assert.That(rowData.Open_Delete.ButtonPlus, Is.Not.Null);
            Assert.That(row.GetComponent<ButtonPlus>(), Is.Null);
            var rect = (RectTransform)row;
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(1053, 126)));
            Assert.That(-rect.anchoredPosition.y + rect.sizeDelta.y, Is.LessThanOrEqualTo(442));
            RectTransform planets = rowData.Close_Planets.RectTransform;
            Assert.That(planets.anchoredPosition.x + planets.sizeDelta.x,
                Is.LessThan(rowData.Save.RectTransform.anchoredPosition.x));
        }
    }

    private static void AssertBindings(UIData data)
    {
        foreach (FieldInfo field in data.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType != typeof(UINode))
                continue;
            UINode node = (UINode)field.GetValue(data);
            Assert.That(node, Is.Not.Null, field.Name);
            Assert.That(node.GameObject, Is.Not.Null, field.Name);
        }
    }

    [Test]
    public void AllVisibleAndHiddenImagesHaveValidSpritesExceptExplicitFlatGraphics()
    {
        GameObject prefab = LoadPrefab();
        var flatPaths = new HashSet<string>
        {
            "Back", "Panel/Decorations/TitleLeftLine", "Panel/Decorations/TitleRightLine",
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
            flatPaths.Add(row + "/Save");
        }

        int spriteCount = 0;
        foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(image.transform, prefab.transform);
            if (flatPaths.Remove(path))
            {
                Assert.That(image.sprite, Is.Null, path);
                continue;
            }
            Assert.That(image.sprite, Is.Not.Null, path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(image.sprite));
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            spriteCount++;
        }
        Assert.That(flatPaths, Is.Empty);
        Assert.That(spriteCount, Is.EqualTo(41));
    }

    [Serializable] private sealed class TranslationTable { public List<Translation> Rows; }
    [Serializable] private sealed class Translation { public string Key; public string ChineseSimplified; public string English; }

    [Test]
    public void ButtonStatesAndTranslationsMatchThePaperStyle()
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
        Assert.That(prefab.GetComponentsInChildren<ButtonPlus>(true), Has.Length.EqualTo(7));
        foreach (ButtonPlus button in prefab.GetComponentsInChildren<ButtonPlus>(true))
        {
            if (button.name == "Delete")
                continue;
            var serialized = new SerializedObject(button);
            Assert.That(serialized.FindProperty("defaultTransforms").objectReferenceValue, Is.EqualTo(button.transform.Find("Default")));
            Assert.That(serialized.FindProperty("clickTransforms").objectReferenceValue, Is.EqualTo(button.transform.Find("Click")));
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
            foreach (string state in new[] { "Default", "Click" })
            {
                Image face = button.transform.Find(state).GetComponent<Image>();
                Assert.That(face.sprite, Is.Not.Null);
                Assert.That(face.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(face.raycastTarget, Is.False);
                Assert.That(face.pixelsPerUnitMultiplier, Is.EqualTo(1f / 3f).Within(0.00001f));
                Assert.That(AssetDatabase.GetAssetPath(face.sprite), Does.StartWith("Assets/Res/Sprites/UISprites/LoadUI/"));
            }
        }
    }

    [Test]
    public void EmptyAndOccupiedSlotsEmitTheRightIntentsWithoutDuplicateSubscriptions()
    {
        // Preview contents only. Never call a save controller or write/delete user saves.
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        SaveUI panel = contents.GetComponent<SaveUI>();
        SaveUI_SaveItemView[] rows = contents.GetComponentsInChildren<SaveUI_SaveItemView>(true);
        var savedSlots = new List<int>();
        var deletedSlots = new List<int>();
        int backed = 0;
        panel.SaveItemClicked += savedSlots.Add;
        panel.SaveItemDeleteClicked += deletedSlots.Add;
        panel.BackClicked += () => backed++;
        try
        {
            panel.EnsureInitialized();
            foreach (SaveUI_SaveItemView row in rows)
            {
                Lifecycle(row, "OnDisable");
                Lifecycle(row, "OnEnable");
            }
            for (int cycle = 0; cycle < 2; cycle++)
            {
                panel.OnOpen();
                panel.RenderSlots(new SaveRecord[] { null, new SaveRecord { SaveIndex = 1, StashMoney = 2480 }, null });
                Assert.That(rows[1].UI.Open_MaxFloor.TextMeshProUGUI.text, Is.EqualTo("—"));
                Assert.That(rows[1].UI.Open_TotalRuns.TextMeshProUGUI.text, Is.EqualTo("—"));
                Assert.That(rows[1].UI.Open_Money.TextMeshProUGUI.text, Is.EqualTo(2480.ToString("N0", CultureInfo.CurrentCulture)));
                for (int i = 0; i < rows.Length; i++)
                {
                    Assert.That(rows[i].UI.Badge_Index.TextMeshProUGUI.text, Is.EqualTo((i + 1).ToString("00")));
                    Assert.That(rows[i].UI.Open.GameObject.activeSelf, Is.EqualTo(i == 1));
                    Assert.That(rows[i].UI.Close.GameObject.activeSelf, Is.EqualTo(i != 1));
                    Assert.That(rows[i].UI.Save.GameObject.activeInHierarchy, Is.True);
                    string label = LocalizationComponent.Resolve(i == 1 ? "ui.save.overwrite" : "ui.save.create");
                    Assert.That(rows[i].UI.Save_Default_Text.TextMeshProUGUI.text, Is.EqualTo(label));
                    Assert.That(rows[i].UI.Save_Click_Text.TextMeshProUGUI.text, Is.EqualTo(label));
                    rows[i].UI.Save.ButtonPlus.onClick.Invoke();
                    rows[i].UI.Open_Delete.ButtonPlus.onClick.Invoke();
                }
                contents.transform.Find("Back").GetComponent<ButtonPlus>().onClick.Invoke();
                panel.OnClose();
                Assert.That(savedSlots.Count, Is.EqualTo((cycle + 1) * 3));
                Assert.That(deletedSlots.Count, Is.EqualTo(cycle + 1));
                Assert.That(backed, Is.EqualTo(cycle + 1));
            }
            Assert.That(savedSlots, Is.EqualTo(new[] { 0, 1, 2, 0, 1, 2 }));
            Assert.That(deletedSlots, Is.EqualTo(new[] { 1, 1 }));
            panel.RenderSlots(null);
            Assert.That(rows[1].UI.Close.GameObject.activeSelf, Is.True);
            Assert.That(rows[1].UI.Save_Default_Text.TextMeshProUGUI.text, Is.EqualTo(LocalizationComponent.Resolve("ui.save.create")));
        }
        finally
        {
            panel.OnClose();
            foreach (SaveUI_SaveItemView row in rows)
                Lifecycle(row, "OnDisable");
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void Lifecycle(SaveUI_SaveItemView view, string method)
    {
        typeof(SaveUI_SaveItemView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    }

    [MenuItem("Tools/Crystal Magic/Validate Save UI")]
    public static void ValidateConfiguredSaveUI()
    {
        var tests = new SaveUIPaperLayoutTests();
        tests.ThreeFixedRowsHaveBindingsAndSaveButtonsOutsideOccupiedOnlyContent();
        tests.AllVisibleAndHiddenImagesHaveValidSpritesExceptExplicitFlatGraphics();
        tests.ButtonStatesAndTranslationsMatchThePaperStyle();
        tests.EmptyAndOccupiedSlotsEmitTheRightIntentsWithoutDuplicateSubscriptions();
        Debug.Log("[SaveUI] PASS: fixed layout, Sprite references, buttons, localization and row lifecycle.");
    }
}
