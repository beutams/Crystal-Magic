using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Server;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class BattleSimulationTimingTests
{
    [Test]
    public void WorldRunsOnceWhileOnlyPlayersCatchUpAndWorldTimeIsRestored()
    {
        using World world = new("Hybrid simulation timing");
        world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        world.GetOrCreateSystemManaged<ClientInputSystemGroup>();
        world.GetOrCreateSystemManaged<UnitInitializationSystemGroup>();
        var decisions = world.GetOrCreateSystemManaged<UnitDecisionSystemGroup>();
        world.GetOrCreateSystemManaged<UnitExecutionSystemGroup>();
        world.GetOrCreateSystemManaged<UnitPostProcessSystemGroup>();
        world.GetOrCreateSystemManaged<ClientPlayerPredictionSystemGroup>();
        world.GetOrCreateSystemManaged<FrameReceiveSystem>();
        world.GetOrCreateSystem<PlayerInputPulseResetSystem>();
        FrameManager frame = new() { running = true };
        FrameManagerUtility.Bind(world.EntityManager, frame);
        var ordinary = world.GetOrCreateSystemManaged<OrdinaryTimingProbe>();
        var player = world.GetOrCreateSystemManaged<PlayerTimingProbe>();
        decisions.AddSystemToUpdateList(ordinary);
        var players = world.GetExistingSystemManaged<BattlePlayerSimulationSystemGroup>();
        players.AddSystemToUpdateList(player);
        var battle = world.GetExistingSystemManaged<BattleSimulationSystemGroup>();
        world.SetTime(new TimeData(0.1, 0.1f));
        battle.Update();
        Assert.That(battle.RateManager, Is.Null);
        Assert.That(ordinary.Deltas, Is.EqualTo(new[] { 0.1f }));
        Assert.That(player.Deltas.Count, Is.EqualTo(3));
        Assert.That(player.Deltas, Has.All.EqualTo(0.033f).Within(0.000001));
        Assert.That(frame.currentFrame, Is.EqualTo(3));
        Assert.That(world.Time.ElapsedTime, Is.EqualTo(0.1));
        using EntityQuery scope = world.EntityManager.CreateEntityQuery(typeof(BattleSimulationScope));
        Assert.That(scope.GetSingleton<BattleSimulationScope>().Pass, Is.EqualTo(BattleSimulationPass.World));
        world.SetTime(new TimeData(0.11, 0.01f));
        battle.Update();
        Assert.That(ordinary.Deltas.Count, Is.EqualTo(2));
        Assert.That(player.Deltas.Count, Is.EqualTo(3));
        frame.running = false;
        world.SetTime(new TimeData(0.12, 0.01f));
        battle.Update();
        Assert.That(ordinary.Deltas.Last(), Is.Zero);
        Assert.That(player.Deltas.Count, Is.EqualTo(3));
        Assert.That(world.Time.DeltaTime, Is.EqualTo(0.01f));
    }

    [Test]
    public void WallClockPauseDoesNotCreateSimulationDebt()
    {
        using World world = new("Paused clock");
        var group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        FrameManager frame = new() { running = true };
        long now = 100;
        var rate = new BattleFrameRateManager(frame, () => now);
        world.SetTime(new TimeData(0.01, 0.01f));
        Assert.That(rate.ShouldGroupUpdate(group), Is.False);
        now += 60_000;
        world.SetTime(new TimeData(0.02, 0.01f));
        Assert.That(rate.ShouldGroupUpdate(group), Is.False);
        Assert.That(frame.clock.AccumulatedMilliseconds, Is.EqualTo(20).Within(0.001));
        world.SetTime(new TimeData(0.02, 0));
        Assert.That(rate.ShouldGroupUpdate(group), Is.False);
        Assert.That(frame.currentFrame, Is.Zero);
    }

    [Test]
    public void PlayerAndNpcControlTimersAreNeverAdvancedByBothPasses()
    {
        using World world = new("Control timing scope");
        var manager = world.EntityManager;
        Entity npc = manager.CreateEntity(typeof(UnitControlRuntimeComponent));
        Entity player = manager.CreateEntity(typeof(UnitControlRuntimeComponent), typeof(PlayerInputComponent));
        UnitControlRuntimeComponent initial = default;
        initial.Entries.Add(new UnitControlRuntimeEntry { RemainingTime = 1 });
        manager.SetComponentData(npc, initial);
        manager.SetComponentData(player, initial);
        Entity scope = manager.CreateEntity(typeof(BattleSimulationScope));
        var system = world.GetOrCreateSystemManaged<ControlTimingProbe>();
        manager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.World });
        world.SetTime(new TimeData(0.1, 0.1f));
        system.Update();
        manager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.Players });
        world.SetTime(new TimeData(0.1, 0.033f));
        for (int index = 0; index < 3; index++)
            system.Update();
        Assert.That(manager.GetComponentData<UnitControlRuntimeComponent>(npc).Entries[0].RemainingTime,
            Is.EqualTo(0.9f).Within(0.00001));
        Assert.That(manager.GetComponentData<UnitControlRuntimeComponent>(player).Entries[0].RemainingTime,
            Is.EqualTo(0.901f).Within(0.00001));
    }

    [Test]
    public void CollectionKeepsPlayerTickSeparateAndCapturesNpcChangesBetweenTicks()
    {
        using World world = new("Snapshot timing scope");
        var manager = world.EntityManager;
        var frame = new ServerFrameManager { running = true, currentFrame = 7 };
        Entity binding = manager.CreateEntity();
        manager.AddComponentObject(binding, new FrameManagerComponent { manager = frame });
        Entity scope = manager.CreateEntity(typeof(BattleSimulationScope));
        Entity npc = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(UnitMoveComponent), typeof(LocalTransform));
        Entity player = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(UnitMoveComponent), typeof(LocalTransform), typeof(PlayerInputComponent));
        Guid npcId = Guid.NewGuid(), playerId = Guid.NewGuid();
        manager.SetComponentData(npc, new NetworkIdentityComponent { id = npcId });
        manager.SetComponentData(player, new NetworkIdentityComponent { id = playerId });
        manager.SetComponentData(npc, new UnitMoveComponent { NetworkDirty = 1 });
        manager.SetComponentData(player, new UnitMoveComponent { NetworkDirty = 1 });
        var collect = world.GetOrCreateSystemManaged<ServerNetworkStateCollectSystem>();
        manager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.World });
        collect.Update();
        Assert.That(frame.sendOrder[6].Single().unitId, Is.EqualTo(npcId));
        Assert.That(manager.GetComponentData<UnitMoveComponent>(player).NetworkDirty, Is.EqualTo(1));
        manager.SetComponentData(npc, new UnitMoveComponent { NetworkDirty = 1, Velocity = new float2(2, 0) });
        collect.Update();
        Assert.That(frame.sendOrder[6].Count, Is.EqualTo(2));
        manager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.Players });
        collect.Update();
        Assert.That(frame.sendOrder[7].Single().unitId, Is.EqualTo(playerId));
        Assert.That(manager.GetComponentData<UnitMoveComponent>(player).NetworkDirty, Is.Zero);
    }

    [Test]
    public void SendTimerBatchesCompletedInputTicksWithoutDroppingOperations()
    {
        var frame = new RecordingClient { running = true };
        var first = new NetworkPrimaryPressData();
        var second = new NetworkInteractData();
        frame.sendOrder[0] = new Queue<NetworkStateData>(new NetworkStateData[] { first, second });
        frame.OnTick();
        frame.sendOrder[1] = new Queue<NetworkStateData>(new NetworkStateData[] { new NetworkPrimaryPressData() });
        frame.OnTick();
        frame.sendOrder[2] = new Queue<NetworkStateData>(new NetworkStateData[] { new NetworkInteractData() });
        Assert.That(frame.Packets, Is.Empty);
        Assert.That(frame.FlushNetwork(32), Is.False);
        Assert.That(frame.FlushNetwork(33), Is.True);
        Assert.That(frame.Packets.Count, Is.EqualTo(1));
        Assert.That(frame.Packets[0].Select(tick => tick.frameId), Is.EqualTo(new uint[] { 0, 1 }));
        Assert.That(frame.Packets[0][0].datas, Is.EqualTo(new NetworkStateData[] { first, second }));
        Assert.That(frame.sendOrder.ContainsKey(2), Is.True);
        frame.OnTick();
        frame.FlushNetwork(10_000);
        Assert.That(frame.Packets.Count, Is.EqualTo(2));
        Assert.That(frame.FlushNetwork(10_000), Is.False);
        frame.Stop();
        Assert.That(frame.sendOrder, Is.Empty);
    }

    [Test]
    public void SnapshotsMergeButSpawnAttackAndDespawnRemainOrdered()
    {
        Guid id = Guid.NewGuid();
        var oldMove = new NetworkMoveStateData { unitId = id };
        var move = new NetworkMoveStateData { unitId = id };
        var attack = new NetworkPrimaryPressData { unitId = id };
        var spawn = new NetworkEntitySpawnStateData { unitId = id };
        var despawn = new NetworkEntityDespawnStateData { unitId = id };
        var nextLifeMove = new NetworkMoveStateData { unitId = id };
        var frames = new List<NetworkFrameData>
        {
            new() { frameId = 0, datas = new() { spawn, oldMove } },
            new() { frameId = 1, datas = new() { move, attack, despawn, spawn, nextLifeMove } },
        };
        NetworkSnapshotBatchUtility.Coalesce(frames);
        Assert.That(frames.SelectMany(frame => frame.datas),
            Is.EqualTo(new NetworkStateData[] { spawn, move, attack, despawn, spawn, nextLifeMove }));
        Assert.That(frames[1].frameId, Is.EqualTo(1));
    }

    [Test]
    public void ServerConsumesEveryTickInOnePacketOnceAtItsOwnTick()
    {
        MessageCodec.Init();
        Guid id = Guid.NewGuid();
        var connect = new Connect();
        var server = new ServerFrameManager { currentFrame = 4 };
        server.AddConnect(connect, id);
        var packet = new General_FrameStateData
        {
            frames = new()
            {
                new() { frameId = 4, datas = new() { new NetworkPrimaryPressData { unitId = id } } },
                new() { frameId = 5, datas = new() { new NetworkInteractData { unitId = id } } },
            },
        };
        server.OnReceiveMessage(packet, connect);
        server.OnReceiveMessage(packet, connect);
        var applied = new List<uint>();
        server.onHandleReceive = (tick, states) => { applied.Add(tick); Assert.That(states.Count, Is.EqualTo(1)); };
        server.HandleReceive();
        Assert.That(applied, Is.EqualTo(new uint[] { 4 }));
        server.currentFrame++;
        server.HandleReceive();
        Assert.That(applied, Is.EqualTo(new uint[] { 4, 5 }));
        server.Stop();
    }

    private sealed class RecordingClient : ClientFrameManager
    {
        public readonly List<List<NetworkFrameData>> Packets = new();
        protected override void SendFrames(List<NetworkFrameData> frames) => Packets.Add(frames);
    }
}

[DisableAutoCreation]
public partial class OrdinaryTimingProbe : SystemBase
{
    public readonly List<float> Deltas = new();
    protected override void OnUpdate() => Deltas.Add(SystemAPI.Time.DeltaTime);
}

[DisableAutoCreation]
public partial class PlayerTimingProbe : SystemBase
{
    public readonly List<float> Deltas = new();
    protected override void OnUpdate()
    {
        Assert.That(SystemAPI.GetSingleton<BattleSimulationScope>().Pass, Is.EqualTo(BattleSimulationPass.Players));
        Deltas.Add(SystemAPI.Time.DeltaTime);
    }
}

[DisableAutoCreation]
public partial class ControlTimingProbe : SystemBase
{
    protected override void OnUpdate()
    {
        new UnitControlTickJob
        {
            LocalPlayers = GetComponentLookup<NetworkPlayerComponent>(true),
            Scope = SystemAPI.GetSingleton<BattleSimulationScope>(),
            Players = GetComponentLookup<PlayerInputComponent>(true),
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.Run();
    }
}
