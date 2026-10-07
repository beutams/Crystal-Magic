using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CrystalMagic.Core;
using NUnit.Framework;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

public sealed class NetworkHotPathTests
{
    private bool _profilerWasEnabled;

    [SetUp]
    public void EnableAllocationRecording()
    {
        _profilerWasEnabled = UnityEngine.Profiling.Profiler.enabled;
        UnityEngine.Profiling.Profiler.enabled = true;
    }

    [TearDown]
    public void RestoreProfilerState() => UnityEngine.Profiling.Profiler.enabled = _profilerWasEnabled;

    [Test]
    public void AllocationRecorderDetectsManagedAllocation()
    {
        using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC Allocated In Frame", 0);
        long before = recorder.CurrentValue;
        var allocation = new byte[32768];
        long allocated = recorder.CurrentValue - before;
        GC.KeepAlive(allocation);
        Assert.That(allocated, Is.GreaterThanOrEqualTo(32768),
            "The allocation regression needs a working Unity allocation recorder.");
    }

    [Test]
    public void ReportReceiveAndCollectionCosts()
    {
        foreach (int monsters in new[] { 0, 1000 })
        {
            Sample receive = MeasureReceive(monsters);
            Sample collect = MeasureCollection(monsters);
            TestContext.WriteLine($"NetworkHotPath monsters={monsters}: receive={receive.Milliseconds:F4} ms, {receive.Bytes:F0} B; collect={collect.Milliseconds:F4} ms, {collect.Bytes:F0} B per tick");
        }
    }

    [Test]
    public void ServerReceiveAllocationDoesNotScaleWithMonsterCount()
    {
        Sample small = MeasureReceive(0);
        Sample large = MeasureReceive(1000);
        Assert.That(large.Bytes, Is.LessThanOrEqualTo(small.Bytes + 2048),
            "Applying player input must not allocate a new mapping of all monsters every tick.");
    }

    [Test]
    public void ServerInputMappingContainsPlayersOnlyAndKeepsFrameOrder()
    {
        using Runtime runtime = new(2);
        var first = new LookupState { unitId = runtime.PlayerId, Expected = true };
        var second = new LookupState { unitId = runtime.MonsterId, Expected = false };
        runtime.Enqueue(7, first, second);
        runtime.Enqueue(8, first);
        runtime.Receive.Update();
        Assert.That(first.Frames, Is.EqualTo(new uint[] { 7, 8 }));
        Assert.That(second.Frames, Is.EqualTo(new uint[] { 7 }));
    }

    [Test]
    public void ReceiveRefreshesMappingAfterPlayerReplacementAndIdentityChange()
    {
        using Runtime runtime = new(0);
        Entity oldPlayer = runtime.Player;
        runtime.Enqueue(1, new NetworkPlayerInputStateData { unitId = runtime.PlayerId, moveX = 1 });
        runtime.Receive.Update();
        runtime.Manager.DestroyEntity(oldPlayer);
        Entity replacement = runtime.CreateUnit(runtime.PlayerId, true);
        runtime.Enqueue(2, new NetworkPlayerInputStateData { unitId = runtime.PlayerId, moveY = 1 });
        runtime.Receive.Update();
        Assert.That(runtime.Manager.GetComponentData<PlayerInputComponent>(replacement).Move, Is.EqualTo(new float2(0, 1)));

        Guid newId = Guid.NewGuid();
        runtime.Manager.SetComponentData(replacement, new NetworkIdentityComponent { id = newId });
        runtime.Enqueue(3, new LookupState { unitId = runtime.PlayerId, Expected = false },
            new NetworkPlayerInputStateData { unitId = newId, moveX = -1 });
        runtime.Receive.Update();
        Assert.That(runtime.Manager.GetComponentData<PlayerInputComponent>(replacement).Move, Is.EqualTo(new float2(-1, 0)));
    }

