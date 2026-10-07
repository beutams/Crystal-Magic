using System;
using CrystalMagic.Core;
using NUnit.Framework;
using Server;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

public sealed class UnitControlImmunityTests
{
    [TestCase(0.1f, 0.5f)]
    [TestCase(0.5f, 1f)]
    [TestCase(1f, 2f)]
    [TestCase(2f, 4f)]
    public void SameTypeRemainsBlockedForTheDurationScaledGapAfterControlEnds(float duration, float gap)
    {
        using World world = new("Player control immunity");
        EntityManager manager = world.EntityManager;
        Entity player = CreateTarget(manager);
        UnitControlUtility.ApplyStun(manager, player, Entity.Null, duration);
        Tick(manager, player, duration);
        Assert.That(Read(manager, player).HasControl, Is.Zero);
        Assert.That(UnitControlUtility.GetImmunityRemaining(Read(manager, player), UnitControlType.Stun),
            Is.EqualTo(gap).Within(0.00001f));

        UnitControlUtility.ApplyStun(manager, player, Entity.Null, 10f);
        Assert.That(Read(manager, player).HasControl, Is.Zero);
        Tick(manager, player, gap - 0.01f);
        UnitControlUtility.ApplyStun(manager, player, Entity.Null, 10f);
        Assert.That(Read(manager, player).HasControl, Is.Zero);
        Tick(manager, player, 0.01f);
        UnitControlUtility.ApplyStun(manager, player, Entity.Null, duration);
        Assert.That(Read(manager, player).ActiveType, Is.EqualTo(UnitControlType.Stun));
    }

    [Test]
    public void BlockedKnockbackCannotChangeDirectionSourceDurationOrExtendProtection()
    {
        using World world = new("Repeated knockback");
        EntityManager manager = world.EntityManager;
        Entity player = CreateTarget(manager);
        Entity first = manager.CreateEntity(), second = manager.CreateEntity();
        UnitControlUtility.ApplyKnockback(manager, player, first, new float2(1, 0), 5f, 0.2f);
        Tick(manager, player, 0.05f);
        UnitControlRuntimeComponent expected = Read(manager, player);
        expected.NetworkDirty = 0;
        manager.SetComponentData(player, expected);
        UnitControlUtility.ApplyKnockback(manager, player, second, new float2(-1, 0), 100f, 4f);
        UnitControlRuntimeComponent actual = Read(manager, player);
        Assert.That(UnitControlUtility.StatesMatch(expected, actual), Is.True);
        Assert.That(actual.NetworkDirty, Is.Zero);
        Assert.That(actual.ActiveSourceEntity, Is.EqualTo(first));
        Assert.That(actual.ActiveMotionVelocity.x, Is.GreaterThan(0));
    }

