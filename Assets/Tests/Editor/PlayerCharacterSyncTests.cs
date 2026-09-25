using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using Server;
using Unity.Entities;

public sealed class PlayerCharacterSyncTests
{
    [Test]
    public void DraftAndSnapshotDoNotShareMutableData()
    {
        CharacterData original = new() { Money = 10 };
        original.Backpack.Items.Add(new InventoryItemData { ItemId = 2, Quantity = 3 });
        original.Skills.Chains[0].Slots.Add(new SkillChainSlotData { SkillStoneItemId = 4 });
        original.Props.Slots.Add(new CharacterPropSlotData { ItemId = 5, Quantity = 6 });
        CharacterData draft = PlayerCharacterUtility.Clone(original);
        draft.Money = 100;
        draft.Backpack.Items[0].Quantity = 0;
        draft.Skills.Chains[0].Slots[0].SkillStoneItemId = 50;
        draft.Equipment.SpiritSlots[0] = 60;
        draft.Props.Slots[0].Quantity = 0;
        Assert.That(original.Money, Is.EqualTo(10));
        Assert.That(original.Backpack.Items[0].Quantity, Is.EqualTo(3));
        Assert.That(original.Skills.Chains[0].Slots[0].SkillStoneItemId, Is.EqualTo(4));
        Assert.That(original.Equipment.SpiritSlots[0], Is.EqualTo(-1));
        Assert.That(original.Props.Slots[0].Quantity, Is.EqualTo(6));
    }

    [Test]
    public void ServerChangeRejectsOldDraftAndCommitCopiesAcceptedData()
    {
        PlayerCharacterComponent character = new();
        CharacterData draft = PlayerCharacterUtility.Clone(character.Data);
        character.Data.Money = 10;
        character.MarkChanged();
        Assert.That(character.TryEdit(0, draft), Is.False);
        Assert.That(character.Data.Money, Is.EqualTo(10));
        draft.Money = 20;
        Assert.That(character.TryEdit(1, draft), Is.True);
        Assert.That(character.Revision, Is.EqualTo(2));
        Assert.That(character.NetworkDirty, Is.EqualTo(1));
        draft.Money = 30;
        Assert.That(character.Data.Money, Is.EqualTo(20));
        Assert.That(character.TryEdit(1, draft), Is.False);
    }

    [Test]
    public void LateEditsAreRejectedAndFutureEditsWaitForTheirFrame()
    {
        TCPPacketCode.Init();
        Guid unitId = Guid.NewGuid();
        Connect connect = new();
        ServerFrameManager server = new() { currentFrame = 101 };
        server.AddConnect(connect, unitId);
        NetworkCharacterEditData late = new() { unitId = unitId };
        NetworkCharacterEditData future = new() { unitId = unitId };
        NetworkPropUseData propUse = new() { unitId = unitId };
        server.OnReceiveMessage(new General_FrameStateData
        {
            data = new NetworkFrameData
            {
                frameId = 99,
                datas = new List<NetworkStateData>
                {
                    new NetworkPlayerInputStateData { unitId = unitId }, late, propUse,
                },
            },
        }, connect);
        server.OnReceiveMessage(new General_FrameStateData
        {
            data = new NetworkFrameData
            {
                frameId = 110,
                datas = new List<NetworkStateData>
                {
                    future, new NetworkPlayerInputStateData { unitId = unitId },
                },
            },
        }, connect);
        List<NetworkStateData> applied = new();
        server.onHandleReceive = (frame, states) =>
        {
            Assert.That(frame, Is.EqualTo(110));
            while (states.Count > 0)
                applied.Add(states.Dequeue().data);
        };
        server.HandleReceive();
        server.HandleReceive();
        Assert.That(applied, Is.Empty);
        Assert.That(server.receivedOrder.ContainsKey(99), Is.False);
        Assert.That(server.receivedOrder[110].Count, Is.EqualTo(2));
        NetworkCharacterStateData rejection = (NetworkCharacterStateData)server.sendOrder[101].Dequeue();
        Assert.That(rejection.accepted, Is.False);
        Assert.That(rejection.requestId, Is.EqualTo(late.requestId));
        server.currentFrame = 110;
        server.HandleReceive();
        server.HandleReceive();
        Assert.That(applied.Count, Is.EqualTo(2));
        Assert.That(applied[0], Is.SameAs(future));
        Assert.That(applied[1], Is.TypeOf<NetworkPlayerInputStateData>());
    }

