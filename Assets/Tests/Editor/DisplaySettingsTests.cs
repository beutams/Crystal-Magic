using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class DisplaySettingsTests
{
    [Test]
    public void OldSettingsAdoptCurrentWindowWithoutDiscardingAudioOrLanguage()
    {
        GameSettingsData settings = JsonUtility.FromJson<GameSettingsData>("{\"MasterVolume\":0.4,\"Language\":1}");
        GameDisplaySettings.Resolve(settings, new(1920, 1080), new(1600, 900), false);
        Assert.That(settings.ResolutionWidth, Is.EqualTo(1600));
        Assert.That(settings.ResolutionHeight, Is.EqualTo(900));
        Assert.That(settings.Fullscreen, Is.False);
        Assert.That(settings.MasterVolume, Is.EqualTo(.4f));
        Assert.That(settings.Language, Is.EqualTo(GameLanguage.English));
    }

    [Test]
    public void DisplaySettingsRoundTripAndCloneIndependently()
    {
        var settings = new GameSettingsData { ResolutionWidth = 2560, ResolutionHeight = 1440, Fullscreen = false };
        var restored = JsonUtility.FromJson<GameSettingsData>(JsonUtility.ToJson(settings));
        Assert.That(restored.ResolutionWidth, Is.EqualTo(2560));
        Assert.That(restored.ResolutionHeight, Is.EqualTo(1440));
        Assert.That(restored.Fullscreen, Is.False);
        GameSettingsData copy = restored.Clone();
        copy.ResolutionWidth = 1280;
        copy.Fullscreen = true;
        Assert.That(restored.ResolutionWidth, Is.EqualTo(2560));
        Assert.That(restored.Fullscreen, Is.False);
    }

    [TestCase(0, 1080)]
    [TestCase(-1, 900)]
    [TestCase(100000, 100000)]
    public void InvalidResolutionRecoversToCurrentWindow(int width, int height)
    {
        var settings = new GameSettingsData { ResolutionWidth = width, ResolutionHeight = height };
        GameDisplaySettings.Resolve(settings, new(1920, 1080), new(1280, 720), false);
        Assert.That(settings.ResolutionWidth, Is.EqualTo(1280));
        Assert.That(settings.ResolutionHeight, Is.EqualTo(720));
    }

    [Test]
    public void SavedResolutionFitsANewSmallerMonitor()
    {
        var settings = new GameSettingsData { ResolutionWidth = 3840, ResolutionHeight = 2160, Fullscreen = false };
        GameDisplaySettings.Resolve(settings, new(1920, 1080), new(1280, 720), true);
        Assert.That(settings.ResolutionWidth, Is.EqualTo(1920));
        Assert.That(settings.ResolutionHeight, Is.EqualTo(1080));
        Assert.That(settings.Fullscreen, Is.False);
    }

    [Test]
    public void ResolutionOptionsIncludeCommonCurrentAndNativeSizesOnceAndWrapBothWays()
    {
        Vector2Int current = new(1500, 840), desktop = new(1920, 1080);
        List<Vector2Int> options = GameDisplaySettings.GetResolutions(desktop, current);
        Assert.That(options, Does.Contain(new Vector2Int(1280, 720)));
        Assert.That(options, Does.Contain(new Vector2Int(1600, 900)));
        Assert.That(options, Does.Contain(current));
        Assert.That(options, Does.Contain(desktop));
        Assert.That(options.Distinct().Count(), Is.EqualTo(options.Count));
        Assert.That(options.All(r => r.x <= desktop.x && r.y <= desktop.y), Is.True);
        Assert.That(GameDisplaySettings.ShiftResolution(options[0], -1, options), Is.EqualTo(options.Last()));
        Assert.That(GameDisplaySettings.ShiftResolution(options.Last(), 1, options), Is.EqualTo(options[0]));
        Assert.That(GameDisplaySettings.ShiftResolution(current, 0, options), Is.EqualTo(current));
    }

    [Test]
    public void ModeAndResolutionLabelsMatchBothLanguages()
    {
        var settings = new GameSettingsData { ResolutionWidth = 1920, ResolutionHeight = 1080 };
        Assert.That(GameDisplaySettings.ModeLabel(settings), Is.EqualTo("全屏"));
        settings.Fullscreen = false;
        Assert.That(GameDisplaySettings.ModeLabel(settings), Is.EqualTo("窗口"));
        settings.Language = GameLanguage.English;
        Assert.That(GameDisplaySettings.ModeLabel(settings), Is.EqualTo("Windowed"));
        settings.Fullscreen = true;
        Assert.That(GameDisplaySettings.ModeLabel(settings), Is.EqualTo("Fullscreen"));
        Assert.That(GameDisplaySettings.ResolutionLabel(settings), Is.EqualTo("1920 × 1080"));
    }

    [TestCase("Assets/Res/UI/SettingUI.prefab", "Panel/")]
    [TestCase("Assets/Res/UI/CharacterUI.prefab", "Setting/Display/")]
    public void BothSettingsUseTheRealPurchaseArrowSprites(string path, string prefix)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject shop = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/ShopBuyUI.prefab");
        Assert.That(prefab.GetComponentsInChildren<Component>(true).All(c => c != null), Is.True);
        foreach (string row in new[] { "DisplayMode", "Resolution" })
        {
            foreach (string direction in new[] { "Previous", "Next" })
            {
                Transform button = prefab.transform.Find(prefix + row + direction);
                Assert.That(button.GetComponent<ButtonPlus>(), Is.Not.Null);
                Image image = button.GetComponent<Image>();
                Assert.That(image.sprite, Is.EqualTo(shop.transform.Find(direction == "Previous" ? "Reduce" : "Add").GetComponent<Image>().sprite));
                Assert.That(image.raycastTarget, Is.True);
            }
            Assert.That(prefab.transform.Find(prefix + row + "Value").GetComponent<TMPro.TMP_Text>().font, Is.Not.Null);
        }
    }

    [Test]
    public void MainMenuDisplayButtonsUnsubscribeAndNeverDuplicateOnReopen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Res/UI/SettingUI.prefab");
        var view = root.GetComponent<SettingUI>();
        var data = new SettingUIData();
        data.Bind(root.transform);
        int modes = 0, previous = 0, next = 0;
        view.DisplayModeRequested += () => modes++;
        view.ResolutionRequested += direction => { if (direction < 0) previous++; else next++; };
        void Click()
        {
            data.Panel_DisplayModePrevious.ButtonPlus.onClick.Invoke();
            data.Panel_DisplayModeNext.ButtonPlus.onClick.Invoke();
            data.Panel_ResolutionPrevious.ButtonPlus.onClick.Invoke();
            data.Panel_ResolutionNext.ButtonPlus.onClick.Invoke();
        }
        try
        {
            view.EnsureInitialized();
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                view.OnOpen();
                Click();
                view.OnClose();
                Click();
                Assert.That(modes, Is.EqualTo(cycle * 2));
                Assert.That(previous, Is.EqualTo(cycle));
                Assert.That(next, Is.EqualTo(cycle));
            }
        }
        finally { view.OnClose(); PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void BookDisplayIntentsBindOnceAndRenderingIsSilent()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Res/UI/CharacterUI.prefab");
        try
        {
            var view = root.transform.Find("Setting").GetComponent<CharacterUI_SettingView>();
            view.InitializeBindings();
            view.InitializeBindings();
            int modes = 0, previous = 0, next = 0;
            view.DisplayModeRequested += () => modes++;
            view.ResolutionRequested += direction => { if (direction < 0) previous++; else next++; };
            view.Render(new GameSettingsData { Fullscreen = false, ResolutionWidth = 1600, ResolutionHeight = 900 }, CharacterSettingsSection.Display);
            Assert.That(modes + previous + next, Is.Zero);
            Assert.That(view.UI.Display_ResolutionValue.TextMeshProUGUI.text, Is.EqualTo("1600 × 900"));
            Assert.That(view.UI.Display_DisplayModeValue.TextMeshProUGUI.text, Is.EqualTo("窗口"));
            view.UI.Display_DisplayModePrevious.ButtonPlus.onClick.Invoke();
            view.UI.Display_DisplayModeNext.ButtonPlus.onClick.Invoke();
            view.UI.Display_ResolutionPrevious.ButtonPlus.onClick.Invoke();
            view.UI.Display_ResolutionNext.ButtonPlus.onClick.Invoke();
            Assert.That(modes, Is.EqualTo(2));
            Assert.That(previous, Is.EqualTo(1));
            Assert.That(next, Is.EqualTo(1));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
