using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CharacterUIBookInteractionTests
{
    private GameObject _instance;
    private CharacterUI _view;
    private CharacterUIData _ui;
    private CharacterUIModel _model;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/CharacterUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        _instance = Object.Instantiate(prefab);
        _instance.SetActive(true);
        // Match UIGroup's runtime normalization without depending on an editor Game view.
        _instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var root = (RectTransform)_instance.transform;
        root.localScale = Vector3.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(2560, 1440);
        var scaler = _instance.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;
        _view = _instance.GetComponent<CharacterUI>();
        _view.EnsureInitialized();
        _ui = new CharacterUIData();
        _ui.Bind(_instance.transform);
        _model = new CharacterUIModel();
        _view.BindModel(_model);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_instance);
    }

    private object Invoke(string method, params object[] args) =>
        typeof(CharacterUI).GetMethod(method, PrivateInstance).Invoke(_view, args);

    private void SetModelValue(string property, object value) =>
        typeof(CharacterUIModel).GetField("<" + property + ">k__BackingField", PrivateInstance).SetValue(_model, value);

    private static void AssertBindings(UIData data)
    {
        foreach (FieldInfo field in data.GetType().GetFields())
            if (field.FieldType == typeof(UINode))
                Assert.That((UINode)field.GetValue(data), Is.Not.Null, field.Name);
    }

    [Test]
    public void AllBindingsMatchTheBookPrefabAndBothInventories()
    {
        AssertBindings(_ui);
        var rowData = new CharacterUI_SkillItemData();
        rowData.Bind(_ui.Skill_SkillChain_Viewport_Content_SkillItem.GameObject.transform);
        AssertBindings(rowData);
        foreach (UINode template in new[] { _ui.Equip_InventoryView_InventoryItem, _ui.Skill_InventoryView_InventoryItem })
        {
            var inventoryData = new CharacterUI_InventoryItemData();
            inventoryData.Bind(template.GameObject.transform);
            AssertBindings(inventoryData);
            Assert.That(template.GameObject.GetComponent<CharacterUI_InventoryItemView>(), Is.Not.Null);
        }
        Assert.That(_ui.Equip_ItemDrag.GameObject.transform.parent, Is.EqualTo(_ui.Equip.GameObject.transform));
        Assert.That(_ui.Skill_SkillDrag.GameObject.transform.parent, Is.EqualTo(_ui.Skill.GameObject.transform));
        Assert.That(_ui.Skill_SkillDrag.GameObject.GetComponent<CharacterUI_SkillItemView>(), Is.Null);
    }

    [Test]
    public void DefaultPageIsEquipAndSelectedBookmarkGetsExactlyFiftyExtraWidth()
    {
        Assert.That(_model.SelectedPage, Is.EqualTo(CharacterPage.Equip));
        Assert.That(_model.SelectedChainIndex, Is.Zero);
        UINode[] tabs = { _ui.Buttons_Equip, _ui.Buttons_Skill, _ui.Buttons_Handbook, _ui.Buttons_Setting };
        var widths = new float[tabs.Length];
        for (int i = 0; i < tabs.Length; i++) widths[i] = tabs[i].RectTransform.sizeDelta.x;
        foreach (int page in new[] { 0, 1, 2, 3 })
        {
            SetModelValue(nameof(CharacterUIModel.SelectedPage), (CharacterPage)page);
            for (int repeat = 0; repeat < 3; repeat++) Invoke("RenderNavigation");
            for (int i = 0; i < tabs.Length; i++)
            {
                Assert.That(tabs[i].RectTransform.sizeDelta.x, Is.EqualTo(widths[i] + (i == page ? 50 : 0)));
                Assert.That(tabs[i].RectTransform.pivot.x, Is.Zero);
            }
            Assert.That(_ui.Equip.GameObject.activeSelf, Is.EqualTo(page == 0));
            Assert.That(_ui.Skill.GameObject.activeSelf, Is.EqualTo(page == 1));
            Assert.That(_ui.Handbook.GameObject.activeSelf, Is.EqualTo(page == 2));
            Assert.That(_ui.Setting.GameObject.activeSelf, Is.EqualTo(page == 3));
        }
    }

    [Test]
    public void FourBookmarksAndFiveChainButtonsPublishTheCorrectIntentOnlyOnce()
    {
        int pageClicks = 0, chainClicks = 0;
        CharacterPage selectedPage = CharacterPage.Equip;
        int selectedChain = -1;
        _view.PageRequested += page => { pageClicks++; selectedPage = page; };
        _view.ChainRequested += index => { chainClicks++; selectedChain = index; };
        _view.EnsureInitialized();
        UINode[] pages = { _ui.Buttons_Equip, _ui.Buttons_Skill, _ui.Buttons_Handbook, _ui.Buttons_Setting };
        UINode[] chains = { _ui.Skill_ChainTabs_Chain1, _ui.Skill_ChainTabs_Chain2, _ui.Skill_ChainTabs_Chain3, _ui.Skill_ChainTabs_Chain4, _ui.Skill_ChainTabs_Chain5 };
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].ButtonPlus.onClick.Invoke();
            Assert.That(selectedPage, Is.EqualTo((CharacterPage)i));
        }
        for (int i = 0; i < chains.Length; i++)
        {
            chains[i].ButtonPlus.onClick.Invoke();
            Assert.That(selectedChain, Is.EqualTo(i));
        }
        Assert.That(pageClicks, Is.EqualTo(4));
        Assert.That(chainClicks, Is.EqualTo(5));
    }

    [Test]
    public void HoverSelectUsesTheAuthoredEquipmentAndPropFrames()
    {
        Invoke("EnsureEquipSlotHandlers");
        Invoke("EnsurePropSlotHandlers");
        var equip = _ui.Equip_MagicStoneBorder.GameObject.GetComponent<CharacterUI_EquipSlotDragHandler>();
        equip.OnPointerEnter(null);
        Assert.That(_ui.Equip_MagicStoneBorder_Select.GameObject.activeSelf, Is.True);
        equip.OnPointerExit(null);
        Assert.That(_ui.Equip_MagicStoneBorder_Select.GameObject.activeSelf, Is.False);
        var prop = _ui.Equip_PropSlot1.GameObject.GetComponent<CharacterUI_PropSlotDragHandler>();
        prop.OnPointerEnter(null);
        Assert.That(_ui.Equip_PropSlot1_Select.GameObject.activeSelf, Is.True);
        prop.OnPointerExit(null);
        Assert.That(_ui.Equip_PropSlot1_Select.GameObject.activeSelf, Is.False);
        foreach (Graphic frame in _ui.Equip_MagicStoneBorder_Select.GameObject.GetComponentsInChildren<Graphic>(true))
            Assert.That(frame.raycastTarget, Is.False);
    }

    [Test]
    public void AdditionButtonUsesCurrentRowAndFirstSkillHasNoAddition()
    {
        _ui.Skill.GameObject.SetActive(true);
        CharacterUI_SkillItemView row = _ui.Skill_SkillChain_Viewport_Content_SkillItem.GameObject.GetComponent<CharacterUI_SkillItemView>();
        // MonoBehaviour.Awake is not invoked reliably by EditMode instantiation.
        typeof(CharacterUI_SkillItemView).GetMethod("Awake", PrivateInstance).Invoke(row, null);
        int clicks = 0;
        CharacterSkillDisplayData requested = null;
        row.AdditionClicked += data => { clicks++; requested = data; };
        var first = new CharacterSkillDisplayData { SkillIndex = 0, DisplayIndex = 1, Name = "First" };
        row.Render(first);
        Assert.That(row.UI.Effect.GameObject.activeSelf, Is.False);
        row.UI.Effect.ButtonPlus.onClick.Invoke();
        Assert.That(clicks, Is.Zero);
        var second = new CharacterSkillDisplayData { SkillIndex = 1, DisplayIndex = 2, Name = "Second", CanSelectAddition = true, MpCost = 4, ChantDuration = 0.5f };
        row.Render(second);
        Assert.That(row.UI.Effect.ButtonPlus.enabled, Is.True);
        Assert.That(row.UI.Effect.GameObject.activeSelf, Is.True);
        row.UI.Effect.ButtonPlus.onClick.Invoke();
        Assert.That(requested, Is.SameAs(second));
        Assert.That(clicks, Is.EqualTo(1));
        Assert.That(row.UI.NameLabel.TextMeshProUGUI.text, Is.EqualTo("Second"));
    }

    [Test]
    public void SkillDropOrderUsesVerticalMidpointsAndRejectsHiddenPage()
    {
        _ui.Skill.GameObject.SetActive(true);
        // Use a world-space canvas so screen points are deterministic in EditMode.
        Canvas canvas = _instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = _instance.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;
        _instance.transform.localScale = Vector3.one;
        var root = (RectTransform)_instance.transform;
        root.sizeDelta = new Vector2(2560, 1440);
        var list = (List<CharacterUI_SkillItemView>)typeof(CharacterUI).GetField("_skillItemViews", PrivateInstance).GetValue(_view);
        var data = (List<CharacterSkillDisplayData>)typeof(CharacterUI).GetField("_currentSkillItems", PrivateInstance).GetValue(_view);
        GameObject template = _ui.Skill_SkillChain_Viewport_Content_SkillItem.GameObject;
        template.SetActive(false);
        for (int i = 0; i < 3; i++)
        {
            GameObject row = Object.Instantiate(template, _ui.Skill_SkillChain_Viewport_Content.GameObject.transform);
            row.SetActive(true);
            list.Add(row.GetComponent<CharacterUI_SkillItemView>());
            data.Add(new CharacterSkillDisplayData { SkillIndex = i });
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(_ui.Skill_SkillChain_Viewport_Content.RectTransform);
        var rect = (RectTransform)list[1].transform;
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(null) { position = center + Vector2.up * 10 };
        Assert.That((int)Invoke("GetSkillInsertIndex", pointer), Is.EqualTo(1));
        pointer.position = center - Vector2.up * 10;
        Assert.That((int)Invoke("GetSkillInsertIndex", pointer), Is.EqualTo(2));
        list.Clear();
        data.Clear();
        Assert.That((int)Invoke("GetSkillInsertIndex", pointer), Is.Zero, "An empty chain accepts the first skill.");
        _ui.Skill.GameObject.SetActive(false);
        Assert.That((int)Invoke("GetSkillInsertIndex", pointer), Is.EqualTo(-1));
    }

    [Test]
    public void CurrentPageUsesItsOwnDragVisualAndSwitchingPagesCancelsBoth()
    {
        var data = new CharacterInventoryDisplayData { SlotIndex = 0, ItemId = 1 };
        var pointer = new PointerEventData(null) { position = Vector2.zero };
        Invoke("RenderNavigation");
        Invoke("HandleInventoryDragStarted", data, pointer);
        Assert.That(_ui.Equip_ItemDrag.GameObject.activeInHierarchy, Is.True);
        Assert.That(_ui.Skill_SkillDrag.GameObject.activeInHierarchy, Is.False);

        SetModelValue(nameof(CharacterUIModel.SelectedPage), CharacterPage.Skill);
        Invoke("RenderNavigation");
        Assert.That(_ui.Equip_ItemDrag.GameObject.activeSelf, Is.False);
        Invoke("HandleInventoryDragStarted", data, pointer);
        Assert.That(_ui.Skill_SkillDrag.GameObject.activeInHierarchy, Is.True);
        Assert.That(_ui.Equip_ItemDrag.GameObject.activeInHierarchy, Is.False);

        SetModelValue(nameof(CharacterUIModel.SelectedPage), CharacterPage.Setting);
        Invoke("RenderNavigation");
        Assert.That(_ui.Equip_ItemDrag.GameObject.activeSelf, Is.False);
        Assert.That(_ui.Skill_SkillDrag.GameObject.activeSelf, Is.False);
        Assert.That(typeof(CharacterUI).GetField("_draggedInventoryItem", PrivateInstance).GetValue(_view), Is.Null);
    }

    [Test]
    public void AllEquipmentHandlersAreAuthoredAndOnlySlotRootsReceiveRaycasts()
    {
        foreach (UINode slot in new[] { _ui.Equip_MagicStoneBorder, _ui.Equip_Equip1Border, _ui.Equip_Equip2Border, _ui.Equip_Equip3Border, _ui.Equip_Equip4Border })
        {
            Assert.That(slot.GameObject.GetComponent<CharacterUI_EquipSlotDragHandler>(), Is.Not.Null);
            Assert.That(slot.Image.raycastTarget, Is.True);
            foreach (Graphic graphic in slot.GameObject.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject != slot.GameObject)
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }
        foreach (UINode drag in new[] { _ui.Equip_ItemDrag, _ui.Skill_SkillDrag })
            foreach (Graphic graphic in drag.GameObject.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
    }

    [Test]
    public void BothPageSortButtonsUseOneIntentAndClearDragVisuals()
    {
        int requests = 0;
        _view.InventorySortRequested += () => requests++;
        _view.EnsureInitialized();
        foreach (UINode button in new[] { _ui.Equip_InventorySort, _ui.Skill_InventorySort })
        {
            _ui.Equip_ItemDrag.GameObject.SetActive(true);
            _ui.Skill_SkillDrag.GameObject.SetActive(true);
            button.ButtonPlus.onClick.Invoke();
            Assert.That(_ui.Equip_ItemDrag.GameObject.activeSelf, Is.False);
            Assert.That(_ui.Skill_SkillDrag.GameObject.activeSelf, Is.False);
        }
        Assert.That(requests, Is.EqualTo(2));
    }

    [Test]
    public void BothInventoriesRouteEmptySlotsByTheirOriginalSlotIndex()
    {
        foreach (CharacterPage page in new[] { CharacterPage.Equip, CharacterPage.Skill })
        {
            SetModelValue(nameof(CharacterUIModel.SelectedPage), page);
            Invoke("RenderNavigation");
            UINode template = page == CharacterPage.Equip ? _ui.Equip_InventoryView_InventoryItem : _ui.Skill_InventoryView_InventoryItem;
            var item = template.GameObject.GetComponent<CharacterUI_InventoryItemView>();
            item.Render(null, 31);
            var pointer = new PointerEventData(null) { pointerCurrentRaycast = new RaycastResult { gameObject = template.GameObject } };
            object[] args = { pointer, -1 };
            bool found = (bool)typeof(CharacterUI).GetMethod("TryGetHoveredInventorySlotIndex", PrivateInstance).Invoke(_view, args);
            Assert.That(found, Is.True);
            Assert.That(args[1], Is.EqualTo(31));
            template.GameObject.SetActive(false);
            args[1] = -1;
            Assert.That((bool)typeof(CharacterUI).GetMethod("TryGetHoveredInventorySlotIndex", PrivateInstance).Invoke(_view, args), Is.False);
        }
    }

    [Test]
    public void DragEndIntoBackpackPublishesOneMoveAndHidesThePageVisual()
    {
        Invoke("RenderNavigation");
        var item = _ui.Equip_InventoryView_InventoryItem.GameObject.GetComponent<CharacterUI_InventoryItemView>();
        item.Render(null, 7);
        var data = new CharacterInventoryDisplayData { SlotIndex = 2, ItemId = 1 };
        var pointer = new PointerEventData(null)
        {
            position = new Vector2(99999, 99999),
            pointerCurrentRaycast = new RaycastResult { gameObject = item.gameObject },
        };
        int moves = 0;
        _view.InventoryItemMoved += (source, target) => { Assert.That(source, Is.SameAs(data)); Assert.That(target, Is.EqualTo(7)); moves++; };
        Invoke("HandleInventoryDragStarted", data, pointer);
        Invoke("HandleInventoryDragEnded", data, pointer);
        Invoke("HandleInventoryDragEnded", data, pointer);
        Assert.That(moves, Is.EqualTo(1));
        Assert.That(_ui.Equip_ItemDrag.GameObject.activeSelf, Is.False);
    }

    [Test]
    public void HandbookOpensItsOwnPageAndMissingIconsStayHidden()
    {
        SetModelValue(nameof(CharacterUIModel.SelectedPage), CharacterPage.Handbook);
        Invoke("RenderNavigation");
        Assert.That(_ui.Handbook.GameObject.activeSelf, Is.True);
        Assert.That(_ui.Skill.GameObject.activeSelf, Is.False);
        Assert.That(_ui.Equip.GameObject.activeSelf, Is.False);
        Assert.That(_ui.Setting.GameObject.activeSelf, Is.False);
        var inventory = _ui.Skill_InventoryView_InventoryItem.GameObject.GetComponent<CharacterUI_InventoryItemView>();
        inventory.Render(new CharacterInventoryDisplayData { ItemId = 1, Count = 2 }, 0);
        Assert.That(inventory.UI.Mask_Icon.GameObject.activeSelf, Is.False);
        var row = _ui.Skill_SkillChain_Viewport_Content_SkillItem.GameObject.GetComponent<CharacterUI_SkillItemView>();
        row.Render(new CharacterSkillDisplayData { SkillIndex = 0, Name = "test" });
        Assert.That(row.UI.SkillMask_Skill.GameObject.activeSelf, Is.False);
        row.Render(null);
        Assert.That(row.UI.SkillMask_Skill.GameObject.activeSelf, Is.False);
        Invoke("RenderEquipSlot", _ui.Equip_MagicStoneBorder_MagicStone, new CharacterEquipDisplayData { ItemId = 1 });
        Assert.That(_ui.Equip_MagicStoneBorder_MagicStone.GameObject.activeSelf, Is.False);
    }

    [Test]
    public void HandbookUsesIndependentBookPagesAndNineAuthoredEntries()
    {
        Assert.That(_ui.Handbook.GameObject.transform.parent, Is.EqualTo(_instance.transform));
        Assert.That(_ui.Handbook.GameObject.activeSelf, Is.False);
        Assert.That(_ui.Handbook_LeftPage.RectTransform.anchoredPosition.x,
            Is.LessThan(_ui.Handbook_RightPage.RectTransform.anchoredPosition.x));
        Assert.That(_ui.Handbook_LeftPage_Entries.GameObject.transform.childCount, Is.EqualTo(9));
        Assert.That(_ui.Handbook_LeftPage_Entries_Entry2_Selected.GameObject.activeSelf, Is.True);
        Assert.That(_ui.Handbook_RightPage_Header_Title.TextMeshProUGUI.text, Is.EqualTo("重甲骷髅"));
        Assert.That(_ui.Handbook_RightPage_Portrait_Sprite.Image.sprite, Is.Not.Null);
        Assert.That(_ui.Handbook_RightPage_Stats_Health_Value.TextMeshProUGUI.text, Is.EqualTo("150"));
        Assert.That(_ui.Handbook_RightPage_Drops_Amount.TextMeshProUGUI.text, Is.EqualTo("2–4"));
        Assert.That(_ui.Handbook_LeftPage_Footer_CollectionStatus.TextMeshProUGUI.text, Does.Contain("待接入"));
        foreach (Graphic graphic in _ui.Handbook.GameObject.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False, "Layout-only content must not intercept the existing bookmarks: " + graphic.name);
        foreach (Image image in _ui.Handbook.GameObject.GetComponentsInChildren<Image>(true))
        {
            Assert.That(image.sprite, Is.Not.Null, image.name);
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.StartWith("Assets/Res/Sprites/"));
        }
    }
}