    [Test]
    public void OnlyMatchingResponseUnlocksNextEditAndDisconnectClearsPending()
    {
        ClientFrameManager client = new() { running = true, connect = new Connect() };
        NetworkCharacterEditData first = new() { unitId = Guid.NewGuid(), characterData = new CharacterData() };
        Assert.That(client.TrySendCharacterEdit(first), Is.True);
        NetworkCharacterEditData queued = (NetworkCharacterEditData)client.sendOrder[0].Peek();
        first.characterData.Money = 10;
        Assert.That(queued.characterData.Money, Is.Zero);
        Assert.That(client.TrySendCharacterEdit(new NetworkCharacterEditData { characterData = new CharacterData() }), Is.False);
        Assert.That(client.CompleteCharacterEdit(Guid.NewGuid(), queued.requestId), Is.False);
        Assert.That(client.CompleteCharacterEdit(first.unitId, Guid.NewGuid()), Is.False);
        Assert.That(client.CompleteCharacterEdit(first.unitId, queued.requestId), Is.True);
        Assert.That(client.CompleteCharacterEdit(first.unitId, queued.requestId), Is.False);
        Assert.That(client.TrySendCharacterEdit(first), Is.True);
        client.ClearOrders();
        Assert.That(client.HasPendingCharacterEdit, Is.False);
        Assert.That(client.sendOrder, Is.Empty);
    }

    [Test]
    public void RoomQueuesAndPlayerRevisionsAreIndependent()
    {
        ServerFrameManager first = new();
        ServerFrameManager second = new();
        PlayerCharacterComponent playerA = new();
        PlayerCharacterComponent playerB = new();
        playerA.MarkChanged();
        Assert.That(playerB.Revision, Is.Zero);
        first.OnReceiveMessage(new General_FrameStateData
        {
            data = new NetworkFrameData { datas = new List<NetworkStateData> { new NetworkCharacterEditData() } },
        }, null);
        int count = 0;
        second.onHandleReceive = (_, _) => count++;
        second.HandleReceive();
        first.ClearOrders();
        first.onHandleReceive = (_, _) => count++;
        first.HandleReceive();
        Assert.That(count, Is.Zero);
    }

    [Test]
    public void FrameSerializationPreservesCharacterSubclasses()
    {
        General_FrameStateData message = new()
        {
            data = new NetworkFrameData
            {
                datas = new List<NetworkStateData>
                {
                    new NetworkCharacterEditData { revision = 6, characterData = new CharacterData { Money = 55 } },
                    new NetworkCharacterStateData { revision = 7, accepted = true, characterData = new CharacterData() },
                    new NetworkPropUseData { slotIndex = 2, itemId = 3 },
                },
            },
        };
        byte[] bytes = TCPPacketCode.ToJson(message);
        General_FrameStateData decoded = JsonConvert.DeserializeObject<General_FrameStateData>(
            System.Text.Encoding.UTF8.GetString(bytes), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        Assert.That(decoded.data.datas[0], Is.TypeOf<NetworkCharacterEditData>());
        Assert.That(((NetworkCharacterEditData)decoded.data.datas[0]).characterData.Money, Is.EqualTo(55));
        Assert.That(((NetworkCharacterStateData)decoded.data.datas[1]).revision, Is.EqualTo(7));
        Assert.That(decoded.data.datas[2], Is.TypeOf<NetworkPropUseData>());
    }

    [Test]
    public void RebuildingRemotePlayerDoesNotAddInputOrCooldownRole()
    {
        using World world = new("Remote character test");
        EntityManager manager = world.EntityManager;
        Entity player = manager.CreateEntity();
        manager.AddComponentObject(player, new PlayerCharacterComponent());
        PlayerCharacterUtility.Rebuild(manager, player);
        Assert.That(manager.HasComponent<PlayerInputComponent>(player), Is.False);
        Assert.That(manager.HasComponent<PlayerPropCooldownComponent>(player), Is.False);
        Assert.That(manager.GetBuffer<PlayerSkillChainElement>(player).Length, Is.EqualTo(5));
    }
}