    [Test]
    public void ClientBatchSeesSpawnedEntitiesAndRefreshesAfterDestroy()
    {
        using Runtime runtime = new(0, client: true);
        Guid id = Guid.NewGuid();
        var spawn = new RegisterState { unitId = id };
        var lookup = new LookupState { unitId = id, Expected = true };
        runtime.Enqueue(4, spawn, lookup);
        runtime.Enqueue(5, lookup);
        runtime.Receive.Update();
        Assert.That(lookup.Frames, Is.EqualTo(new uint[] { 4, 5 }));
        runtime.Manager.DestroyEntity(spawn.Entity);
        runtime.Enqueue(6, new LookupState { unitId = id, Expected = false });
        runtime.Receive.Update();
    }

    [Test]
    public void CollectionSeparatesPlayerAndWorldDirtyStates()
    {
        using Runtime runtime = new(1);
        runtime.Frame.running = true;
        runtime.Frame.currentFrame = 7;
        runtime.DirtyUnits();
        runtime.SetPass(BattleSimulationPass.Players);
        runtime.Collect.Update();
        Assert.That(runtime.Frame.sendOrder[7].All(state => state.unitId == runtime.PlayerId), Is.True);
        Assert.That(runtime.Manager.GetComponentData<UnitManaComponent>(runtime.Monster).NetworkDirty, Is.EqualTo(1));
        Assert.That(runtime.Manager.GetComponentData<UnitMoveComponent>(runtime.Monster).NetworkDirty, Is.EqualTo(1));
        Assert.That(runtime.Manager.GetComponentData<UnitManaComponent>(runtime.Player).NetworkDirty, Is.Zero);

        runtime.SetPass(BattleSimulationPass.World);
        runtime.Collect.Update();
        Assert.That(runtime.Frame.sendOrder[6].All(state => state.unitId == runtime.MonsterId), Is.True);
        Assert.That(runtime.Manager.GetComponentData<UnitMoveComponent>(runtime.Monster).NetworkDirty, Is.Zero);
    }

    [Test]
    public void AllPassCollectsBothScopesAndPlayerPassPreservesCrossEntityEvents()
    {
        using Runtime runtime = new(1);
        runtime.Frame.running = true;
        runtime.Frame.currentFrame = 7;
        runtime.DirtyUnits();
        runtime.SetPass(BattleSimulationPass.All);
        runtime.Collect.Update();
        Assert.That(runtime.Frame.sendOrder[6].OfType<NetworkMoveStateData>().Select(state => state.unitId),
            Is.EquivalentTo(new[] { runtime.PlayerId, runtime.MonsterId }));

        Entity queueEntity = runtime.Manager.CreateEntity();
        var queue = new NetworkPresentationEventQueueComponent();
        var presentation = new NetworkPresentationEventStateData { unitId = runtime.MonsterId };
        queue.Events.Add(presentation);
        runtime.Manager.AddComponentObject(queueEntity, queue);
        runtime.SetPass(BattleSimulationPass.Players);
        runtime.Collect.Update();
        runtime.Collect.Update();
        Assert.That(runtime.Frame.sendOrder[7].OfType<NetworkPresentationEventStateData>().Single(), Is.SameAs(presentation));
        Assert.That(queue.Events, Is.Empty);
    }

    [Test]
    public void FullSnapshotIncludesBothScopesAndDoesNotConsumePlayerDirtyState()
    {
        using Runtime runtime = new(1);
        runtime.Frame.running = true;
        runtime.Frame.currentFrame = 7;
        runtime.DirtyUnits();
        runtime.SetPass(BattleSimulationPass.World);
        List<NetworkStateData> snapshot = null;
        runtime.Collect.RequestSnapshot((_, _, states) => snapshot = states);
        runtime.Collect.Update();
        Assert.That(snapshot, Is.Not.Null);
        Assert.That(snapshot.OfType<NetworkMoveStateData>().Select(state => state.unitId),
            Is.EquivalentTo(new[] { runtime.PlayerId, runtime.MonsterId }));
        Assert.That(runtime.Manager.GetComponentData<UnitMoveComponent>(runtime.Player).NetworkDirty, Is.EqualTo(1));
    }

