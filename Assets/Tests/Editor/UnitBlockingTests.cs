using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Collider = Unity.Physics.Collider;

public sealed class UnitBlockingTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void MovingUnitStopsBeforeIdleOpponentWithoutMovingIt(bool playerMoves)
    {
        using var collider = Sphere();
        RigidBody a = Body(collider, 0), b = Body(collider, 2);
        float2 va = playerMoves ? new float2(10, 0) : float2.zero;
        float2 vb = playerMoves ? float2.zero : new float2(-10, 0);
        Assert.That(UnitBlockingUtility.Constrain(in a, in b, 0.2f, ref va, ref vb), Is.True);
        Assert.That(playerMoves ? vb : va, Is.EqualTo(float2.zero));
        Assert.That(2 + (vb.x - va.x) * 0.2f, Is.GreaterThanOrEqualTo(1f));
    }

    [Test]
    public void HeadOnMotionStopsBothInsteadOfPushingEitherBackwards()
    {
        using var collider = Sphere();
        RigidBody a = Body(collider, -1), b = Body(collider, 1);
        float2 va = new(20, 0), vb = new(-10, 0);
        UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb);
        Assert.That(va.x, Is.InRange(0, 20));
        Assert.That(vb.x, Is.InRange(-10, 0));
        Assert.That(2 + (vb.x - va.x) * 0.1f, Is.GreaterThanOrEqualTo(1f));
    }

    [Test]
    public void TangentialMovementAndOutwardMovementArePreserved()
    {
        using var collider = Sphere();
        RigidBody a = Body(collider, 0), b = Body(collider, 1);
        float2 va = new(5, 3), vb = float2.zero;
        UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb);
        Assert.That(va.x, Is.EqualTo(0).Within(0.0001));
        Assert.That(va.y, Is.EqualTo(3).Within(0.0001));
        va = new float2(-5, 3);
        Assert.That(UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb), Is.False);
        Assert.That(va, Is.EqualTo(new float2(-5, 3)));
    }

    [Test]
    public void FasterPursuerIsLimitedWhileUnitAheadKeepsItsOwnSpeed()
    {
        using var collider = Sphere();
        RigidBody a = Body(collider, 0), b = Body(collider, 1);
        float2 va = new(6, 0), vb = new(4, 0);
        UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb);
        Assert.That(va.x, Is.EqualTo(4).Within(0.0001));
        Assert.That(vb.x, Is.EqualTo(4).Within(0.0001));
    }

    [Test]
    public void ExistingOverlapDoesNotInventSeparationVelocity()
    {
        using var collider = Sphere();
        RigidBody a = Body(collider, 0), b = Body(collider, 0.8f);
        float2 va = float2.zero, vb = float2.zero;
        Assert.That(UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb), Is.False);
        Assert.That(va, Is.EqualTo(float2.zero));
        Assert.That(vb, Is.EqualTo(float2.zero));
        va = new float2(-1, 0);
        UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb);
        Assert.That(va.x, Is.EqualTo(-1));
    }

    [Test]
    public void FastSweepCannotTunnelThroughAnOpponent()
    {
        using var collider = Sphere();
        RigidBody a = Body(collider, 0), b = Body(collider, 5);
        float2 va = new(300, 0), vb = float2.zero;
        UnitBlockingUtility.Constrain(in a, in b, 1f / 30, ref va, ref vb);
        Assert.That(va.x / 30, Is.LessThanOrEqualTo(4));
        Assert.That(vb, Is.EqualTo(float2.zero));
    }

    [Test]
    public void SweepsRespectColliderOffsetAndScale()
    {
        using var offset = SphereCollider.Create(new SphereGeometry
        {
            Center = new float3(0.2f, 0, 0), Radius = 0.25f,
        }, CollisionFilter.Default);
        using var sphere = Sphere();
        RigidBody a = Body(offset, 0), b = Body(sphere, 2);
        a.Scale = 2;
        float2 va = new(10, 0), vb = float2.zero;
        UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb);
        Assert.That(va.x * 0.1f, Is.EqualTo(0.6f - UnitBlockingUtility.Skin).Within(0.001));
    }

    [Test]
    public void RemoteProxyIsReadOnlyAndDoesNotCreateAnIdlePlayerVelocity()
    {
        using var collider = Sphere();
        RigidBody a = Body(collider, 0), b = Body(collider, 1);
        float2 va = float2.zero, vb = new(-4, 0);
        UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb, true, false);
        Assert.That(va, Is.EqualTo(float2.zero));
        Assert.That(vb, Is.EqualTo(new float2(-4, 0)));
        va = new float2(4, 0);
        UnitBlockingUtility.Constrain(in a, in b, 0.1f, ref va, ref vb, true, false);
        Assert.That(va.x, Is.EqualTo(0).Within(0.0001));
        Assert.That(vb.x, Is.EqualTo(-4));
    }

    [TestCase(UnitFactionType.Player, UnitFactionType.Enemy, true)]
    [TestCase(UnitFactionType.Boss, UnitFactionType.Player, true)]
    [TestCase(UnitFactionType.Enemy, UnitFactionType.Enemy, false)]
    [TestCase(UnitFactionType.Player, UnitFactionType.Friend, false)]
    [TestCase(UnitFactionType.Player, UnitFactionType.Interactable, false)]
    public void OnlyPlayerHostilePairsUseMutualBlocking(UnitFactionType a, UnitFactionType b, bool blocks) =>
        Assert.That(UnitBlockingUtility.IsHostilePair(a, b), Is.EqualTo(blocks));

    [TestCase(GameWorldRole.Standalone)]
    [TestCase(GameWorldRole.Server)]
    [TestCase(GameWorldRole.Client)]
    public void RealPhysicsContactDoesNotDisplaceIdlePlayer(GameWorldRole role)
    {
        using var fixture = new PhysicsFixture(role);
        Entity player = fixture.AddUnit(UnitFactionType.Player, float3.zero);
        Entity enemy = fixture.AddUnit(UnitFactionType.Enemy, new float3(0.98f, 0, 0));
        for (int i = 0; i < 12; i++)
        {
            fixture.SetVelocity(player, float2.zero);
            fixture.SetVelocity(enemy, new float2(-4, 0));
            fixture.Step();
        }
        Assert.That(math.length(fixture.Position(player)), Is.LessThan(0.0001));
    }

    [TestCase(GameWorldRole.Standalone)]
    [TestCase(GameWorldRole.Server)]
    [TestCase(GameWorldRole.Client)]
    public void PlayerCannotCrossIdleEnemyOrMoveIt(GameWorldRole role)
    {
        using var fixture = new PhysicsFixture(role);
        Entity player = fixture.AddUnit(UnitFactionType.Player, float3.zero);
        Entity enemy = fixture.AddUnit(UnitFactionType.Enemy, new float3(2, 0, 0));
        for (int i = 0; i < 30; i++)
        {
            fixture.SetVelocity(player, new float2(6, 0));
            fixture.SetVelocity(enemy, float2.zero);
            fixture.Step();
        }
        Assert.That(fixture.Position(player).x, Is.InRange(0.9f, 1.001f));
        Assert.That(fixture.Position(enemy).x, Is.EqualTo(2).Within(0.0001));
    }

    [TestCase(GameWorldRole.Standalone)]
    [TestCase(GameWorldRole.Server)]
    [TestCase(GameWorldRole.Client)]
    public void WallContactsAndExplicitControlMotionRemainEffective(GameWorldRole role)
    {
        using var fixture = new PhysicsFixture(role);
        Entity player = fixture.AddUnit(UnitFactionType.Player, float3.zero);
        fixture.AddWall(new float3(-2, 0, 0));
        fixture.Manager.AddComponentData(player, new UnitControlRuntimeComponent
        {
            LockMove = 1, ActiveMotionVelocity = new float2(-4, 0),
        });
        var move = fixture.World.GetOrCreateSystem<UnitMoveSystem>();
        for (int i = 0; i < 30; i++)
        {
            move.Update(fixture.World.Unmanaged);
            fixture.Step();
        }
        Assert.That(fixture.Position(player).x, Is.InRange(-1.1f, -0.8f),
            "Explicit knockback still moves its victim, and the wall still stops it.");
    }

    [Test]
    public void SeveralEnemiesDoNotPermitOrderDependentCrossing()
    {
        using var fixture = new PhysicsFixture(GameWorldRole.Server);
        Entity player = fixture.AddUnit(UnitFactionType.Player, float3.zero);
        Entity a = fixture.AddUnit(UnitFactionType.Enemy, new float3(1.3f, -0.55f, 0));
        Entity b = fixture.AddUnit(UnitFactionType.Enemy, new float3(1.3f, 0.55f, 0));
        for (int i = 0; i < 30; i++)
        {
            fixture.SetVelocity(player, new float2(6, 0));
            fixture.SetVelocity(a, float2.zero);
            fixture.SetVelocity(b, float2.zero);
            fixture.Step();
        }
        Assert.That(fixture.Position(player).x, Is.LessThan(0.51f));
    }

    [Test]
    public void EnemiesBehindTheFrontRowCannotPushItThroughThePlayer()
    {
        using var fixture = new PhysicsFixture(GameWorldRole.Server);
        Entity player = fixture.AddUnit(UnitFactionType.Player, float3.zero);
        Entity front = fixture.AddUnit(UnitFactionType.Enemy, new float3(1.01f, 0, 0));
        Entity rear = fixture.AddUnit(UnitFactionType.Enemy, new float3(2.01f, 0, 0));
        for (int i = 0; i < 30; i++)
        {
            fixture.SetVelocity(player, float2.zero);
            fixture.SetVelocity(front, new float2(-4, 0));
            fixture.SetVelocity(rear, new float2(-6, 0));
            fixture.Step();
        }
        Assert.That(fixture.Position(player).x, Is.EqualTo(0).Within(0.0001));
        Assert.That(fixture.Position(front).x, Is.GreaterThanOrEqualTo(0.999f));
    }

    private static BlobAssetReference<Collider> Sphere() => SphereCollider.Create(
        new SphereGeometry { Radius = 0.5f }, CollisionFilter.Default);

    private static RigidBody Body(BlobAssetReference<Collider> collider, float x) => new()
    {
        Collider = collider, Scale = 1, WorldFromBody = new RigidTransform(quaternion.identity, new float3(x, 0, 0)),
    };

    private sealed class PhysicsFixture : IDisposable
    {
        public readonly World World;
        public EntityManager Manager => World.EntityManager;
        private readonly GameWorldRole _role;
        private readonly BlobAssetReference<Collider> _sphere;
        private readonly BlobAssetReference<Collider> _box;
        private readonly FixedStepSimulationSystemGroup _fixed;
        private int _tick;

        public PhysicsFixture(GameWorldRole role)
        {
            _role = role;
            World = new World("Mutual unit blocking", role == GameWorldRole.Client ? WorldFlags.GameClient :
                role == GameWorldRole.Server ? WorldFlags.GameServer : WorldFlags.Game);
            Manager.SetComponentData(Manager.CreateEntity(typeof(GameWorldContextComponent)),
                new GameWorldContextComponent { Role = role, SceneMode = GameSceneMode.Dungeon });
            var flags = role == GameWorldRole.Client ? WorldSystemFilterFlags.ClientSimulation :
                role == GameWorldRole.Server ? WorldSystemFilterFlags.ServerSimulation : WorldSystemFilterFlags.LocalSimulation;
            List<Type> selected = new();
            foreach (Type type in DefaultWorldInitialization.GetAllSystems(flags))
            {
                string ns = type.Namespace ?? string.Empty;
                if (ns.StartsWith("Unity.Physics") || ns.StartsWith("Unity.Transforms") || ns == "Unity.Entities" ||
                    type == typeof(UnitBlockingSystem) || type == typeof(UnitBlockingContactSystem) ||
                    type == typeof(UnitBlockingFinalizeSystem) ||
                    type == typeof(ClientPhysicsProxySystem) || type == typeof(UnitPhysicsRotationInitializationSystem))
                    selected.Add(type);
            }
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, selected);
            _fixed = World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            _fixed.RateManager = null;
            PhysicsStep step = PhysicsStep.Default;
            step.Gravity = float3.zero;
            Manager.SetComponentData(Manager.CreateEntity(typeof(PhysicsStep)), step);
            _sphere = Sphere();
            _box = BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero, Size = new float3(1, 10, 2), Orientation = quaternion.identity,
            }, CollisionFilter.Default);
            World.SetTime(new TimeData(0, 1f / 30));
        }

        public Entity AddUnit(UnitFactionType faction, float3 position)
        {
            Entity entity = AddBody(_sphere, position);
            Manager.AddComponentData(entity, new UnitFactionComponent { Value = faction });
            Manager.AddComponentData(entity, new UnitMoveComponent
            {
                BaseMoveSpeed = 6, BaseMaxAcceleration = 10000, StateMoveMultiplier = 1, CommandMoveSpeed = -1,
            });
            Manager.AddComponentData(entity, PhysicsMass.CreateDynamic(_sphere.Value.MassProperties, 1));
            Manager.AddComponentData(entity, default(PhysicsVelocity));
            Manager.AddComponentData(entity, new PhysicsGravityFactor { Value = 0 });
            if (_role == GameWorldRole.Client && faction == UnitFactionType.Player)
                Manager.AddComponent<NetworkPlayerComponent>(entity);
            return entity;
        }

        public void AddWall(float3 position) => AddBody(_box, position);
        private Entity AddBody(BlobAssetReference<Collider> collider, float3 position)
        {
            Entity entity = Manager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider));
            Manager.SetComponentData(entity, LocalTransform.FromPosition(position));
            Manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
            Manager.AddSharedComponent(entity, new PhysicsWorldIndex());
            return entity;
        }
        public void SetVelocity(Entity entity, float2 velocity)
        {
            Manager.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(velocity, 0) });
            UnitMoveComponent move = Manager.GetComponentData<UnitMoveComponent>(entity);
            move.Velocity = velocity;
            Manager.SetComponentData(entity, move);
        }
        public float3 Position(Entity entity) => Manager.GetComponentData<LocalTransform>(entity).Position;
        public void Step()
        {
            World.SetTime(new TimeData(++_tick / 30d, 1f / 30));
            _fixed.Update();
            Manager.CompleteAllTrackedJobs();
        }
        public void Dispose()
        {
            World.Dispose();
            _sphere.Dispose();
            _box.Dispose();
        }
    }
}