    [Test]
    public void ProtectionIsPerTypeAndSharedAcrossAttackers()
    {
        using World world = new("Independent hard control types");
        EntityManager manager = world.EntityManager;
        Entity player = CreateTarget(manager);
        Entity first = manager.CreateEntity(), second = manager.CreateEntity();
        UnitControlUtility.ApplyStun(manager, player, first, 0.1f);
        Tick(manager, player, 0.1f);
        UnitControlUtility.ApplyStun(manager, player, second, 3f);
        Assert.That(Read(manager, player).HasControl, Is.Zero);
        UnitControlUtility.ApplyFear(manager, player, second, 0.2f);
        Assert.That(Read(manager, player).ActiveType, Is.EqualTo(UnitControlType.Fear));
        UnitControlUtility.ApplyKnockback(manager, player, second, new float2(1, 0), 1f, 0.1f);
        Assert.That(Read(manager, player).ActiveType, Is.EqualTo(UnitControlType.Knockback));
        Assert.That(Read(manager, player).Immunities.Length, Is.EqualTo(3));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void MonstersAndPlayersWithProtectionDisabledKeepTheExistingRefreshBehaviour(bool player, bool enabled)
    {
        using World world = new("Optional player protection");
        EntityManager manager = world.EntityManager;
        Entity target = CreateTarget(manager, player, enabled);
        UnitControlUtility.ApplyStun(manager, target, Entity.Null, 0.1f);
        UnitControlUtility.ApplyStun(manager, target, Entity.Null, 2f);
        Assert.That(Read(manager, target).ActiveRemainingTime, Is.EqualTo(2f));
        Assert.That(Read(manager, target).Immunities.Length, Is.Zero);
    }

    [Test]
    public void ControlEndingAndImmunityEndingAreDirtyButOrdinaryTimerTicksAreNot()
    {
        using World world = new("Control dirty state");
        EntityManager manager = world.EntityManager;
        Entity player = CreateTarget(manager);
        UnitControlUtility.ApplyStun(manager, player, Entity.Null, 0.1f);
        ResetDirty(manager, player);
        Tick(manager, player, 0.05f);
        Assert.That(Read(manager, player).NetworkDirty, Is.Zero);
        Tick(manager, player, 0.05f);
        Assert.That(Read(manager, player).NetworkDirty, Is.EqualTo(1));
        ResetDirty(manager, player);
        Tick(manager, player, 0.5f);
        Assert.That(Read(manager, player).NetworkDirty, Is.EqualTo(1));
        Assert.That(Read(manager, player).Immunities.Length, Is.Zero);
    }

    [Test]
    public void NetworkSnapshotPreservesExactTimersSettingsAndMapsControlSource()
    {
        using World server = new("Control server");
        using World client = new("Control client");
        GameSingletonUtility.Create(client.EntityManager, GameWorldRole.Client, GameSceneMode.Dungeon);
        Guid id = Guid.NewGuid(), sourceId = Guid.NewGuid();
        Entity player = CreateTarget(server.EntityManager);
        Entity source = server.EntityManager.CreateEntity(typeof(NetworkIdentityComponent));
        server.EntityManager.SetComponentData(source, new NetworkIdentityComponent { id = sourceId });
        UnitControlUtility.ApplyStun(server.EntityManager, player, source, 0.1f);
        NetworkControlStateData state = NetworkControlStateData.Capture(server.EntityManager, id,
            Read(server.EntityManager, player), 10, 33);

        Entity copy = CreateTarget(client.EntityManager);
        client.EntityManager.AddComponentData(copy, new NetworkIdentityComponent { id = id });
        Entity copySource = client.EntityManager.CreateEntity(typeof(NetworkIdentityComponent));
        client.EntityManager.SetComponentData(copySource, new NetworkIdentityComponent { id = sourceId });
        state.Apply(new NetworkStateApplyContext(client.EntityManager, 10, 33));
        UnitControlRuntimeComponent restored = Read(client.EntityManager, copy);
        Assert.That(restored.ActiveRemainingTime, Is.EqualTo(0.1f));
        Assert.That(restored.ActiveSourceEntity, Is.EqualTo(copySource));
        Assert.That(restored.HardControlImmunityEnabled, Is.EqualTo(1));
        Assert.That(restored.HardControlImmunityDurationMultiplier, Is.EqualTo(2));
        Assert.That(UnitControlUtility.GetImmunityRemaining(restored, UnitControlType.Stun), Is.EqualTo(0.6f));
    }

    [Test]
    public void SnapshotRestoresProtectionEvenWhenThereIsNoStateScript()
    {
        using World world = new("Control prediction snapshot");
        EntityManager manager = world.EntityManager;
        Entity player = CreateTarget(manager);
        UnitControlUtility.ApplyStun(manager, player, Entity.Null, 0.2f);
        ClientPlayerPredictionSnapshot snapshot = ClientPlayerPredictionSnapshot.Capture(manager, player, Guid.NewGuid());
        Tick(manager, player, 0.6f);
        snapshot.RestoreControl(manager, player);
        Assert.That(UnitControlUtility.StatesMatch(snapshot.Control, Read(manager, player)), Is.True);
    }

    [Test]
    public void DeathOrSceneTransitionClearsBothControlAndProtection()
    {
        using World world = new("Control reset");
        EntityManager manager = world.EntityManager;
        Entity player = CreateTarget(manager);
        UnitControlUtility.ApplyStun(manager, player, Entity.Null, 1f);
        BattlePlayerStatusUtility.Apply(manager, player, new BattlePlayerStatusComponent { LifeState = BattlePlayerLifeState.Dead });
        Assert.That(Read(manager, player).HasControl, Is.Zero);
        Assert.That(Read(manager, player).Immunities.Length, Is.Zero);
        BattlePlayerStatusUtility.Apply(manager, player, default);
        UnitControlUtility.ApplyStun(manager, player, Entity.Null, 1f);
        BattlePlayerStatusUtility.Apply(manager, player, new BattlePlayerStatusComponent { TransitionReady = 1 });
        Assert.That(Read(manager, player).HasControl, Is.Zero);
        Assert.That(Read(manager, player).Immunities.Length, Is.Zero);
    }

    [Test]
    public void ClientTicksOnlyOwnedPlayerAndARepeatedLateSnapshotDoesNotRestartTheTimer()
    {
        using World world = new("Owned control ticking", WorldFlags.GameClient);
        EntityManager manager = world.EntityManager;
        GameSingletonUtility.Create(manager, GameWorldRole.Client, GameSceneMode.Dungeon);
        ClientFrameManager frame = new() { running = true, currentFrame = 10 };
        manager.AddComponentObject(manager.CreateEntity(), new FrameManagerComponent { manager = frame });
        Entity owned = CreateTarget(manager), remote = CreateTarget(manager);
        Guid id = Guid.NewGuid();
        manager.AddComponentData(owned, new NetworkPlayerComponent { id = id });
        manager.AddComponentData(owned, new NetworkIdentityComponent { id = id });
        UnitControlUtility.ApplyStun(manager, owned, Entity.Null, 0.2f);
        UnitControlUtility.ApplyStun(manager, remote, Entity.Null, 0.2f);
        NetworkControlStateData state = NetworkControlStateData.Capture(manager, id, Read(manager, owned), 7, 33);
        state.Apply(new NetworkStateApplyContext(manager, 7, 33));
        float aged = 0.2f - 2 * 0.033f;
        Assert.That(Read(manager, owned).ActiveRemainingTime, Is.EqualTo(aged).Within(0.00001));
        var system = world.GetOrCreateSystem<UnitControlSystem>();
        world.SetTime(new TimeData(0.363, 0.033f));
        system.Update(world.Unmanaged);
        manager.CompleteAllTrackedJobs();
        frame.currentFrame++;
        Assert.That(Read(manager, owned).ActiveRemainingTime, Is.EqualTo(aged - 0.033f).Within(0.00001));
        Assert.That(Read(manager, remote).ActiveRemainingTime, Is.EqualTo(0.2f));
        float remaining = Read(manager, owned).ActiveRemainingTime;
        state.Apply(new NetworkStateApplyContext(manager, 7, 33));
        Assert.That(Read(manager, owned).ActiveRemainingTime, Is.EqualTo(remaining).Within(0.00001));
    }

    private static Entity CreateTarget(EntityManager manager, bool player = true, bool enabled = true)
    {
        Entity entity = manager.CreateEntity(typeof(UnitControlRuntimeComponent));
        manager.SetComponentData(entity, new UnitControlRuntimeComponent
        {
            HardControlImmunityEnabled = enabled ? (byte)1 : (byte)0,
            HardControlImmunityDurationMultiplier = 2f,
            HardControlImmunityMinimumSeconds = 0.5f,
        });
        if (player)
            manager.AddComponent<PlayerInputComponent>(entity);
        return entity;
    }

    private static UnitControlRuntimeComponent Read(EntityManager manager, Entity entity) =>
        manager.GetComponentData<UnitControlRuntimeComponent>(entity);

    private static void Tick(EntityManager manager, Entity entity, float seconds)
    {
        UnitControlRuntimeComponent runtime = Read(manager, entity);
        UnitControlUtility.TickAndRefresh(ref runtime, seconds);
        manager.SetComponentData(entity, runtime);
    }

    private static void ResetDirty(EntityManager manager, Entity entity)
    {
        UnitControlRuntimeComponent runtime = Read(manager, entity);
        runtime.NetworkDirty = 0;
        manager.SetComponentData(entity, runtime);
    }
}
