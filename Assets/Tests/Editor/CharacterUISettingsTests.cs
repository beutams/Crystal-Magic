using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CharacterUISettingsTests
{
    private const string Path = "Assets/Res/UI/CharacterUI.prefab";
    private GameObject _contents;
    private CharacterUI_SettingView _view;

    [SetUp]
    public void SetUp()
    {
        _contents = PrefabUtility.LoadPrefabContents(Path);
        _view = _contents.transform.Find("Setting").GetComponent<CharacterUI_SettingView>();
        _view.InitializeBindings();
    }

    [TearDown]
    public void TearDown() => PrefabUtility.UnloadPrefabContents(_contents);

    private static void AssertBindings(UIData data)
    {
        foreach (FieldInfo field in data.GetType().GetFields())
            if (field.FieldType == typeof(UINode))
                Assert.That((UINode)field.GetValue(data), Is.Not.Null, field.Name);
    }

    [Test]
    public void SettingIsAnIndependentBookPageWithResolvedBindingsAndNoFooterButtons()
    {
        var main = new CharacterUIData();
        main.Bind(_contents.transform);
        AssertBindings(main);
        AssertBindings(_view.UI);
        Assert.That(_view.transform.parent, Is.EqualTo(_contents.transform));
        Assert.That(_view.gameObject.activeSelf, Is.False);
        Assert.That(_view.GetComponentsInChildren<UIBase>(true), Is.Empty);
        Assert.That(_view.GetComponentsInChildren<Canvas>(true), Is.Empty);
        Assert.That(_view.GetComponentsInChildren<ButtonPlus>(true), Has.Length.EqualTo(6));
        Assert.That(_view.UI.Navigation_Save.GameObject.transform.parent, Is.EqualTo(_view.UI.Navigation.GameObject.transform));
        Assert.That(_view.UI.Navigation_ReturnMainMenu.GameObject.transform.parent, Is.EqualTo(_view.UI.Navigation.GameObject.transform));
        Assert.That(_view.transform.Find("Game"), Is.Null);
        Assert.That(_view.UI.Navigation.GameObject.transform.Find("Game"), Is.Null);
        Assert.That(_view.GetComponentsInChildren<CharacterUI_SettingVolumeView>(true), Has.Length.EqualTo(3));
        foreach (string name in new[] { "Save", "Reset", "Back", "Resume", "Continue" })
            Assert.That(_view.transform.Find(name), Is.Null);
        Assert.That(_contents.transform.Find("BG"), Is.Not.Null);
        Assert.That(_contents.transform.Find("Equip"), Is.Not.Null);
        Assert.That(_contents.transform.Find("Skill"), Is.Not.Null);
        Assert.That(_contents.transform.Find("Handbook"), Is.Not.Null, "The authored handbook is a sibling page, not part of Settings.");
    }

    [Test]
    public void AllArtworkIsExistingBookV1AndImportedAsPixelSprites()
    {
        foreach (Image image in _view.GetComponentsInChildren<Image>(true))
        {
            Assert.That(image.sprite, Is.Not.Null, image.name);
            string path = AssetDatabase.GetAssetPath(image.sprite);
            Assert.That(path, Does.StartWith("Assets/Res/Sprites/UISprites/BookV1/"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
        }
    }

    [Test]
    public void BothSectionsRenderSilentlyWithCorrectLanguageAndPercentages()
    {
        int changed = 0;
        _view.ValueChanged += (_, _) => changed++;
        var settings = new GameSettingsData
        {
            MasterVolume = 0f, BgmVolume = 0.6f, SfxVolume = 1f,
            Language = GameLanguage.English,
        };
        for (int section = 0; section < 2; section++)
        {
            _view.Render(settings, (CharacterSettingsSection)section);
            Assert.That(_view.UI.Audio.GameObject.activeSelf, Is.EqualTo(section == 0));
            Assert.That(_view.UI.Language.GameObject.activeSelf, Is.EqualTo(section == 1));
        }
        Assert.That(changed, Is.Zero);
        Assert.That(_view.UI.Language_Chinese_Selected.GameObject.activeSelf, Is.False);
        Assert.That(_view.UI.Language_English_Selected.GameObject.activeSelf, Is.True);
        var master = _view.UI.Audio_MasterVolume.GameObject.GetComponent<CharacterUI_SettingVolumeView>();
        var music = _view.UI.Audio_BgmVolume.GameObject.GetComponent<CharacterUI_SettingVolumeView>();
        Assert.That(master.UI.Value.TextMeshProUGUI.text, Is.EqualTo("0%"));
        Assert.That(music.UI.Value.TextMeshProUGUI.text, Is.EqualTo("60%"));
        Assert.That(music.UI.Slider.Slider.value, Is.EqualTo(0.6f));
        Assert.That(music.UI.Slider_Dot6.Image.sprite, Is.Not.EqualTo(music.UI.Slider_Dot7.Image.sprite));
        Assert.That(master.UI.Slider_Dot1.Image.sprite, Is.EqualTo(master.UI.Slider_Dot10.Image.sprite));
    }

    [Test]
    public void RebindingDoesNotDuplicateIntentsAndEverySliderHasACommitGesture()
    {
        _view.InitializeBindings();
        _view.InitializeBindings();
        int values = 0, commits = 0, sections = 0, languages = 0;
        CharacterSettingValue field = CharacterSettingValue.MasterVolume;
        _view.ValueChanged += (which, _) => { values++; field = which; };
        _view.EditCompleted += () => commits++;
        _view.SectionRequested += _ => sections++;
        _view.LanguageRequested += _ => languages++;
        CharacterUI_SettingVolumeView[] rows =
        {
            _view.UI.Audio_MasterVolume.GameObject.GetComponent<CharacterUI_SettingVolumeView>(),
            _view.UI.Audio_BgmVolume.GameObject.GetComponent<CharacterUI_SettingVolumeView>(),
            _view.UI.Audio_SfxVolume.GameObject.GetComponent<CharacterUI_SettingVolumeView>(),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            AssertBindings(rows[i].UI);
            var serialized = new SerializedObject(rows[i]);
            Assert.That(serialized.FindProperty("_activeDot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_idleDot").objectReferenceValue, Is.Not.Null);
            var slider = (CharacterUI_SettingSlider)rows[i].UI.Slider.Slider;
            Assert.That(slider.minValue, Is.Zero);
            Assert.That(slider.maxValue, Is.EqualTo(1f));
            Assert.That(slider.wholeNumbers, Is.False);
            Assert.That(slider.targetGraphic.raycastTarget, Is.True);
            Assert.That(slider.handleRect.parent, Is.EqualTo(slider.transform));
            slider.value = 0.25f;
            Assert.That(values, Is.EqualTo(i + 1));
            Assert.That(field, Is.EqualTo((CharacterSettingValue)i));
            slider.OnPointerUp(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Assert.That(commits, Is.EqualTo(i + 1));
        }
        _view.UI.Navigation_Audio.ButtonPlus.onClick.Invoke();
        _view.UI.Navigation_Language.ButtonPlus.onClick.Invoke();
        _view.UI.Language_Chinese.ButtonPlus.onClick.Invoke();
        _view.UI.Language_English.ButtonPlus.onClick.Invoke();
        Assert.That(sections, Is.EqualTo(2));
        Assert.That(languages, Is.EqualTo(2));
    }

    [Test]
    public void SaveAndReturnAreIndependentActionsAndDoNotSwitchSettingsSections()
    {
        _view.InitializeBindings();
        _view.InitializeBindings();
        int saves = 0, returns = 0, sections = 0;
        _view.SaveRequested += () => saves++;
        _view.ReturnMainMenuRequested += () => returns++;
        _view.SectionRequested += _ => sections++;
        _view.UI.Navigation_Save.ButtonPlus.onClick.Invoke();
        _view.UI.Navigation_ReturnMainMenu.ButtonPlus.onClick.Invoke();
        Assert.That(saves, Is.EqualTo(1));
        Assert.That(returns, Is.EqualTo(1));
        Assert.That(sections, Is.Zero);
        UINode[] navigation = { _view.UI.Navigation_Audio, _view.UI.Navigation_Language,
            _view.UI.Navigation_Save, _view.UI.Navigation_ReturnMainMenu };
        for (int i = 1; i < navigation.Length; i++)
            Assert.That(navigation[i - 1].RectTransform.anchoredPosition.y - navigation[i].RectTransform.anchoredPosition.y,
                Is.GreaterThan(navigation[i].RectTransform.rect.height), "Left entries must not overlap.");
    }

    [Serializable] private sealed class Table { public List<Row> Rows; }
    [Serializable] private sealed class Row { public string Key; public string ChineseSimplified; public string English; }

    [Test]
    public void SettingLabelsHaveBothTranslationsAndTheExistingFont()
    {
        var table = JsonUtility.FromJson<Table>(DataFileUtility.ReadJsonText("Assets/Res/Data/LocalizationDataTable.json"));
        foreach (LocalizedTextMeshProUGUI text in _view.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true))
        {
            Assert.That(text.font, Is.Not.Null);
            Assert.That(text.raycastTarget, Is.False);
            if (string.IsNullOrEmpty(text.LocalizationKey)) continue;
            Row row = table.Rows.Single(r => r.Key == text.LocalizationKey);
            Assert.That(row.ChineseSimplified, Is.Not.Empty);
            Assert.That(row.English, Is.Not.Empty);
        }
    }
}
