using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonSettlementUILayoutTests
{
    private GameObject _instance;
    private DungeonSettlementUI _view;
    private DungeonSettlementUIData _ui;

    [SetUp]
    public void SetUp()
    {
        _instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/DungeonSettlementUI.prefab"));
        _view = _instance.GetComponent<DungeonSettlementUI>();
        _view.EnsureInitialized();
        _ui = new DungeonSettlementUIData();
        _ui.Bind(_instance.transform);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_instance);

    [Test]
    public void AllMainAndItemBindingsResolve()
    {
        AssertBindings(_ui);
        var item = new DungeonSettlementUI_ItemData();
        item.Bind(_ui.Panel_Items_Viewport_Content_Item.RectTransform);
        AssertBindings(item);
    }

    private static void AssertBindings(UIData data)
    {
        foreach (var field in data.GetType().GetFields())
            if (field.FieldType == typeof(UINode))
                Assert.That(field.GetValue(data), Is.Not.Null, field.Name);
    }

    [Test]
    public void StoneIsNineSlicedAndBlueGoldRibbonIsReused()
    {
        Image panel = _instance.transform.Find("Panel").GetComponent<Image>();
        Assert.That(AssetDatabase.GetAssetPath(panel.sprite), Does.EndWith("StonePixel/Seperated/2.png"));
        Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(panel.sprite.border, Is.EqualTo(new Vector4(32, 32, 32, 32)));
        Assert.That(_ui.Panel_BG_Header.Image.sprite, Is.SameAs(_ui.Panel_Confirm.Image.sprite));
        Assert.That(AssetDatabase.GetAssetPath(_ui.Panel_Confirm.Image.sprite), Does.EndWith("MagicalUI/Headers/Header4.png"));
        Assert.That(panel.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
    }

    [Test]
    public void ThreeColumnListClipsAndScrollsWithoutMovingTheReturnButton()
    {
        var scroll = _ui.Panel_Items.GameObject.GetComponent<ScrollRect>();
        Assert.That(scroll.vertical, Is.True);
        Assert.That(scroll.horizontal, Is.False);
        Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(scroll.content, Is.SameAs(_ui.Panel_Items_Viewport_Content.RectTransform));
        var grid = scroll.content.GetComponent<GridLayoutGroup>();
        Assert.That(grid.constraintCount, Is.EqualTo(3));
        Assert.That(_ui.Panel_Confirm.RectTransform.IsChildOf(scroll.content), Is.False);
        Assert.That(_ui.Panel_Items_Viewport_Content_Item.GameObject.activeSelf, Is.False);
        for (int i = 0; i < 30; i++)
            Object.Instantiate(_ui.Panel_Items_Viewport_Content_Item.GameObject, scroll.content).SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
    }

    [Test]
    public void ReturnIntentUnsubscribesAcrossRepeatedOpens()
    {
        int clicks = 0;
        _view.ConfirmClicked += () => clicks++;
        for (int i = 1; i <= 3; i++)
        {
            _view.OnOpen();
            _ui.Panel_Confirm.ButtonPlus.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(i));
            _view.OnClose();
            _ui.Panel_Confirm.ButtonPlus.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(i));
        }
    }

    [Test]
    public void MissingIconDoesNotLeaveAWhiteSquare()
    {
        var item = _ui.Panel_Items_Viewport_Content_Item.GameObject.GetComponent<DungeonSettlementUI_ItemView>();
        item.Render(0, new CrystalMagic.UI.DungeonSettlementItemDisplayData { Name = "test", Count = 3 }, false, true);
        Assert.That(item.UI.Socket_Icon.GameObject.activeSelf, Is.False);
        Assert.That(item.UI.Socket_Amount.TextMeshProUGUI.text, Is.EqualTo("−3"));
        Assert.That(item.UI.Selected.GameObject.activeSelf, Is.True);
        item.Render(-1, null, true, false);
        Assert.That(item.UI.Name.TextMeshProUGUI.text, Is.Empty);
        Assert.That(item.UI.Selected.GameObject.activeSelf, Is.False);
    }
}