    [Test]
    public void PlayerPassStillCollectsCrossEntityDespawnExactlyOnce()
    {
        using Runtime runtime = new(1);
        runtime.Frame.running = true;
        runtime.Frame.currentFrame = 7;
        runtime.Manager.AddComponent<DestroyEntityFlag>(runtime.Monster);
        runtime.Manager.SetComponentEnabled<DestroyEntityFlag>(runtime.Monster, true);
        runtime.SetPass(BattleSimulationPass.Players);
        runtime.Collect.Update();
        runtime.Collect.Update();
        Assert.That(runtime.Frame.sendOrder[7].OfType<NetworkEntityDespawnStateData>()
            .Count(state => state.unitId == runtime.MonsterId), Is.EqualTo(1));
    }

    [TestCase(BattleSimulationPass.Players)]
    [TestCase(BattleSimulationPass.World)]
    public void DisabledDeathComponentStillSendsDirtyAliveState(BattleSimulationPass pass)
    {
        using Runtime runtime = new(1);
        runtime.Frame.running = true;
        runtime.Frame.currentFrame = 7;
        Entity entity = pass == BattleSimulationPass.Players ? runtime.Player : runtime.Monster;
        runtime.Manager.AddComponentData(entity, new UnitDeathComponent { NetworkDirty = 1 });
        runtime.Manager.SetComponentEnabled<UnitDeathComponent>(entity, false);
        runtime.SetPass(pass);
        runtime.Collect.Update();
        uint frame = pass == BattleSimulationPass.Players ? 7u : 6u;
        Assert.That(runtime.Frame.sendOrder[frame].OfType<NetworkDeathStateData>().Single().isDead, Is.Zero);
    }

    private static Sample MeasureReceive(int monsters)
    {
        using Runtime runtime = new(monsters);
        var input = new NetworkPlayerInputStateData { unitId = runtime.PlayerId, moveX = 1 };
        using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC Allocated In Frame", 0);
        long bytes = 0, ticks = 0;
        for (uint i = 0; i < 80; i++)
        {
            runtime.Enqueue(i, input);
            long beforeBytes = recorder.CurrentValue;
            long beforeTime = Stopwatch.GetTimestamp();
            runtime.Receive.Update();
            long elapsed = Stopwatch.GetTimestamp() - beforeTime;
            long allocated = recorder.CurrentValue - beforeBytes;
            if (i < 16) continue;
            ticks += elapsed;
            bytes += allocated;
        }
        return new Sample(ticks * 1000d / Stopwatch.Frequency / 64, bytes / 64d);
    }

    private static Sample MeasureCollection(int monsters)
    {
        using Runtime runtime = new(monsters);
        runtime.Frame.running = true;
        runtime.SetPass(BattleSimulationPass.Players);
        using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC Allocated In Frame", 0);
        long bytes = 0, ticks = 0;
        for (uint i = 0; i < 80; i++)
        {
            runtime.Frame.currentFrame = i;
            runtime.Frame.sendOrder.Clear();
            runtime.DirtyPlayer();
            long beforeBytes = recorder.CurrentValue;
            long beforeTime = Stopwatch.GetTimestamp();
            runtime.Collect.Update();
            long elapsed = Stopwatch.GetTimestamp() - beforeTime;
            long allocated = recorder.CurrentValue - beforeBytes;
            if (i < 16) continue;
            ticks += elapsed;
            bytes += allocated;
        }
        return new Sample(ticks * 1000d / Stopwatch.Frequency / 64, bytes / 64d);
    }

    private readonly struct Sample
    {
        public readonly double Milliseconds;
        public readonly double Bytes;
        public Sample(double milliseconds, double bytes) => (Milliseconds, Bytes) = (milliseconds, bytes);
    }

    private sealed class LookupState : NetworkStateData
    {
        public bool Expected;
        public readonly List<uint> Frames = new();
        public override void Apply(NetworkStateApplyContext context)
        {
            Assert.That(context.TryGetEntity(unitId, out _), Is.EqualTo(Expected));
            Frames.Add(context.Frame);
        }
    }

