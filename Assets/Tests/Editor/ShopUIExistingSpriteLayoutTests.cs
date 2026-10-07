using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopUIExistingSpriteLayoutTests
{
    private GameObject _instance;
    private ShopUI _view;
    private ShopUIData _ui;

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/ShopUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        _instance = Object.Instantiate(prefab);
        _instance.SetActive(true);
        _view = _instance.GetComponent<ShopUI>();
        _view.EnsureInitialized();
        _ui = new ShopUIData();
        _ui.Bind(_instance.transform);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_instance);

    private static void AssertBindings(UIData data)
    {
        foreach (FieldInfo field in data.GetType().GetFields())
            if (field.FieldType == typeof(UINode))
                Assert.That(field.GetValue(data), Is.Not.Null, field.Name);
    }

    [Test]
    public void MainAndItemBindingsResolveAndNoStashHandlersRemain()
    {
        AssertBindings(_ui);
        var commodity = new ShopUI_CommodityItemData();
        commodity.Bind(_ui.ShopView_Viewport_Content_CommodityItem.GameObject.transform);
        AssertBindings(commodity);
        var inventory = new ShopUI_InventoryItemData();
        inventory.Bind(_ui.InventoryView_Viewport_Content_InventoryItem.GameObject.transform);
        AssertBindings(inventory);
        Assert.That(_instance.GetComponentsInChildren<StashUI_StashItemView>(true), Is.Empty);
    }

    [Test]
    public void PanelAndHeadersUseTheApprovedExistingSprites()
    {
        AssertSprite(_ui.ShopView.Image, "00e10dcb58fb7144daaf3a561b084d66");
        AssertSprite(_ui.InventoryView.Image, "00e10dcb58fb7144daaf3a561b084d66");
        AssertSprite(_ui.ShopHeader.Image, "86847723b6099994abd41109ea1609f0");
        AssertSprite(_ui.InventoryHeader.Image, "b00f9213663b46a49b99a620b17204a1");
        Assert.That(_ui.ShopView.Image.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(_ui.ShopView.Image.pixelsPerUnitMultiplier, Is.EqualTo(1f / 4.2f).Within(.001f));
        Assert.That(_instance.transform.localScale, Is.EqualTo(Vector3.one));
    }

    private static void AssertSprite(Image image, string guid)
    {
        Assert.That(image.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.sprite)), Is.EqualTo(guid));
    }

    [Test]
    public void OnlyPanelItemAndButtonRootsReceiveRaycasts()
    {
        var interactive = new HashSet<GameObject>
        {
            _ui.Backdrop.GameObject, _ui.ShopView.GameObject, _ui.InventoryView.GameObject,
            _ui.ShopView_Viewport_Content_CommodityItem.GameObject,
            _ui.InventoryView_Viewport_Content_InventoryItem.GameObject, _ui.Back.GameObject, _ui.InventorySort.GameObject
        };
        foreach (Graphic graphic in _instance.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.EqualTo(interactive.Contains(graphic.gameObject)), graphic.name);
    }

    [Test]
    public void SelectedRowControlsBothTheBackgroundAndGreenMarker()
    {
        var item = _ui.ShopView_Viewport_Content_CommodityItem.GameObject.GetComponent<UISelectableListItem>();
        item.SetSelected(true);
        Assert.That(_ui.ShopView_Viewport_Content_CommodityItem_Select.GameObject.activeSelf, Is.True);
        Assert.That(_ui.ShopView_Viewport_Content_CommodityItem_Select_Marker.GameObject.activeInHierarchy, Is.True);
        item.SetSelected(false);
        Assert.That(_ui.ShopView_Viewport_Content_CommodityItem_Select.GameObject.activeSelf, Is.False);
        Assert.That(_ui.ShopView_Viewport_Content_CommodityItem_Select_Marker.GameObject.activeInHierarchy, Is.False);
    }

    [Test]
    public void InventoryHasFiveColumnsFourVisibleRowsAndScrollsAllThirtyTwoSlots()
    {
        var content = _ui.InventoryView_Viewport_Content.RectTransform;
        var grid = content.GetComponent<GridLayoutGroup>();
        Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
        Assert.That(grid.constraintCount, Is.EqualTo(5));
        Assert.That(grid.cellSize, Is.EqualTo(new Vector2(123.2f, 123.2f)));
        Assert.That(_ui.InventoryView_Viewport.RectTransform.rect.height,
            Is.EqualTo(grid.cellSize.y * 4 + grid.spacing.y * 3).Within(.01f));
        var template = _ui.InventoryView_Viewport_Content_InventoryItem.GameObject;
        template.SetActive(false);
        for (int i = 0; i < 32; i++)
            Object.Instantiate(template, content).SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Assert.That(content.rect.height, Is.GreaterThan(_ui.InventoryView_Viewport.RectTransform.rect.height));
        var scroll = _ui.InventoryView.GameObject.GetComponent<ScrollRect>();
        Assert.That(scroll.vertical, Is.True);
        Assert.That(scroll.horizontal, Is.False);
        Assert.That(scroll.content, Is.SameAs(content));
        Assert.That(_ui.InventoryView_Viewport_Content_InventoryItem_Name.GameObject.activeSelf, Is.False);
    }

    [Test]
    public void ReturnButtonPublishesOncePerOpenAndUnsubscribesWhenClosed()
    {
        int clicks = 0;
        _view.BackClicked += () => clicks++;
        for (int i = 0; i < 3; i++)
        {
            _view.OnOpen();
            _ui.Back.ButtonPlus.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(i + 1));
            _view.OnClose();
            _ui.Back.ButtonPlus.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(i + 1));
        }
    }

    [Test]
    public void MissingIconsAndEmptyDataNeverLeaveAWhiteImage()
    {
        var commodity = _ui.ShopView_Viewport_Content_CommodityItem.GameObject.GetComponent<ShopUI_CommodityItemView>();
        commodity.Render(new ShopCommodityDisplayData { Name = "test", Price = 123 });
        Assert.That(commodity.UI.IconBG_Mask_Icon.GameObject.activeSelf, Is.False);
        Assert.That(commodity.UI.Price.TextMeshProUGUI.text, Is.EqualTo("123"));
        commodity.Render(null);
        Assert.That(commodity.UI.IconBG_Mask_Icon.GameObject.activeSelf, Is.False);
        Assert.That(commodity.UI.Name.TextMeshProUGUI.text, Is.Empty);
        var inventory = _ui.InventoryView_Viewport_Content_InventoryItem.GameObject.GetComponent<ShopUI_InventoryItemView>();
        inventory.Render(new ShopInventoryDisplayData { Name = "test", Count = 3 });
        Assert.That(inventory.UI.IconBG_Mask_Icon.GameObject.activeSelf, Is.False);
        inventory.Render(null);
        Assert.That(inventory.UI.Count.TextMeshProUGUI.text, Is.Empty);
    }

    [Test]
    public void InventorySortPublishesOnceAndCancelsThePreviousDragAcrossReopens()
    {
        int clicks = 0;
        _view.InventorySortRequested += () => clicks++;
        for (int i = 1; i <= 3; i++)
        {
            _view.OnOpen();
            _ui.Drag.GameObject.SetActive(true);
            _ui.InventorySort.ButtonPlus.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(i));
            Assert.That(_ui.Drag.GameObject.activeSelf, Is.False);
            _view.OnClose();
            _ui.InventorySort.ButtonPlus.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(i));
        }
    }
}
