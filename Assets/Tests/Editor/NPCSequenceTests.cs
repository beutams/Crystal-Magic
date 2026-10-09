using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class NPCSequenceTests
{
    [Test]
    public void OwnershipIncludesEquippedStonePotionsAndChainsButNotEmptySlots()
    {
        var character = new CharacterData();
        character.Equipment.MagicStoneId = 1;
        character.Props.Slots.Add(new CharacterPropSlotData { ItemId = 2, Quantity = 1 });
        character.Props.Slots.Add(new CharacterPropSlotData { ItemId = 3, Quantity = 0 });
        character.Skills.Chains[0].Slots.Add(new SkillChainSlotData { SkillStoneItemId = 4 });
        character.Backpack.Items.Add(new InventoryItemData { ItemId = 18, Quantity = 1 });
        foreach (int id in new[] { 1, 2, 4, 18 }) Assert.That(NPCSequenceUtility.Owns(character, id), Is.True);
        foreach (int id in new[] { -1, 3, 16 }) Assert.That(NPCSequenceUtility.Owns(character, id), Is.False);
    }

    [Test]
    public void SkillProgressRequiresCorrectFirstChainOrder()
    {
        var character = new CharacterData();
        var ids = new[] { 4, 18, 16 };
        character.Skills.Chains[1].Slots = ids.Select(id => new SkillChainSlotData { SkillStoneItemId = id }).ToList();
        Assert.That(NPCSequenceUtility.HasSkillPrefix(character, ids), Is.False);
        character.Skills.Chains[0].Slots = new[] { 18, 4, 16 }.Select(id => new SkillChainSlotData { SkillStoneItemId = id }).ToList();
        Assert.That(NPCSequenceUtility.HasSkillPrefix(character, ids), Is.False);
        character.Skills.Chains[0].Slots = ids.Select(id => new SkillChainSlotData { SkillStoneItemId = id }).ToList();
        Assert.That(NPCSequenceUtility.HasSkillPrefix(character, ids), Is.True);
        character.Skills.Chains = Array.Empty<SkillChainData>();
        Assert.That(NPCSequenceUtility.HasSkillPrefix(character, ids), Is.False);
    }

    [Test]
    public void ClosingCharacterPanelRechecksTheEntireLoadout()
    {
        var intro = Intro();
        var closed = intro.GetNode("character_closed");
        var requirement = closed.Branches[0].RuntimeCondition;
        Assert.That(requirement.Condition, Is.EqualTo(NPCWaitCondition.All));
        Assert.That(closed.Branches[1].NextNodeGuid, Is.EqualTo("loadout_reason"));
        var character = new CharacterData();
        character.Equipment.MagicStoneId = 1;
        character.Props.Slots.Add(new CharacterPropSlotData { ItemId = 2, Quantity = 1 });
        character.Props.Slots.Add(new CharacterPropSlotData { ItemId = 3, Quantity = 1 });
        character.Skills.Chains[0].Slots = new[] { 4, 18, 16 }.Select(id => new SkillChainSlotData { SkillStoneItemId = id }).ToList();
        Assert.That(NPCSequenceUtility.Check(requirement, character), Is.True);
        character.Equipment.MagicStoneId = -1;
        Assert.That(NPCSequenceUtility.Check(requirement, character), Is.False);
    }

    [Test]
    public void PurchasesAndEquipsSaveBeforeTheNextGuideAndArrivalDoesNotRunEnterDungeon()
    {
        var intro = Intro();
        Assert.That(intro.IsSequence, Is.True);
        foreach (var node in intro.Nodes.OfType<NPCWaitConditionInteractionNodeData>().Where(n => n.Guid.StartsWith("own_") || n.Guid.StartsWith("equipped_") || n.Guid.StartsWith("chain_has_")))
            Assert.That(intro.GetNode(node.Branches[0].NextNodeGuid), Is.TypeOf<NPCProgressInteractionNodeData>());
        Assert.That(intro.Nodes.OfType<NPCEnterDungeonInteractionNodeData>(), Is.Empty);
        var finish = (NPCWaitConditionInteractionNodeData)intro.GetNode("dungeon_arrival");
        Assert.That(finish.Value, Is.EqualTo("intro_completed==1"));
        Assert.That(finish.Branches, Is.Empty);
        Assert.That(intro.GetEntryNode().Branches.Count, Is.EqualTo(6));
    }

    [Test]
    public void TutorialShoppingCosts180AndAllRegisteredNodeTypesHaveRunners()
    {
        var rows = Rows("ShopDataTable");
        int[] ids = { 1, 2, 3, 4, 18, 16 };
        Assert.That(ids.Sum(id => (int)rows.Single(r => (int)r["itemDataId"] == id)["Price"]), Is.EqualTo(180));
        var factory = new NPCInteractionNodeRunnerFactory();
        NPCInteractionNodeRunnerRegistry.RegisterAll(factory);
        foreach (var node in Intro().Nodes) Assert.That(factory.Create(node), Is.Not.Null, node.GetType().Name);
    }

    [Test]
    public void GuidePrefabHasBoundMaskLabelAndCannotConsumeEscape()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/GuideUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<GuideUI>(), Is.Not.Null);
        var data = new GuideUIData(); data.Bind(prefab.transform);
        Assert.That(data.Mask.GameObject.GetComponent<GuideMaskGraphic>(), Is.Not.Null);
        Assert.That(data.Hint_Label.TextMeshProUGUI, Is.Not.Null);
        Assert.That(data.Hint_Label.TextMeshProUGUI.raycastTarget, Is.False);
        var serialized = new SerializedObject(prefab.GetComponent<GuideUI>());
        Assert.That(serialized.FindProperty("_canCloseByEscape").boolValue, Is.False);
    }

    [Test]
    public void MaskPassesBothDragEndpointsAndBlocksOutsideTheirUnion()
    {
        var root = new GameObject("mask test", typeof(RectTransform), typeof(CanvasRenderer), typeof(GuideMaskGraphic));
        try
        {
            var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(400, 300);
            var graphic = root.GetComponent<GuideMaskGraphic>();
            var holes = new[] { new Rect(-100, -50, 80, 80), new Rect(40, 0, 80, 80) };
            graphic.Render(holes, true);
            Assert.That(graphic.Raycast(rect.TransformPoint(holes[0].center), null), Is.False);
            Assert.That(graphic.Raycast(rect.TransformPoint(holes[1].center), null), Is.False);
            Assert.That(graphic.Raycast(rect.TransformPoint(new Vector2(150, -100)), null), Is.True);
            graphic.Render(Array.Empty<Rect>(), false);
            Assert.That(graphic.Raycast(Vector2.zero, null), Is.False, "Missing targets must not strand input.");
            using var vertices = new VertexHelper();
            typeof(GuideMaskGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Invoke(graphic, new object[] { vertices });
            Assert.That(vertices.currentVertCount, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void CancellingSessionDisposesGuideExactlyOnce()
    {
        var lease = new TestLease();
        var session = new NPCInteractionSession(default, new NPCData(), new NPCInteractionData());
        session.Guide = lease;
        session.Cancel(); session.Cancel(); session.ClearGuide();
        Assert.That(lease.Calls, Is.EqualTo(1));
    }

    private sealed class TestLease : IDisposable { public int Calls; public void Dispose() => Calls++; }

    [Test]
    public void GuideKeyboardHintsMatchTheExistingInputBindings()
    {
        var input = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Res/Input/InputControls.inputactions")));
        var bindings = input["maps"].SelectMany(map => map["bindings"]);
        Assert.That(bindings.Any(b => (string)b["action"] == "Inventory" && (string)b["path"] == "<Keyboard>/b"), Is.True);
        Assert.That(bindings.Any(b => (string)b["action"] == "Interact" && (string)b["path"] == "<Keyboard>/e"), Is.True);
        var hints = Rows("LocalizationDataTable").Where(row => ((string)row["Key"]).StartsWith("guide.intro."));
        Assert.That(hints.All(row => !((string)row["ChineseSimplified"]).Contains("按 I")), Is.True);
        Assert.That((string)hints.Single(row => (string)row["Key"] == "guide.intro.close_character")["ChineseSimplified"], Does.Contain("Esc"));
    }

    [Test]
    public void GuideViewRefreshesFromModelAndKeepsTheHintAwayFromTargets()
    {
        using var events = new EventScope();
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/GuideUI.prefab"));
        var view = instance.GetComponent<GuideUI>();
        var model = new GuideUIModel();
        try
        {
            view.EnsureInitialized(); view.BindModel(model); view.OnOpen();
            var data = new GuideUIData(); data.Bind(instance.transform);
            model.SetPresentation(new[] { new Rect(-80, -680, 100, 100), new Rect(250, -20, 120, 120) }, true, "拖拽物品到槽位");
            Assert.That(data.Hint_Label.TextMeshProUGUI.text, Is.EqualTo("拖拽物品到槽位"));
            Assert.That(data.Mask.GameObject.GetComponent<GuideMaskGraphic>().raycastTarget, Is.True);
            Assert.That(data.Hint.RectTransform.anchoredPosition.y, Is.GreaterThan(1000));
            model.SetPresentation(Array.Empty<Rect>(), false, "按 I 打开角色界面");
            Assert.That(data.Mask.GameObject.GetComponent<GuideMaskGraphic>().raycastTarget, Is.False);
            Assert.That(data.Hint.RectTransform.anchoredPosition.y, Is.EqualTo(32));
        }
        finally { view.OnClose(); Object.DestroyImmediate(instance); model.Dispose(); }
    }

    [Test]
    public void CameraNodeKeepsItsLeaseUntilDurationOrCancellation()
    {
        using var world = new Unity.Entities.World("Camera sequence test");
        var root = new GameObject("Camera sequence components");
        var camera = root.AddComponent<CameraComponent>();
        var actor = world.EntityManager.CreateEntity(typeof(Unity.Transforms.LocalToWorld));
        world.EntityManager.SetComponentData(actor, new Unity.Transforms.LocalToWorld { Value = Unity.Mathematics.float4x4.identity });
        var session = new NPCInteractionSession(actor, new NPCData(), new NPCInteractionData(), actor, world);
        var runner = new NPCCameraInteractionNodeRunner(new NPCCameraInteractionNodeData { Target = "actor", Duration = 2 });
        try
        {
            runner.Enter(session);
            var leases = (System.Collections.IList)typeof(CameraComponent).GetField("_followOverrides", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(camera);
            Assert.That(leases.Count, Is.EqualTo(1));
            runner.Update(session, 1.9f); Assert.That(runner.IsCompleted(session), Is.False);
            runner.Update(session, .1f); Assert.That(runner.IsCompleted(session), Is.True);
            runner.Cancel(session); runner.Exit(session);
            Assert.That(leases.Count, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private sealed class EventScope : IDisposable
    {
        private readonly System.Reflection.FieldInfo _field = typeof(Singleton<EventComponent>).GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        private readonly object _previous;
        private readonly GameObject _root;
        public EventScope()
        {
            _previous = _field.GetValue(null); _root = new GameObject("Guide events"); _root.SetActive(false);
            _field.SetValue(null, _root.AddComponent<EventComponent>());
        }
        public void Dispose() { Object.DestroyImmediate(_root); _field.SetValue(null, _previous); }
    }
    private static JArray Rows(string table) => (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Res/Data/" + table + ".json")))["Rows"];
    private static NPCInteractionData Intro() => Rows("NPCDataTable").Single(r => (string)r["NPC"] == TownIntroTriggerUtility.NpcName).ToObject<NPCData>().Interactions.Single();
}
