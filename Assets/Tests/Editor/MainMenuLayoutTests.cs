using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuLayoutTests
{
    private const string PrefabPath = "Assets/Res/UI/MainMenuUI.prefab";
    private const string BackgroundPath = "Assets/Res/Sprites/UISprites/MainMenu/MainMenuBackground.png";
    private static readonly string[] ButtonNames = { "Start", "Load", "Config", "Exit" };

    private static GameObject Load()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }

    [Test]
    public void ExistingBindingsAndButtonStatesRemainConnected()
    {
        var prefab = Load();
        Assert.That(prefab.GetComponent<MainMenuUI>(), Is.Not.Null);
        var data = new MainMenuUIData();
        Assert.DoesNotThrow(() => data.Bind(prefab.transform));
        Assert.That(new[] { data.MenuBack_Start.ButtonPlus, data.MenuBack_Load.ButtonPlus,
            data.MenuBack_Config.ButtonPlus, data.MenuBack_Exit.ButtonPlus }, Has.None.Null);
        Assert.That(prefab.GetComponentsInChildren<ButtonPlus>(true), Has.Length.EqualTo(4));
        foreach (string name in ButtonNames)
        {
            Transform button = prefab.transform.Find("MenuBack/" + name);
            var serialized = new SerializedObject(button.GetComponent<ButtonPlus>());
            Assert.That(serialized.FindProperty("defaultTransforms").objectReferenceValue, Is.EqualTo(button.Find("Default")));
            Assert.That(serialized.FindProperty("clickTransforms").objectReferenceValue, Is.EqualTo(button.Find("Select")));
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
            foreach (string state in new[] { "Default", "Select" })
            {
                Image face = button.Find(state).GetComponent<Image>();
                Assert.That(face.sprite, Is.Not.Null);
                Assert.That(face.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(face.raycastTarget, Is.False, "Only the stable button root should receive raycasts.");
            }
        }
    }

    [Test]
    public void BackgroundIsSeparateUncompressedAndCoversViewport()
    {
        var prefab = Load();
        Transform background = prefab.transform.GetChild(0);
        Assert.That(background.name, Is.EqualTo("Background"));
        var image = background.GetComponent<Image>();
        Assert.That(image.sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath)));
        Assert.That(image.sprite, Is.Not.Null);
        Assert.That(image.raycastTarget, Is.False);
        var fitter = background.GetComponent<AspectRatioFitter>();
        Assert.That(fitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
        Assert.That(fitter.aspectRatio, Is.EqualTo(1672f / 941f).Within(0.00001f));
        var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(prefab.transform.Find("MenuBack").GetComponent<Image>(), Is.Null);
        Assert.That(prefab.transform.Find("MenuBack/Menu").GetComponent<Image>(), Is.Null);
    }

    [Test]
    public void CompactMenuFitsSupportedLandscapeAspectRatios()
    {
        var root = Load().transform;
        var menu = (RectTransform)root.Find("MenuBack");
        Assert.That(root.localScale, Is.EqualTo(Vector3.one));
        Assert.That(menu.localScale, Is.EqualTo(Vector3.one));
        Assert.That(menu.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(menu.anchorMax, Is.EqualTo(Vector2.zero));
        Assert.That(menu.pivot, Is.EqualTo(Vector2.zero));
        foreach (Vector2 screen in new[] { new Vector2(1280, 720), new Vector2(1920, 1080),
            new Vector2(2560, 1440), new Vector2(3440, 1440), new Vector2(1920, 1200), new Vector2(1024, 768) })
        {
            // UIGroup uses CanvasScaler.Expand at the existing 2560x1440 reference.
            float scale = Mathf.Min(screen.x / 2560, screen.y / 1440);
            var menuTopRight = (menu.anchoredPosition + menu.sizeDelta) * scale;
            Assert.That(menuTopRight.x, Is.LessThan(screen.x * 0.4f));
            Assert.That(menuTopRight.y, Is.LessThan(screen.y));
            float priorBottom = float.PositiveInfinity;
            foreach (string name in ButtonNames)
            {
                var button = (RectTransform)menu.Find(name);
                Assert.That(button.anchorMin, Is.EqualTo(new Vector2(0.5f, 0)));
                Assert.That(button.anchorMax, Is.EqualTo(button.anchorMin));
                Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(416, 80)));
                float top = button.anchoredPosition.y + button.sizeDelta.y / 2;
                Assert.That(top, Is.LessThan(priorBottom));
                priorBottom = button.anchoredPosition.y - button.sizeDelta.y / 2;
                Assert.That(priorBottom, Is.GreaterThanOrEqualTo(0));
            }
        }
    }

    [Serializable] private sealed class TranslationTable { public List<Translation> Rows; }
    [Serializable] private sealed class Translation { public string Key; public string ChineseSimplified; public string English; }

    [Test]
    public void TitlesAndAllButtonStatesHaveValidLocalizationAndFonts()
    {
        var prefab = Load();
        var table = JsonUtility.FromJson<TranslationTable>(File.ReadAllText("Assets/Res/Data/LocalizationDataTable.json"));
        foreach (var text in prefab.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true))
        {
            Assert.That(text.raycastTarget, Is.False);
            Assert.That(text.font, Is.Not.Null);
            Assert.That(text.fontSharedMaterial, Is.Not.Null);
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            if (string.IsNullOrEmpty(text.LocalizationKey))
            {
                Assert.That(text.text, Is.EqualTo("CRYSTAL MAGIC"));
                continue;
            }
            Translation row = table.Rows.Single(t => t.Key == text.LocalizationKey);
            Assert.That(row.ChineseSimplified, Is.Not.Empty);
            Assert.That(row.English, Is.Not.Empty);
            Assert.That(text.font.HasCharacters(row.ChineseSimplified), Is.True, row.Key);
        }
    }

    [MenuItem("Tools/Crystal Magic/Validate Main Menu")]
    public static void ValidateConfiguredMainMenu()
    {
        var tests = new MainMenuLayoutTests();
        tests.ExistingBindingsAndButtonStatesRemainConnected();
        tests.BackgroundIsSeparateUncompressedAndCoversViewport();
        tests.CompactMenuFitsSupportedLandscapeAspectRatios();
        tests.TitlesAndAllButtonStatesHaveValidLocalizationAndFonts();
        Directory.CreateDirectory("Temp/MainMenuValidation");
        File.WriteAllText("Temp/MainMenuValidation/editor-validation.txt", "PASS: 4 main-menu asset/layout tests. " + DateTime.UtcNow.ToString("O"));
        Debug.Log("[MainMenu] PASS: prefab bindings, sprite imports, layout bounds and localization.");
    }
}
