using System.Linq;
using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

public sealed class BattleDeathSpectatorTests
{
    [Test]
    public void DeathKeepsPlayerOnlineAndLosesOneToThreeBackpackSlotsOnlyOnce()
    {
        using World world = new("Online death spectator regression", WorldFlags.GameServer);
        EntityManager manager = world.EntityManager;
        GameSingletonUtility.Create(manager, GameWorldRole.Server, GameSceneMode.Dungeon);
        Entity player = manager.CreateEntity(typeof(UnitDeathComponent), typeof(PlayerInputComponent), typeof(BattlePlayerStatusComponent));
        CharacterData data = new();
        data.Backpack.Items.Clear();
        for (int index = 0; index < 6; index++)
            data.Backpack.Items.Add(new InventoryItemData { ItemId = index + 1, Quantity = 1 });
        PlayerCharacterComponent character = new() { Data = data };
        manager.AddComponentObject(player, character);
        SystemBase death = (SystemBase)world.GetOrCreateSystemManaged(typeof(BattlePlayerStatusPresentationSystem).Assembly.GetType("UnitDeathFinalizeSystem"));
        death.Update();

        Assert.That(manager.Exists(player), Is.True);
        BattlePlayerStatusComponent status = manager.GetComponentData<BattlePlayerStatusComponent>(player);
        Assert.That(status.LifeState, Is.EqualTo(BattlePlayerLifeState.Dead));
        Assert.That(status.ConnectionState, Is.EqualTo(BattlePlayerConnectionState.Online));
        Assert.That(manager.HasComponent<BattleSpectatorComponent>(player), Is.True);
        Assert.That(manager.HasComponent<DestroyEntityFlag>(player) && manager.IsComponentEnabled<DestroyEntityFlag>(player), Is.False);
        int remaining = data.Backpack.Items.Count(item => !item.IsEmpty);
        Assert.That(6 - remaining, Is.InRange(1, 3));
        death.Update();
        Assert.That(data.Backpack.Items.Count(item => !item.IsEmpty), Is.EqualTo(remaining));
    }

    [Test]
    public void GhostIsTransparentAndReturnsToNormalPresentationAfterRevival()
    {
        using World world = new("Ghost transparency regression", WorldFlags.GameClient);
        GameObject visual = new("Ghost test visual");
        try
        {
            EntityManager manager = world.EntityManager;
            Entity player = manager.CreateEntity();
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            manager.AddComponentObject(player, renderer);
            BattlePlayerStatusUtility.Apply(manager, player, new BattlePlayerStatusComponent { LifeState = BattlePlayerLifeState.Dead });
            BattlePlayerStatusPresentationSystem presentation = world.GetOrCreateSystemManaged<BattlePlayerStatusPresentationSystem>();
            presentation.Update();
            Assert.That(renderer.color.a, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(manager.HasComponent<BattleSpectatorComponent>(player), Is.True);
            BattlePlayerStatusUtility.Apply(manager, player, new BattlePlayerStatusComponent { LifeState = BattlePlayerLifeState.Alive });
            presentation.Update();
            Assert.That(renderer.color.a, Is.EqualTo(1f));
            Assert.That(manager.HasComponent<BattleSpectatorComponent>(player), Is.False);
        }
        finally { Object.DestroyImmediate(visual); }
    }
}
