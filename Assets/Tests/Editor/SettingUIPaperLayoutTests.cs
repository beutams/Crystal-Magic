using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingUIPaperLayoutTests
{
    private const string PrefabPath = "Assets/Res/UI/SettingUI.prefab";
    private static readonly string[] VolumeNames = { "Master", "Bgm", "Sfx" };
    private static readonly string[] ButtonNames = { "LanguagePrevious", "LanguageNext", "Save", "Reset", "Back" };

    private static GameObject LoadPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }

    [Test]
    public void BindingsAndPixelSpritesResolveIncludingInactiveButtonStates()
    {
        GameObject prefab = LoadPrefab();
        var data = new SettingUIData();
        data.Bind(prefab.transform);
        foreach (FieldInfo field in typeof(SettingUIData).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType != typeof(UINode))
                continue;
            UINode node = (UINode)field.GetValue(data);
            Assert.That(node, Is.Not.Null, field.Name);
            Assert.That(node.GameObject, Is.Not.Null, field.Name);
        }
        var flatPaths = new HashSet<string>
        {
            "Overlay", "Panel/Decorations/TitleLeftLine", "Panel/Decorations/TitleRightLine", "Panel/LanguageUnderline",
        };
        foreach (string divider in new[] { "HeaderDivider", "LanguageDivider", "FooterDivider" })
        {
            flatPaths.Add("Panel/Decorations/" + divider + "/Left");
            flatPaths.Add("Panel/Decorations/" + divider + "/Right");
        }
        foreach (string name in ButtonNames)
            flatPaths.Add("Panel/" + name);
        foreach (string name in VolumeNames)
        {
            string path = "Panel/" + name + "VolumeSlider";
            flatPaths.Add(path);
            flatPaths.Add(path + "/FillArea/Fill");
            flatPaths.Add(path + "/HandleArea/Handle/GripLeft");
            flatPaths.Add(path + "/HandleArea/Handle/GripRight");
        }
        int sprites = 0;
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
            sprites++;
        }
        Assert.That(flatPaths, Is.Empty);
        Assert.That(sprites, Is.EqualTo(31));
    }

    [Test]
    public void SliderHandlesAndButtonStatesStayConnectedAndWithinThePaper()
    {
        Transform panel = LoadPrefab().transform.Find("Panel");
        Assert.That(((RectTransform)panel).sizeDelta, Is.EqualTo(new Vector2(1281, 792)));
        Assert.That(panel.GetComponentsInChildren<Slider>(true), Has.Length.EqualTo(3));
        foreach (string name in VolumeNames)
        {
            Slider slider = panel.Find(name + "VolumeSlider").GetComponent<Slider>();
            Assert.That(slider.minValue, Is.EqualTo(0));
            Assert.That(slider.maxValue, Is.EqualTo(1));
            Assert.That(slider.wholeNumbers, Is.False);
            Assert.That(slider.direction, Is.EqualTo(Slider.Direction.LeftToRight));
            Assert.That(slider.fillRect, Is.EqualTo(slider.transform.Find("FillArea/Fill")));
            Assert.That(slider.handleRect, Is.EqualTo(slider.transform.Find("HandleArea/Handle")));
            Assert.That(slider.targetGraphic, Is.EqualTo(slider.handleRect.GetComponent<Image>()));
            Assert.That(slider.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(slider.fillRect.GetComponent<Image>().raycastTarget, Is.False);
            var root = (RectTransform)slider.transform;
            var area = (RectTransform)slider.handleRect.parent;
            Assert.That(root.sizeDelta.y, Is.GreaterThanOrEqualTo(60));
            Assert.That(area.anchoredPosition.x - slider.handleRect.sizeDelta.x / 2, Is.EqualTo(0));
            Assert.That(area.anchoredPosition.x + area.sizeDelta.x + slider.handleRect.sizeDelta.x / 2, Is.EqualTo(root.sizeDelta.x));
        }
        Assert.That(panel.GetComponentsInChildren<ButtonPlus>(true), Has.Length.EqualTo(5));
        foreach (string name in ButtonNames)
        {
            Transform button = panel.Find(name);
            var serialized = new SerializedObject(button.GetComponent<ButtonPlus>());
            Assert.That(serialized.FindProperty("defaultTransforms").objectReferenceValue, Is.EqualTo(button.Find("Default")));
            Assert.That(serialized.FindProperty("clickTransforms").objectReferenceValue, Is.EqualTo(button.Find("Click")));
            foreach (string state in new[] { "Default", "Click" })
            {
                Image face = button.Find(state).GetComponent<Image>();
                Assert.That(face.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(face.raycastTarget, Is.False);
                Assert.That(face.sprite, Is.Not.Null);
            }
            var rect = (RectTransform)button;
            Assert.That(rect.anchoredPosition.x + rect.sizeDelta.x, Is.LessThan(1200));
            Assert.That(-rect.anchoredPosition.y + rect.sizeDelta.y, Is.LessThanOrEqualTo(710));
        }
    }

    [Serializable] private sealed class TranslationTable { public List<Translation> Rows; }
    [Serializable] private sealed class Translation { public string Key; public string ChineseSimplified; public string English; }

    [Test]
    public void IndependentLabelsHaveTranslationsAndFonts()
    {
        GameObject prefab = LoadPrefab();
        Assert.That(prefab.transform.Find("Panel/RowsLabel"), Is.Null);
        Assert.That(prefab.transform.Find("Panel/RowsValue"), Is.Null);
        var table = JsonUtility.FromJson<TranslationTable>(File.ReadAllText("Assets/Res/Data/LocalizationDataTable.json"));
        foreach (LocalizedTextMeshProUGUI text in prefab.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true))
        {
            Assert.That(text.font, Is.Not.Null);
            Assert.That(text.fontSharedMaterial, Is.Not.Null);
            Assert.That(text.raycastTarget, Is.False);
            if (string.IsNullOrEmpty(text.LocalizationKey))
                continue;
            Translation row = table.Rows.Single(t => t.Key == text.LocalizationKey);
            Assert.That(row.ChineseSimplified, Is.Not.Empty);
            Assert.That(row.English, Is.Not.Empty);
        }
    }

    [Test]
    public void RefreshIsSilentAndReopeningDoesNotDuplicateUserIntents()
    {
        // No controller is bound: this test never changes or saves the user's settings.
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        SettingUI view = contents.GetComponent<SettingUI>();
        var data = new SettingUIData();
        data.Bind(contents.transform);
        int[] counts = new int[8];
        view.MasterVolumeChanged += _ => counts[0]++;
        view.BgmVolumeChanged += _ => counts[1]++;
        view.SfxVolumeChanged += _ => counts[2]++;
        view.PreviousLanguageRequested += () => counts[3]++;
        view.NextLanguageRequested += () => counts[4]++;
        view.SaveRequested += () => counts[5]++;
        view.ResetRequested += () => counts[6]++;
        view.BackRequested += () => counts[7]++;
        try
        {
            view.EnsureInitialized();
            for (int cycle = 0; cycle < 2; cycle++)
            {
                view.OnOpen();
                foreach (string name in VolumeNames)
                    contents.transform.Find("Panel/" + name + "VolumeSlider").GetComponent<Slider>().value = 0.25f + cycle * 0.5f;
                foreach (string name in ButtonNames)
                    contents.transform.Find("Panel/" + name).GetComponent<ButtonPlus>().onClick.Invoke();
                view.OnClose();
                Assert.That(counts, Is.All.EqualTo(cycle + 1));
            }

            var model = new SettingUIModel();
            typeof(SettingUIModel).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model,
                new GameSettingsData { MasterVolume = 0.2f, BgmVolume = 0.45f, SfxVolume = 0.9f, Language = GameLanguage.English });
            view.BindModel(model);
            int sliderNotifications = 0;
            foreach (Slider slider in contents.GetComponentsInChildren<Slider>(true))
                slider.onValueChanged.AddListener(_ => sliderNotifications++);
            typeof(SettingUI).GetMethod("RefreshView", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(data.Panel_MasterVolumeValue.TextMeshProUGUI.text, Is.EqualTo("20%"));
            Assert.That(data.Panel_BgmVolumeValue.TextMeshProUGUI.text, Is.EqualTo("45%"));
            Assert.That(data.Panel_SfxVolumeValue.TextMeshProUGUI.text, Is.EqualTo("90%"));
            Assert.That(data.Panel_LanguageValue.TextMeshProUGUI.text, Is.EqualTo("English"));
            Assert.That(sliderNotifications, Is.Zero);
            Assert.That(counts, Is.All.EqualTo(2));
        }
        finally
        {
            view.OnClose();
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    [MenuItem("Tools/Crystal Magic/Validate Setting UI")]
    public static void ValidateConfiguredSettingUI()
    {
        var tests = new SettingUIPaperLayoutTests();
        tests.BindingsAndPixelSpritesResolveIncludingInactiveButtonStates();
        tests.SliderHandlesAndButtonStatesStayConnectedAndWithinThePaper();
        tests.IndependentLabelsHaveTranslationsAndFonts();
        tests.RefreshIsSilentAndReopeningDoesNotDuplicateUserIntents();
        Debug.Log("[SettingUI] PASS: paper layout, sprites, slider/button wiring, localization and UI intent lifecycle.");
    }
}
