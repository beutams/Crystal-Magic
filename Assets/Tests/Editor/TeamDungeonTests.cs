using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class TeamDungeonTests
{
    [Test]
    public void RevengeWavesShareSpeedAndTargetWithoutCombiningDifferentWaves()
    {
        using var f = new SquadWorld();
        f.Player(100); f.Player(-100);
        Entity one = f.Member(1, 10, 3);
        Entity two = f.Member(1, 12, 5);
        Entity otherWave = f.Member(2, -20, 2);
        f.Tick();
        Assert.That(f.Destination(one), Is.EqualTo(f.Destination(two)));
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(one).CommandMoveSpeed, Is.EqualTo(3));
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(two).CommandMoveSpeed, Is.EqualTo(3));
        Assert.That(f.Destination(one).x, Is.GreaterThan(12));
        Assert.That(f.Destination(otherWave).x, Is.LessThan(-20));
    }

    [Test]
    public void FrontMembersWaitForSleepingRearMembersBeforeAdvancing()
    {
        using var f = new SquadWorld();
        f.Player(100);
        Entity front = f.Member(1, 0), rear = f.Member(1, -2);
        f.Manager.SetEnabled(rear, false);
        f.Tick();
        float3 goal = f.Destination(front);
        f.Position(front, goal.x + 0.25f);
        f.Tick();
        Assert.That(f.Destination(front).x, Is.EqualTo(goal.x + 0.25f));
        Assert.That(f.Destination(rear), Is.EqualTo(goal));
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(rear).CommandMoveSpeed, Is.EqualTo(3));
        f.Position(rear, goal.x);
        f.Tick();
        Assert.That(f.Destination(front).x, Is.GreaterThan(goal.x + 3));
        Assert.That(f.Destination(rear), Is.EqualTo(f.Destination(front)));
    }

    [Test]
    public void EnemyContactReleasesMarchingForTheWholeSquadAndResumesAfterCombat()
    {
        using var f = new SquadWorld();
        Entity player = f.Player(100);
        Entity one = f.Member(1, 0), two = f.Member(1, -1);
        f.Tick();
        f.Manager.AddBuffer<UnitPerceptionUnitElement>(one).Add(new UnitPerceptionUnitElement { Value = player, Faction = UnitFactionType.Player });
        f.Tick();
        Assert.That(f.Bool(one, "dungeon.revenge.hasDestination"), Is.False);
        Assert.That(f.Bool(two, "dungeon.revenge.hasDestination"), Is.False);
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(two).CommandMoveSpeed, Is.EqualTo(-1));
        f.Manager.GetBuffer<UnitPerceptionUnitElement>(one).Clear();
        f.Tick();
        Assert.That(f.Bool(two, "dungeon.revenge.hasDestination"), Is.True);
    }

    [Test]
    public void GhostPlayersAreNotSelectedAsRevengeTargets()
    {
        using var f = new SquadWorld();
        Entity ghost = f.Player(-1);
        f.Manager.AddComponent<BattleSpectatorComponent>(ghost);
        f.Player(100);
        Entity member = f.Member(1, 0);
        f.Tick();
        Assert.That(f.Destination(member).x, Is.GreaterThan(0));
    }

    [Test]
    public void SquadMapSnapshotsIncludeSleepersExcludeDeathsAndReplaceStaleClientMarkers()
    {
        using var f = new SquadWorld();
        f.Member(1, 0);
        Entity sleeper = f.Member(1, 10);
        f.Manager.SetEnabled(sleeper, false);
        f.Member(2, 20);
        Entity dead = f.Member(2, 100);
        f.Manager.SetComponentEnabled<UnitDeathComponent>(dead, true);
        using EntityQuery query = f.Manager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<DungeonRevengeSquadMember>(), ComponentType.ReadOnly<LocalTransform>() },
            None = new[] { ComponentType.ReadOnly<UnitDeathComponent>(), ComponentType.ReadOnly<DestroyEntityFlag>() },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        });
        NetworkRevengeSquadMapStateData state = NetworkRevengeSquadMapStateData.Capture(f.Manager, query);
        Assert.That(state.points.Select(p => p.x), Is.EqualTo(new[] { 5f, 20f }));
        MessageCodec.Init();
        var message = new B2C_ReloadBattleSnapshot { states = new List<NetworkStateData> { state } };
        Assert.That(MessageCodec.TryDecode(MessageCodec.Encode(message), out _, out IMessage decoded, out Exception error),
            Is.EqualTo(MessageDecodeResult.Success), error?.ToString());
        using World client = new("Squad map client", WorldFlags.GameClient);
        GameSingletonUtility.Create(client.EntityManager, GameWorldRole.Client, GameSceneMode.Dungeon);
        var context = new NetworkStateApplyContext(client.EntityManager, 1, 33);
        ((B2C_ReloadBattleSnapshot)decoded).states[0].Apply(context);
        Entity presentation = context.GetOrCreateClientPresentationEntity();
        Assert.That(client.EntityManager.GetBuffer<DungeonRevengeSquadMapElement>(presentation).Length, Is.EqualTo(2));
        new NetworkRevengeSquadMapStateData().Apply(context);
        Assert.That(client.EntityManager.GetBuffer<DungeonRevengeSquadMapElement>(presentation).Length, Is.Zero);
        Assert.That(client.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DungeonRevengeSquadMapElement>()).CalculateEntityCount(), Is.EqualTo(1));
    }

    [Test]
    public void SpawnSnapshotsRetainRevengeMembershipForReconnect()
    {
        using var f = new SquadWorld();
        Entity member = f.Member(7, 0);
        Guid id = Guid.NewGuid();
        f.Manager.AddComponentData(member, new NetworkIdentityComponent { id = id });
        f.Manager.AddComponentObject(member, new NetworkEntitySpawnInfoComponent
            { entityInfo = new NetworkEntitySpawnInfo { unitId = id, revengeSquadId = 7, revengeSquadSpeed = 3 } });
        NetworkEntitySpawnInfo snapshot = NetworkEntitySpawnUtility.CreateSnapshotInfo(f.Manager, member);
        Assert.That(snapshot.revengeSquadId, Is.EqualTo(7));
        Assert.That(snapshot.revengeSquadSpeed, Is.EqualTo(3));
    }

    [Test]
    public void OnlineLootHistoryIsPerPlayerAndSurvivesTransfersWithoutCountingTwice()
    {
        using var f = new SquadWorld();
        GameSingletonUtility.Create(f.Manager, GameWorldRole.Server, GameSceneMode.Dungeon);
        Entity first = f.Player(0), second = f.Player(1);
        DungeonSettlementUtility.RecordItemAcquired(f.Manager, 10, 2, first);
        DungeonSettlementUtility.RecordMoneyAcquired(f.Manager, 7, first);
        DungeonSettlementUtility.RecordItemAcquired(f.Manager, 20, 3, second);
        var player = new BattlePlayer();
        PlayerCharacterComponent character = f.Manager.GetComponentObject<PlayerCharacterComponent>(first);
        MethodInfo capture = typeof(ServerBattleManager).GetMethod("CaptureAcquisitions", BindingFlags.Static | BindingFlags.NonPublic);
        capture.Invoke(null, new object[] { player, character });
        capture.Invoke(null, new object[] { player, character });
        Assert.That(player.acquiredItems.Single().ItemId, Is.EqualTo(10));
        Assert.That(player.acquiredItems.Single().Quantity, Is.EqualTo(2));
        Assert.That(player.acquiredMoney, Is.EqualTo(7));
        Assert.That(f.Manager.GetComponentObject<PlayerCharacterComponent>(second).AcquiredItems.Single().ItemId, Is.EqualTo(20));
        DungeonSettlementUtility.RecordItemAcquired(f.Manager, 10, 1, first);
        capture.Invoke(null, new object[] { player, character });
        Assert.That(player.acquiredItems.Single().Quantity, Is.EqualTo(3));
    }

    private sealed class SquadWorld : IDisposable
    {
        private readonly World _world = new("Revenge squad regression");
        private readonly SystemHandle _system;
        public EntityManager Manager => _world.EntityManager;
        private double _time;
        public SquadWorld() => _system = _world.GetOrCreateSystem<DungeonRevengeSquadSystem>();
        public Entity Player(float x)
        {
            Entity player = Manager.CreateEntity(typeof(LocalTransform));
            Manager.AddComponentObject(player, new PlayerCharacterComponent());
            Position(player, x);
            return player;
        }
        public Entity Member(int id, float x, float speed = 3)
        {
            Entity member = Manager.CreateEntity(typeof(LocalTransform), typeof(UnitNavigationComponent), typeof(UnitMoveComponent),
                typeof(UnitVariableComponent), typeof(UnitDeathComponent), typeof(DestroyEntityFlag));
            Manager.SetComponentEnabled<UnitDeathComponent>(member, false);
            Manager.SetComponentEnabled<DestroyEntityFlag>(member, false);
            Manager.AddComponentData(member, new DungeonRevengeSquadMember { SquadId = id, Speed = speed });
            Manager.AddBuffer<UnitVariableElement>(member);
            Manager.SetComponentData(member, new UnitNavigationComponent { ClearanceRadius = 0.5f });
            Position(member, x);
            return member;
        }
        public void Position(Entity entity, float x) => Manager.SetComponentData(entity, LocalTransform.FromPosition(x, 0, 0));
        public bool Bool(Entity entity, string key) => UnitVariableSource.TryGetValue(Manager, entity, key, out var value) && value.TryGetBool(out bool flag) && flag;
        public float3 Destination(Entity entity)
        {
            Assert.That(UnitVariableSource.TryGetValue(Manager, entity, "dungeon.revenge.destination", out var value), Is.True);
            Assert.That(value.TryGetFloat3(out float3 position), Is.True);
            return position;
        }
        public void Tick()
        {
            _time += 0.1;
            _world.SetTime(new Unity.Core.TimeData(_time, 0.1f));
            _system.Update(_world.Unmanaged);
            Manager.CompleteAllTrackedJobs();
        }
        public void Dispose() => _world.Dispose();
    }
}
