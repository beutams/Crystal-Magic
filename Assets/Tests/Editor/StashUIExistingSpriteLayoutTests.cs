using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StashUIExistingSpriteLayoutTests
{
    private GameObject _instance;
    private StashUI _view;
    private StashUIData _ui;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/StashUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        _instance = Object.Instantiate(prefab);
        _instance.SetActive(true);
        _view = _instance.GetComponent<StashUI>();
        _view.EnsureInitialized();
        _ui = new StashUIData();
        _ui.Bind(_instance.transform);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_instance);

    private object Invoke(string method, params object[] args) =>
        typeof(StashUI).GetMethod(method, PrivateInstance).Invoke(_view, args);

    private static void AssertBindings(UIData data)
    {
        foreach (FieldInfo field in data.GetType().GetFields())
            if (field.FieldType == typeof(UINode))
                Assert.That(field.GetValue(data), Is.Not.Null, field.Name);
    }

    [Test]
    public void GeneratedBindingsAndBothDistinctItemHandlersResolve()
    {
        AssertBindings(_ui);
        var inventory = _ui.InventoryView_Viewport_Content_InventoryItem.GameObject;
        var stash = _ui.StashView_Viewport_Content_StashItem.GameObject;
        Assert.That(inventory.GetComponent<StashUI_InventoryItemView>(), Is.Not.Null);
        Assert.That(inventory.GetComponent<StashUI_StashItemView>(), Is.Null);
        Assert.That(stash.GetComponent<StashUI_StashItemView>(), Is.Not.Null);
        AssertBindings(inventory.GetComponent<StashUI_InventoryItemView>().UI);
        AssertBindings(stash.GetComponent<StashUI_StashItemView>().UI);
    }

    [Test]
    public void SameShopArtIsUsedWithSixAndFiveColumnScrollableGrids()
    {
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_ui.StashView.Image.sprite)),
            Is.EqualTo("00e10dcb58fb7144daaf3a561b084d66"));
        Assert.That(_ui.InventoryView.Image.sprite, Is.SameAs(_ui.StashView.Image.sprite));
        Assert.That(_ui.StashView.Image.pixelsPerUnitMultiplier, Is.EqualTo(1f / 4.2f).Within(.001f));
        foreach (var pair in new[] { (_ui.StashView, _ui.StashView_Viewport_Content, 6), (_ui.InventoryView, _ui.InventoryView_Viewport_Content, 5) })
        {
            var grid = pair.Item2.GameObject.GetComponent<GridLayoutGroup>();
            var scroll = pair.Item1.GameObject.GetComponent<ScrollRect>();
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(pair.Item3));
            Assert.That(scroll.content, Is.SameAs(pair.Item2.RectTransform));
            Assert.That(scroll.viewport.rect.height, Is.EqualTo(4 * grid.cellSize.y + 3 * grid.spacing.y).Within(.01f));
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);
        }
    }

    [Test]
    public void DecorativeGraphicsDoNotInterceptStorageDragAndDrop()
    {
        var interactive = new HashSet<GameObject>
        {
            _ui.Backdrop.GameObject, _ui.StashView.GameObject, _ui.InventoryView.GameObject,
            _ui.StashView_Viewport_Content_StashItem.GameObject, _ui.InventoryView_Viewport_Content_InventoryItem.GameObject,
            _ui.Back.GameObject, _ui.ButtonList_All.GameObject, _ui.ButtonList_Skill.GameObject,
            _ui.ButtonList_Equip.GameObject, _ui.ButtonList_Props.GameObject,
            _ui.InventorySort.GameObject, _ui.StashSort.GameObject
        };
        foreach (Graphic graphic in _instance.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.EqualTo(interactive.Contains(graphic.gameObject)), graphic.name);
    }

    [Test]
    public void FiltersAndReturnPublishExactlyOnceAcrossReopens()
    {
        int all = 0, skills = 0, gear = 0, items = 0, back = 0;
        _view.AllCategoryRequested += () => all++;
        _view.SkillCategoryRequested += () => skills++;
        _view.EquipCategoryRequested += () => gear++;
        _view.PropsCategoryRequested += () => items++;
        _view.BackClicked += () => back++;
        var buttons = new[] { _ui.ButtonList_All, _ui.ButtonList_Skill, _ui.ButtonList_Equip, _ui.ButtonList_Props, _ui.Back };
        for (int i = 1; i <= 3; i++)
        {
            _view.OnOpen();
            foreach (UINode button in buttons) button.ButtonPlus.onClick.Invoke();
            Assert.That(new[] { all, skills, gear, items, back }, Is.All.EqualTo(i));
            _view.OnClose();
            foreach (UINode button in buttons) button.ButtonPlus.onClick.Invoke();
            Assert.That(new[] { all, skills, gear, items, back }, Is.All.EqualTo(i));
        }
    }

    [Test]
    public void BothSortButtonsPublishDistinctIntentsOnceAcrossReopens()
    {
        int bag = 0, stash = 0;
        _view.InventorySortRequested += () => bag++;
        _view.StashSortRequested += () => stash++;
        for (int i = 1; i <= 3; i++)
        {
            _view.OnOpen();
            _ui.Drag.GameObject.SetActive(true);
            _ui.InventorySort.ButtonPlus.onClick.Invoke();
            Assert.That(bag, Is.EqualTo(i));
            Assert.That(stash, Is.EqualTo(i - 1));
            Assert.That(_ui.Drag.GameObject.activeSelf, Is.False);
            _ui.StashSort.ButtonPlus.onClick.Invoke();
            Assert.That(stash, Is.EqualTo(i));
            _view.OnClose();
            _ui.InventorySort.ButtonPlus.onClick.Invoke();
            _ui.StashSort.ButtonPlus.onClick.Invoke();
            Assert.That(new[] { bag, stash }, Is.All.EqualTo(i));
        }
    }

    [Test]
    public void CategoryModelSelectsExactlyOneTabAndItsGreenMarker()
    {
        var model = new StashUIModel();
        _view.BindModel(model);
        var defaults = new[] { _ui.ButtonList_All_Default, _ui.ButtonList_Skill_Default, _ui.ButtonList_Equip_Default, _ui.ButtonList_Props_Default };
        var selected = new[] { _ui.ButtonList_All_Select, _ui.ButtonList_Skill_Select, _ui.ButtonList_Equip_Select, _ui.ButtonList_Props_Select };
        for (int category = 0; category < 4; category++)
        {
            typeof(StashUIModel).GetField("_category", PrivateInstance).SetValue(model, (StashCategory)category);
            Invoke("RefreshCategorySelection");
            for (int tab = 0; tab < 4; tab++)
            {
                Assert.That(selected[tab].GameObject.activeSelf, Is.EqualTo(tab == category));
                Assert.That(defaults[tab].GameObject.activeSelf, Is.EqualTo(tab != category));
            }
        }
    }

    [Test]
    public void EmptyWarehousePadsOneScreenButDoesNotAlterModelContents()
    {
        var template = _ui.StashView_Viewport_Content_StashItem.GameObject.GetComponent<StashUI_StashItemView>();
        var views = (List<StashUI_StashItemView>)typeof(StashUI).GetField("_stashItemViews", PrivateInstance).GetValue(_view);
        for (int i = 0; i < 24; i++) views.Add(Object.Instantiate(template, template.transform.parent));
        var data = new List<StashItemDisplayData>();
        Invoke("RenderStash", data);
        Assert.That(views.Count, Is.EqualTo(24));
        Assert.That(data, Is.Empty);
        Assert.That(_ui.StashView_EmptyHint.GameObject.activeSelf, Is.True);
        foreach (var view in views)
        {
            Assert.That(view.UI.Mask_Icon.GameObject.activeSelf, Is.False);
            Assert.That(view.UI.Count.TextMeshProUGUI.text, Is.Empty);
        }
        data.Add(new StashItemDisplayData { ItemId = 1, Count = 2, Name = "test" });
        Invoke("RenderStash", data);
        Assert.That(data.Count, Is.EqualTo(1));
        Assert.That(views.Count, Is.EqualTo(24));
        Assert.That(_ui.StashView_EmptyHint.GameObject.activeSelf, Is.False);
    }

    [Test]
    public void ItemDoubleClicksPreserveDataAndEmptySlotsAreInert()
    {
        var inventory = _ui.InventoryView_Viewport_Content_InventoryItem.GameObject.GetComponent<StashUI_InventoryItemView>();
        var stash = _ui.StashView_Viewport_Content_StashItem.GameObject.GetComponent<StashUI_StashItemView>();
        var inventoryData = new StashInventoryDisplayData { SlotIndex = 3, ItemId = 5, Count = 2 };
        var stashData = new StashItemDisplayData { SlotIndex = 7, ItemId = 9, Count = 4 };
        int stored = 0, withdrawn = 0;
        inventory.DoubleClicked += data => { Assert.That(data, Is.SameAs(inventoryData)); stored++; };
        stash.DoubleClicked += data => { Assert.That(data, Is.SameAs(stashData)); withdrawn++; };
        var click = new PointerEventData(null) { clickCount = 2 };
        inventory.Render(inventoryData); stash.Render(stashData);
        inventory.OnPointerClick(click); stash.OnPointerClick(click);
        Assert.That(inventory.UI.Mask_Icon.GameObject.activeSelf, Is.False);
        Assert.That(stash.UI.Mask_Icon.GameObject.activeSelf, Is.False);
        inventory.Render(null); stash.Render(null);
        inventory.OnPointerClick(click); stash.OnPointerClick(click);
        Assert.That(stored, Is.EqualTo(1)); Assert.That(withdrawn, Is.EqualTo(1));
    }
}