    private sealed class RegisterState : NetworkStateData
    {
        public Entity Entity;
        public override void Apply(NetworkStateApplyContext context)
        {
            Entity = context.EntityManager.CreateEntity(typeof(NetworkIdentityComponent));
            context.EntityManager.SetComponentData(Entity, new NetworkIdentityComponent { id = unitId });
            context.RegisterEntity(unitId, Entity);
        }
    }

    private sealed class Runtime : IDisposable
    {
        private readonly World _world;
        private readonly Entity _scope;
        private readonly FrameReceiveBufferComponent _buffer;
        public EntityManager Manager => _world.EntityManager;
        public readonly ServerFrameManager Frame = new();
        public readonly FrameReceiveSystem Receive;
        public readonly ServerNetworkStateCollectSystem Collect;
        public readonly Guid PlayerId = Guid.NewGuid();
        public readonly Guid MonsterId = Guid.NewGuid();
        public Entity Player { get; }
        public Entity Monster { get; }

        public Runtime(int monsters, bool client = false)
        {
            _world = new World("Network hot path test", client ? WorldFlags.GameClient : WorldFlags.GameServer);
            Entity role = Manager.CreateEntity(typeof(GameWorldContextComponent));
            Manager.SetComponentData(role, new GameWorldContextComponent { Role = client ? GameWorldRole.Client : GameWorldRole.Server });
            Entity binding = Manager.CreateEntity();
            Manager.AddComponentObject(binding, new FrameManagerComponent { manager = client ? new ClientFrameManager() : Frame });
            _scope = Manager.CreateEntity(typeof(BattleSimulationScope));
            SetPass(BattleSimulationPass.Players);
            Player = CreateUnit(PlayerId, true);
            for (int i = 0; i < monsters; i++)
            {
                Entity monster = CreateUnit(i == 0 ? MonsterId : Guid.NewGuid(), false);
                if (i == 0) Monster = monster;
            }
            Receive = _world.GetOrCreateSystemManaged<FrameReceiveSystem>();
            Collect = _world.GetOrCreateSystemManaged<ServerNetworkStateCollectSystem>();
            using EntityQuery query = Manager.CreateEntityQuery(typeof(FrameReceiveBufferComponent));
            _buffer = Manager.GetComponentObject<FrameReceiveBufferComponent>(query.GetSingletonEntity());
        }

        public Entity CreateUnit(Guid id, bool player)
        {
            Entity entity = Manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(UnitMoveComponent),
                typeof(LocalTransform), typeof(UnitManaComponent), typeof(UnitVitalityComponent), typeof(UnitFacingComponent));
            Manager.SetComponentData(entity, new NetworkIdentityComponent { id = id });
            Manager.SetComponentData(entity, LocalTransform.Identity);
            if (player) Manager.AddComponent<PlayerInputComponent>(entity);
            return entity;
        }

        public void Enqueue(uint frame, params NetworkStateData[] states)
        {
            var queue = new Queue<NetworkState>();
            foreach (NetworkStateData state in states) queue.Enqueue(new NetworkState { data = state });
            _buffer.frames[frame] = queue;
        }

        public void SetPass(BattleSimulationPass pass) => Manager.SetComponentData(_scope, new BattleSimulationScope { Pass = pass });
        public void DirtyPlayer() => Dirty(Player);
        public void DirtyUnits() { Dirty(Player); Dirty(Monster); }
        private void Dirty(Entity entity)
        {
            Manager.SetComponentData(entity, new UnitMoveComponent { NetworkDirty = 1 });
            Manager.SetComponentData(entity, new UnitManaComponent { NetworkDirty = 1 });
            Manager.SetComponentData(entity, new UnitVitalityComponent { NetworkDirty = 1 });
            Manager.SetComponentData(entity, new UnitFacingComponent { NetworkDirty = 1 });
        }
        public void Dispose() => _world.Dispose();
    }
}
