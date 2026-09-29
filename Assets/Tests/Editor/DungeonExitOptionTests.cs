using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using Newtonsoft.Json;
using NUnit.Framework;
using Server;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public sealed class DungeonExitOptionTests
{
    [TestCase(-1, 1, 1)] // Final floor without a next theme: retreat only.
    [TestCase(0, 2, 2)]  // Another floor of the current theme.
    [TestCase(2, 1, 2)]  // First floor of the next theme.
    public void AuthoredExitOptionsFollowTheRuntimeDestination(int themeId, int floor, int expectedCount)
    {
        using var world = new World("Exit option test");
        Entity exit = world.EntityManager.CreateEntity();
        DungeonExitRuntimeUtility.SetDestination(world.EntityManager, exit, themeId, floor);
        NPCInteractionData interaction = LoadExitInteraction();
        var select = (NPCSelectInteractionNodeData)interaction.GetEntryNode();
        var options = select.Options.Where(o => o.IsEnabled() &&
            DungeonExitRuntimeUtility.IsOptionAvailable(world.EntityManager, exit, interaction, o)).ToArray();
        Assert.That(options.Length, Is.EqualTo(expectedCount));
        Assert.That(options.Any(o => o.NextNodeGuid == "exit_retreat"), Is.True);
        Assert.That(options.Any(o => o.NextNodeGuid == "exit_next_floor"), Is.EqualTo(themeId >= 0));
    }

    [Test]
    public void NpcWithoutExitDestinationKeepsItsDungeonEntryOption()
    {
        using var world = new World("Town option test");
        Entity npc = world.EntityManager.CreateEntity();
        NPCInteractionData interaction = LoadExitInteraction();
        var select = (NPCSelectInteractionNodeData)interaction.GetEntryNode();
        Assert.That(DungeonExitRuntimeUtility.IsOptionAvailable(
            world.EntityManager, npc, interaction, select.Options[0]), Is.True);
    }

    [Test]
    public void DirectOnlineNextThemeOptionIsAlsoHiddenButRetreatRemains()
    {
        using var world = new World("Online exit option test");
        Entity exit = world.EntityManager.CreateEntity();
        DungeonExitRuntimeUtility.SetDestination(world.EntityManager, exit, -1, 1);
        var interaction = new NPCInteractionData
        {
            Nodes = new List<NPCInteractionNodeData>
            {
                new NPCRequestBattleExitInteractionNodeData { Guid = "next", RequestType = BattleExitRequestType.NextTheme },
                new NPCRequestBattleExitInteractionNodeData { Guid = "leave", RequestType = BattleExitRequestType.Retreat },
            },
        };
        Assert.That(DungeonExitRuntimeUtility.IsOptionAvailable(world.EntityManager, exit, interaction,
            new NPCSelectOptionData { NextNodeGuid = "next" }), Is.False);
        Assert.That(DungeonExitRuntimeUtility.IsOptionAvailable(world.EntityManager, exit, interaction,
            new NPCSelectOptionData { NextNodeGuid = "leave" }), Is.True);
    }

    [TestCase(-1, 1)]
    [TestCase(0, 2)]
    [TestCase(2, 1)]
    public void SpawnQueueAndReconnectSnapshotPreserveExitDestination(int themeId, int floor)
    {
        using var server = new World("Exit server", WorldFlags.GameServer);
        RegisterExitPrefab(server.EntityManager);
        NetworkEntitySpawnUtility.EnsureSpawnQueue(server.EntityManager);
        NetworkEntitySpawnInfo info = NetworkEntitySpawnUtility.CreateInfo(NetworkEntityPrefabType.Environment, "Exit", Vector3.zero);
        info.hasDungeonExitDestination = true;
        info.dungeonExitTargetThemeId = themeId;
        info.dungeonExitTargetFloor = floor;
        Assert.That(NetworkEntitySpawnUtility.TrySpawn(server.EntityManager, info, out Entity exit), Is.True);
        NetworkEntitySpawnInfo queued = NetworkEntitySpawnUtility.TakeSpawnQueue(server.EntityManager).Single();
        NetworkEntitySpawnInfo snapshot = NetworkEntitySpawnUtility.CreateSnapshotInfos(server.EntityManager).Single();
        using var client = new World("Exit client", WorldFlags.GameClient);
        RegisterExitPrefab(client.EntityManager);
        foreach (NetworkEntitySpawnInfo sent in new[] { queued, snapshot })
        {
            Assert.That(sent.hasDungeonExitDestination, Is.True);
            Assert.That(sent.dungeonExitTargetThemeId, Is.EqualTo(themeId));
            Assert.That(sent.dungeonExitTargetFloor, Is.EqualTo(floor));
            var received = JsonConvert.DeserializeObject<NetworkEntitySpawnInfo>(JsonConvert.SerializeObject(sent));
            Assert.That(NetworkEntitySpawnUtility.TrySpawn(client.EntityManager, received, out Entity clientExit), Is.True);
            Assert.That(DungeonExitRuntimeUtility.TryGetDestination(client.EntityManager, clientExit, out int actualTheme, out int actualFloor), Is.True);
            Assert.That(actualTheme, Is.EqualTo(themeId));
            Assert.That(actualFloor, Is.EqualTo(floor));
            client.EntityManager.DestroyEntity(clientExit);
        }
        // Reconnect must use current runtime values rather than stale spawn metadata.
        DungeonExitRuntimeUtility.SetDestination(server.EntityManager, exit, 7, 3);
        snapshot = NetworkEntitySpawnUtility.CreateSnapshotInfos(server.EntityManager).Single();
        Assert.That(snapshot.dungeonExitTargetThemeId, Is.EqualTo(7));
        Assert.That(snapshot.dungeonExitTargetFloor, Is.EqualTo(3));
    }

    private static void RegisterExitPrefab(EntityManager manager)
    {
        Entity registry = manager.CreateEntity(typeof(EntitySpawnRegistrySingleton));
        Entity prefab = manager.CreateEntity(typeof(Prefab));
        manager.AddBuffer<EnvironmentEntityPrefabRegistryEntry>(registry).Add(new EnvironmentEntityPrefabRegistryEntry
        {
            Name = new FixedString128Bytes("Exit"), Prefab = prefab,
        });
    }

    private static NPCInteractionData LoadExitInteraction() => JsonConvert.DeserializeObject<NpcTable>(
        File.ReadAllText(Path.Combine(Application.dataPath, "Res/Data/NPCDataTable.json")))
        .Rows.Single(r => r.NPC == "Exit").Interactions.Single(i => i.Key == "DungeonExit");

    private sealed class NpcTable { public List<NPCData> Rows = new(); }
}
