using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using Server;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CreateRoomUITests
{
    private GameObject events;
    [SetUp] public void SetUp()
    {
        if (!EventComponent.TryGetInstance(out _))
            events = new GameObject("CreateRoomTestEvents", typeof(EventComponent));
    }
    [TearDown] public void TearDown() { if (events != null) Object.DestroyImmediate(events); }

    [Test] public void AccountNameDefaultsAndManualRoomNameArePreserved()
    {
        var model = new CreateRoomUIModel(); model.InitializeName(" Steam玩家 ");
        Assert.That(model.DisplayName, Is.EqualTo("Steam玩家"));
        Assert.That(model.MaxPlayers, Is.EqualTo(4));
        model.SetDisplayName("新昵称"); Assert.That(model.RoomName, Does.Contain("新昵称"));
        model.SetRoomName("自定义房间"); model.SetDisplayName("另一昵称"); model.RefreshLocalization();
        Assert.That(model.RoomName, Is.EqualTo("自定义房间"));
        Assert.That(model.TryBuildRequest(out C2L_CreateRoom request), Is.True);
        Assert.That(request.displayName, Is.EqualTo("另一昵称"));
        Assert.That(request.roomName, Is.EqualTo("自定义房间"));
    }

    [Test] public void PendingRequestBlocksEditsAndFailureRetainsForm()
    {
        var model = new CreateRoomUIModel(); model.InitializeName("玩家");
        model.SetRoomName("双人探索"); model.SetMaxPlayers(2); model.SetSubmitting(true);
        model.SetRoomName("覆盖"); model.SetDisplayName("覆盖"); model.SetMaxPlayers(4);
        Assert.That(model.TryBuildRequest(out _), Is.False);
        model.SetSubmitting(false, "ui.lobby.error.timeout");
        Assert.That(model.RoomName, Is.EqualTo("双人探索"));
        Assert.That(model.DisplayName, Is.EqualTo("玩家"));
        Assert.That(model.MaxPlayers, Is.EqualTo(2));
        Assert.That(model.ErrorKey, Is.EqualTo("ui.lobby.error.timeout"));
        Assert.That(model.TryBuildRequest(out _), Is.True);
    }

    [TestCase("", "玩家", 4, RoomCreationError.RoomName)]
    [TestCase("   ", "玩家", 4, RoomCreationError.RoomName)]
    [TestCase("房间", "", 4, RoomCreationError.DisplayName)]
    [TestCase("房间", "玩家\n换行", 4, RoomCreationError.DisplayName)]
    [TestCase("房间", "玩家", 1, RoomCreationError.Capacity)]
    [TestCase("房间", "玩家", 5, RoomCreationError.Capacity)]
    public void InvalidSettingsAreRejected(string room, string name, int capacity, RoomCreationError expected)
    {
        Assert.That(RoomCreationRules.TryNormalize(room, name, capacity, out _, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo(expected));
    }

    [Test] public void LongSteamNameCanStillCreateRoomWithItsDefaultName()
    {
        var model = new CreateRoomUIModel(); model.InitializeName(new string('名', 40));
        Assert.That(model.DisplayName.Length, Is.EqualTo(RoomCreationRules.DisplayNameLimit));
        Assert.That(model.TryBuildRequest(out _), Is.True);
    }

    [Test] public void ExistingPixelFontSourceCanSupplyCharactersOutsideTheOldStaticAtlas()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/FusionPixelFont/Lobby Pixel SDF.asset");
        Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
        Assert.That(font.sourceFontFile, Is.Not.Null, "The player build must retain the original font source.");
        Assert.That(font.HasCharacter('昵', false, true), Is.True);
    }

    [Test] public void CreateFormBindingsInputsAndSubmitStatesWork()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Res/UI/CreateRoomUI.prefab");
        try
        {
            var data = new CreateRoomUIData(); data.Bind(root.transform); AssertBindings(data);
            var model = new CreateRoomUIModel(); model.InitializeName("玩家");
            var view = root.GetComponent<CreateRoomUI>(); view.EnsureInitialized(); view.BindModel(model);
            var refresh = typeof(CreateRoomUI).GetMethod("RefreshView", BindingFlags.Instance | BindingFlags.NonPublic);
            refresh.Invoke(view, null);
            Assert.That(data.Panel_DisplayName.TMP_InputField.text, Is.EqualTo("玩家"));
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>())
            { Assert.That(input.textComponent, Is.Not.Null); Assert.That(input.textViewport, Is.Not.Null); Assert.That(input.richText, Is.False); }
            Assert.That(data.Panel_DisplayName.TMP_InputField.characterLimit, Is.EqualTo(24));
            Assert.That(data.Panel_RoomName.TMP_InputField.characterLimit, Is.EqualTo(32));
            model.SetSubmitting(true); refresh.Invoke(view, null);
            Assert.That(data.Panel_Submit.ButtonPlus.enabled, Is.False);
            Assert.That(data.Panel_DisplayName.TMP_InputField.interactable, Is.False);
            model.SetSubmitting(false, "ui.lobby.error.timeout"); refresh.Invoke(view, null);
            Assert.That(data.Panel_Error.GameObject.activeSelf, Is.True);
            Assert.That(data.Panel_Submit.ButtonPlus.enabled, Is.True);
            Assert.That(data.Panel_CapacityFour_Selected.GameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("Overlay").GetComponent<Image>().raycastTarget, Is.True);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test] public void LobbyScrollAndUnavailableRoomButtonsUseResolvedBindings()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Res/UI/LobbyUI.prefab");
        try
        {
            var data = new LobbyUIData(); data.Bind(root.transform); AssertBindings(data);
            ScrollRect scroll = root.transform.Find("Panel/RoomScroll").GetComponent<ScrollRect>();
            Assert.That(scroll.vertical, Is.True); Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.content.GetComponent<ContentSizeFitter>().verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
            var row = data.Panel_RoomScroll_Viewport_Content_RoomItem.GameObject;
            var rowData = new LobbyUI_RoomItemData(); rowData.Bind(row.transform); AssertBindings(rowData);
            var view = row.GetComponent<LobbyUI_RoomItemView>();
            foreach (var state in new[] { (2, false, false), (1, true, false), (1, false, true) })
            {
                view.Render(new SimpleRoomData { roomId = 1, roomName = "<b>字面名字</b>", ownerName = "玩家", maxNum = 2,
                    enterNum = state.Item1, start = state.Item2 });
                view.SetInteractable(true);
                Assert.That(rowData.Join.ButtonPlus.enabled, Is.EqualTo(state.Item3));
                Assert.That(rowData.RoomName.TextMeshProUGUI.richText, Is.False);
            }
            Assert.That(rowData.Join.ButtonPlus.onClick, Is.Not.Null);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AssertBindings(UIData data)
    {
        foreach (var field in data.GetType().GetFields())
            if (field.FieldType == typeof(UINode)) Assert.That(field.GetValue(data), Is.Not.Null, field.Name);
    }

    [Test] public void RoomHostWaitsForMembersAndReadyGuestCanCancel()
    {
        var room = MakeRoom(); var model = new LobbyRoomUIModel(); model.SetRoom(room, 101);
        Assert.That(model.CanConfirm, Is.False); Assert.That(model.UnreadyCount, Is.EqualTo(1));
        room.playerready[202] = true; model.SetRoom(room, 101);
        Assert.That(model.CanConfirm, Is.True); Assert.That(model.ConfirmKey, Is.EqualTo("ui.room.start"));
        model.SetRoom(room, 202);
        Assert.That(model.CanConfirm, Is.True); Assert.That(model.ConfirmKey, Is.EqualTo("ui.room.cancel_ready"));
        Assert.That(model.CanLeave, Is.False);
        room.playerready[202] = false; model.SetRoom(room, 202);
        Assert.That(model.ConfirmKey, Is.EqualTo("ui.room.ready")); Assert.That(model.CanLeave, Is.True);
        room.start = true; model.SetRoom(room, 202);
        Assert.That(model.CanConfirm, Is.False); Assert.That(model.CanLeave, Is.False);
        Assert.That(model.ConfirmKey, Is.EqualTo("ui.room.entering"));
        model.SetRoom(room, 999); Assert.That(model.CanConfirm, Is.False); Assert.That(model.CanLeave, Is.False);
    }

    [Test] public void RoomCardsRefreshWithHostMigrationCapacityAndReopening()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Res/UI/LobbyRoomUI.prefab");
        var view = root.GetComponent<LobbyRoomUI>();
        try
        {
            var data = new LobbyRoomUIData(); data.Bind(root.transform); AssertBindings(data);
            var one = new LobbyRoomUI_PlayerData(); one.Bind(data.Panel_Players_One.GameObject.transform); AssertBindings(one);
            var two = new LobbyRoomUI_PlayerData(); two.Bind(data.Panel_Players_Two.GameObject.transform); AssertBindings(two);
            var three = new LobbyRoomUI_PlayerData(); three.Bind(data.Panel_Players_Three.GameObject.transform); AssertBindings(three);
            var room = MakeRoom(); room.players[101] = "<b>昵称</b>"; room.maxNum = 2;
            var model = new LobbyRoomUIModel(); model.SetRoom(room, 202);
            view.EnsureInitialized(); view.BindModel(model); view.OnOpen();
            Assert.That(view.CanCloseByEscape, Is.False, "Leaving must use the room protocol, not just close the panel.");
            Assert.That(one.PlayerName.TextMeshProUGUI.text, Is.EqualTo("<b>昵称</b>"));
            Assert.That(one.HostIcon.GameObject.activeSelf, Is.True); Assert.That(two.Selected.GameObject.activeSelf, Is.True);
            Assert.That(three.PlayerName.TextMeshProUGUI.text, Is.EqualTo(LocalizationComponent.Resolve("ui.room.closed_slot_hint")));
            room.playerready[202] = true; model.SetRoom(room, 202);
            Assert.That(data.Panel_Confirm_Label.TextMeshProUGUI.text, Is.EqualTo(LocalizationComponent.Resolve("ui.room.cancel_ready")));
            Assert.That(data.Panel_Leave.ButtonPlus.enabled, Is.False);
            room.ownerAccountId = 202; room.playerready[101] = true; room.playerready[202] = false; room.maxNum = 4;
            model.SetRoom(room, 202);
            Assert.That(one.PlayerName.TextMeshProUGUI.text, Is.EqualTo("星火")); Assert.That(one.Selected.GameObject.activeSelf, Is.True);
            Assert.That(two.HostIcon.GameObject.activeSelf, Is.False); Assert.That(two.Selected.GameObject.activeSelf, Is.False);
            Assert.That(data.Panel_Confirm.ButtonPlus.enabled, Is.True);
            Assert.That(three.PlayerName.TextMeshProUGUI.text, Is.EqualTo(LocalizationComponent.Resolve("ui.room.empty_slot")));
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) Assert.That(text.richText, Is.False);
            view.OnClose(); view.OnOpen(); Assert.That(root.transform.Find("Panel/Players").childCount, Is.EqualTo(4));
            model.SetRoom(null, 202); Assert.That(data.Panel_Confirm.ButtonPlus.enabled, Is.False);
        }
        finally { view.OnClose(); PrefabUtility.UnloadPrefabContents(root); }
    }

    private static RoomData MakeRoom()
    {
        var room = new RoomData { roomId = 1, roomName = "测试房间", ownerAccountId = 101, maxNum = 4 };
        room.players.Add(101, "旅行者"); room.players.Add(202, "星火");
        room.playerready.Add(101, false); room.playerready.Add(202, false);
        return room;
    }
}
