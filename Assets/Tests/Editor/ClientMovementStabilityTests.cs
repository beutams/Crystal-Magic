using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using Server;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

public sealed class ClientMovementStabilityTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void BothWorldsResolveCollisionsAtPlayerTicks(bool client)
    {
        using World world = CreateBattleWorld(client);
        using BlobAssetReference<Collider> collider = Unity.Physics.SphereCollider.Create(
            new SphereGeometry { Radius = 1 }, CollisionFilter.Default);
        Entity player = Body(world.EntityManager, collider, float3.zero, true);
        if (client)
            world.EntityManager.AddComponent<NetworkPlayerComponent>(player);
        world.EntityManager.AddComponentData(player, new PhysicsGraphicalSmoothing { ApplySmoothing = 1 });
        world.EntityManager.AddComponentData(player, new PhysicsGraphicalInterpolationBuffer
        {
            PreviousTransform = RigidTransform.identity,
        });
        Body(world.EntityManager, collider, new float3(0.5f, 0, 0), false);
        Entity physicsStep = world.EntityManager.CreateEntity(typeof(PhysicsStep));
        PhysicsStep step = PhysicsStep.Default;
        step.Gravity = float3.zero;
        world.EntityManager.SetComponentData(physicsStep, step);
        var simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();
        for (int index = 1; index <= 12; index++)
        {
            world.SetTime(new TimeData(index * 0.034, 0.034f));
            simulation.Update();
            world.EntityManager.CompleteAllTrackedJobs();
        }
        float distance = math.length(world.EntityManager.GetComponentData<LocalTransform>(player).Position);
        Assert.That(world.EntityManager.HasComponent<PhysicsGraphicalSmoothing>(player), Is.EqualTo(!client));
        Assert.That(world.EntityManager.HasComponent<PhysicsGraphicalInterpolationBuffer>(player), Is.EqualTo(!client));
        Assert.That(distance, Is.GreaterThan(0.01), "Local prediction and authority must both resolve collisions.");

        LocalTransform completed = world.EntityManager.GetComponentData<LocalTransform>(player);
        world.SetTime(new TimeData(0.409, 0.001f));
        simulation.Update();
        world.EntityManager.CompleteAllTrackedJobs();
        Assert.That(world.EntityManager.GetComponentData<LocalTransform>(player).Position,
            Is.EqualTo(completed.Position), "A render update without a player tick must not run physics again.");
    }

    [Test]
    public void CorrectionWaitsForAPlayerTickAndReplayCompletesBeforeRendering()
    {
        using World world = CreateBattleWorld(true);
        using BlobBuilder builder = new(Allocator.Temp);
        builder.ConstructRoot<StateScriptRuntimeRegistryBlob>();
        using BlobAssetReference<StateScriptRuntimeRegistryBlob> registry =
            builder.CreateBlobAssetReference<StateScriptRuntimeRegistryBlob>(Allocator.Persistent);
        var manager = world.EntityManager;
        manager.SetComponentData(manager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent)),
            new StateScriptRuntimeRegistryComponent { Value = registry });
        Entity player = Player(manager, 11f);
        manager.AddComponentData(player, new UnitStateScriptComponent { DefinitionIndex = -1 });
        manager.AddBuffer<StateScriptGraphStateElement>(player);
        manager.AddBuffer<StateScriptNodeStateElement>(player);
        var frame = GetFrame(world);
        frame.currentFrame = 10;
        Guid id = manager.GetComponentData<NetworkIdentityComponent>(player).id;
        frame.RecordPlayerStates(7, ClientPlayerPredictionSnapshot.Capture(manager, player, id));
        // This input also creates the event buffer during replay, invalidating old views.
        frame.RecordInput(8, new NetworkPrimaryPressData { unitId = id });
        frame.receivedOrder[7] = new Queue<NetworkState>(new[]
        {
            new NetworkState { data = new NetworkMoveStateData { unitId = id,
                positionX = 8f, velocityX = 1f, baseMoveSpeed = 1f, stateMoveMultiplier = 1f } },
        });
        var simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();
        world.SetTime(new TimeData(0.01, 0.01f));
        simulation.Update();
        manager.CompleteAllTrackedJobs();
        Assert.That(manager.GetComponentData<UnitMoveComponent>(player).PredictedPosition.x, Is.EqualTo(11f));
        Assert.That(frame.HasPendingPredictionReplay, Is.False);
        Assert.That(frame.receivedOrder.ContainsKey(7), Is.True);
        world.SetTime(new TimeData(0.034, 0.024f));
        simulation.Update();
        manager.CompleteAllTrackedJobs();
        Assert.That(frame.HasPendingPredictionReplay, Is.False);
        Assert.That(frame.receivedOrder, Is.Empty);
        Assert.That(manager.GetComponentData<ClientPlayerMovePresentationComponent>(player).ReconciliationPending, Is.Zero);
        Assert.That(manager.GetComponentData<UnitMoveComponent>(player).PredictedPosition.x,
            Is.EqualTo(8.099f).Within(0.00001), "Replay ticks 8 and 9, followed by normal tick 10.");
        Assert.That(frame.playerStates.Keys, Is.EqualTo(new uint[] { 8, 9, 10 }));
    }

    [Test]
    public void MatchingAndRepeatedAuthorityFramesNeverRewindTheCurrentPrediction()
    {
        using World world = CreateBattleWorld(true);
        var manager = world.EntityManager;
        Entity player = Player(manager, 1f);
        var frame = GetFrame(world);
        frame.currentFrame = 10;
        Guid id = manager.GetComponentData<NetworkIdentityComponent>(player).id;
        var snapshot = ClientPlayerPredictionSnapshot.Capture(manager, player, id);
        frame.RecordPlayerStates(7, snapshot);
        snapshot.TryGetMoveState(out NetworkMoveStateData authority);
        UnitMoveComponent move = manager.GetComponentData<UnitMoveComponent>(player);
        move.PredictedPosition.x = 10f;
        manager.SetComponentData(player, move);
        world.GetExistingSystemManaged<ClientPlayerPredictionSystem>().Update();
        var states = new NetworkStateData[] { authority };
        Assert.That(frame.TryHandlePlayerFrame(7, states, new NetworkStateApplyContext(manager, 7, 33)), Is.True);
        Assert.That(frame.TryHandlePlayerFrame(7, states, new NetworkStateApplyContext(manager, 7, 33)), Is.True);
        Assert.That(frame.TryHandlePlayerFrame(6, states, new NetworkStateApplyContext(manager, 6, 33)), Is.True);
        Assert.That(manager.GetComponentData<UnitMoveComponent>(player).PredictedPosition.x, Is.EqualTo(10f));
        Assert.That(frame.HasPendingPredictionReplay, Is.False);
        frame.ClearOrders();
        Assert.That(frame.HasReconciledPlayerFrame, Is.False, "A new scene may reuse frame zero.");
    }

    [Test]
    public void PreparationStillAppliesInitialSnapshotsWithoutRunningPlayerTicks()
    {
        using World world = CreateBattleWorld(true);
        ClientFrameManager frame = GetFrame(world);
        frame.running = false;
        var receive = world.GetExistingSystemManaged<FrameReceiveSystem>();
        using EntityQuery query = world.EntityManager.CreateEntityQuery(typeof(FrameReceiveBufferComponent));
        var buffer = world.EntityManager.GetComponentObject<FrameReceiveBufferComponent>(query.GetSingletonEntity());
        var state = new CountAppliedState();
        buffer.frames[0] = new Queue<NetworkState>(new[] { new NetworkState { data = state } });
        receive.Update();
        Assert.That(state.Count, Is.EqualTo(1));
        Assert.That(frame.currentFrame, Is.Zero);
    }

    [Test]
    public void MovementIsInterpolatedBetweenTicksAndLocalToWorldUsesTheSameFrame()
    {
        using World world = CreateBattleWorld(true);
        Entity player = Player(world.EntityManager, 1f);
        var presentation = world.EntityManager.GetComponentData<ClientPlayerMovePresentationComponent>(player);
        presentation.PreviousPredictionPosition = float3.zero;
        presentation.HasPredictionSamples = 1;
        world.EntityManager.SetComponentData(player, presentation);
        ClientFrameManager frame = GetFrame(world);
        frame.clock.AdvanceGameTime(8.25, 0, 1);
        var render = world.GetExistingSystemManaged<ClientNetworkPresentationSystemGroup>();
        var transforms = world.GetExistingSystemManaged<TransformSystemGroup>();
        world.SetTime(new TimeData(0.00825, 0.00825f));
        render.Update();
        transforms.Update();
        Assert.That(world.EntityManager.GetComponentData<LocalTransform>(player).Position.x, Is.EqualTo(0.25f).Within(0.00001));
        Assert.That(world.EntityManager.GetComponentData<LocalToWorld>(player).Position.x, Is.EqualTo(0.25f).Within(0.00001));
        frame.clock.AdvanceGameTime(8.25, 0, 1);
        render.Update();
        transforms.Update();
        Assert.That(world.EntityManager.GetComponentData<LocalToWorld>(player).Position.x, Is.EqualTo(0.5f).Within(0.00001));
        var order = world.GetExistingSystemManaged<SimulationSystemGroup>().ManagedSystems.ToList();
        Assert.That(order.IndexOf(world.GetExistingSystemManaged<BattleSimulationSystemGroup>()), Is.LessThan(order.IndexOf(render)));
        Assert.That(order.IndexOf(render), Is.LessThan(order.IndexOf(transforms)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PhysicsSmoothedPrefabsKeepTheirClientSpriteAtThePresentedPosition(bool interpolate)
    {
        using World world = CreateBattleWorld(true);
        var manager = world.EntityManager;
        // Repeat after the first update to cover entities spawned during play too.
        for (int wave = 1; wave <= 2; wave++)
        {
            Entity player = Player(manager, 10f * wave);
            manager.AddSharedComponent(player, new PhysicsWorldIndex());
            manager.AddComponentData(player, new PhysicsGraphicalSmoothing
            {
                ApplySmoothing = 1,
                CurrentVelocity = new PhysicsVelocity { Linear = new float3(5, 0, 0) },
            });
            if (interpolate)
                manager.AddComponentData(player, new PhysicsGraphicalInterpolationBuffer
                {
                    PreviousTransform = RigidTransform.identity,
                });
            GameObject visual = AttachVisual(manager, player);
            world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
            world.SetTime(new TimeData(wave * 0.01, 0.01f));
            world.GetExistingSystemManaged<SimulationSystemGroup>().Update();
            manager.CompleteAllTrackedJobs();

            float3 presented = manager.GetComponentData<ClientPlayerMovePresentationComponent>(player).CurrentPosition;
            LocalToWorld matrix = manager.GetComponentData<LocalToWorld>(player);
            Assert.That(math.distance(matrix.Position, presented), Is.LessThan(0.00001),
                "The sprite matrix must follow client presentation without requiring a physics tick.");
            Assert.That(visual.transform.position, Is.EqualTo((Vector3)presented));
            Assert.That(visual.transform.lossyScale, Is.EqualTo(Vector3.one), "A zero matrix makes the sprite invisible.");
            Assert.That(visual.activeInHierarchy, Is.True);
            Assert.That(manager.HasComponent<PhysicsGraphicalSmoothing>(player), Is.False);
            Assert.That(manager.HasComponent<PhysicsGraphicalInterpolationBuffer>(player), Is.False);
        }
    }

    [Test]
    public void ClientAndAuthorityStopAtTheSameBoxAndSlideAlongItsEdge()
    {
        using World client = CreateBattleWorld(true);
        using World server = CreateBattleWorld(false);
        using BlobAssetReference<Collider> sphere = Unity.Physics.SphereCollider.Create(
            new SphereGeometry { Radius = 0.5f }, CollisionFilter.Default);
        using BlobAssetReference<Collider> box = TestBox();
        Entity predicted = Player(client.EntityManager, 0);
        Entity authority = Player(server.EntityManager, 0);
        client.EntityManager.AddComponentData(predicted, new PhysicsCollider { Value = sphere });
        server.EntityManager.AddComponentData(authority, new PhysicsCollider { Value = sphere });
        Body(client.EntityManager, box, new float3(2, 0, 0), false);
        Body(server.EntityManager, box, new float3(2, 0, 0), false);
        for (int tick = 0; tick < 90; tick++)
        {
            float2 velocity = tick < 45 ? new float2(4, 0) : new float2(4, 4);
            SetVelocity(client.EntityManager, predicted, velocity);
            SetVelocity(server.EntityManager, authority, velocity);
            Step(client, (tick + 1) * 0.033, 0.033f);
            Step(server, (tick + 1) * 0.033, 0.033f);
            float3 position = client.EntityManager.GetComponentData<UnitMoveComponent>(predicted).PredictedPosition;
            float3 authoritativePosition = server.EntityManager.GetComponentData<LocalTransform>(authority).Position;
            Assert.That(position.x, Is.LessThan(1.08f), "Local prediction must stop at the box before any server correction.");
            Assert.That(math.distance(position, authoritativePosition), Is.LessThan(0.0001f),
                $"Tick {tick}: predicted {position}, authority {authoritativePosition}.");
        }
        Assert.That(client.EntityManager.GetComponentData<UnitMoveComponent>(predicted).PredictedPosition.y,
            Is.GreaterThan(0.5f), "Collision must still allow sliding along the box.");
    }

    [Test]
    public void PlayerEnemyBlockingMatchesAuthorityAndSurvivesPredictionReplay()
    {
        using World client = CreateBattleWorld(true);
        using World server = CreateBattleWorld(false);
        using BlobAssetReference<Collider> sphere = Unity.Physics.SphereCollider.Create(
            new SphereGeometry { Radius = 0.5f }, CollisionFilter.Default);
        Entity predicted = Player(client.EntityManager, 0);
        Entity authority = Player(server.EntityManager, 0);
        foreach (var pair in new[] { (client, predicted), (server, authority) })
        {
            EntityManager manager = pair.Item1.EntityManager;
            manager.AddComponentData(pair.Item2, new PhysicsCollider { Value = sphere });
            Entity enemy = Body(manager, sphere, new float3(2, 0, 0), true);
            manager.AddComponentData(enemy, new UnitFactionComponent { Value = UnitFactionType.Enemy });
            manager.AddComponentData(enemy, new UnitMoveComponent
            {
                StateMoveMultiplier = 1, CommandMoveSpeed = -1,
            });
        }
        using BlobAssetReference<StateScriptRuntimeRegistryBlob> registry = EnableEmptyStateScript(client, predicted);
        double elapsed = 0;
        for (int tick = 0; tick < 45; tick++)
        {
            SetVelocity(client.EntityManager, predicted, new float2(4, 0));
            SetVelocity(server.EntityManager, authority, new float2(4, 0));
            elapsed += 0.033;
            Step(client, elapsed, 0.033f);
            Step(server, elapsed, 0.033f);
            float3 local = client.EntityManager.GetComponentData<UnitMoveComponent>(predicted).PredictedPosition;
            float3 authoritative = server.EntityManager.GetComponentData<LocalTransform>(authority).Position;
            Assert.That(local.x, Is.LessThanOrEqualTo(1.001f));
            Assert.That(math.distance(local, authoritative), Is.LessThan(0.0001f), $"Tick {tick}");
        }
        ClientFrameManager frame = GetFrame(client);
        Guid id = client.EntityManager.GetComponentData<NetworkIdentityComponent>(predicted).id;
        frame.receivedOrder[10] = new Queue<NetworkState>(new[]
        {
            new NetworkState { data = new NetworkMoveStateData { unitId = id, positionX = 0.25f,
                velocityX = 4, baseMoveSpeed = 4, stateMoveMultiplier = 1 } },
        });
        Step(client, elapsed + 0.033, 0.033f);
        Assert.That(frame.HasPendingPredictionReplay, Is.False);
        foreach (ClientPlayerPredictionSnapshot snapshot in frame.playerStates.Values)
        {
            Assert.That(snapshot.TryGetMoveState(out NetworkMoveStateData move), Is.True);
            Assert.That(move.positionX, Is.LessThanOrEqualTo(1.001f),
                "Each replay step must refresh its own blocking starting poses.");
        }
    }

    [Test]
    public void CollisionReplayStopsAtTheBoxAndStandingAfterLeavingDoesNotFeedRenderingIntoPhysics()
    {
        using World world = CreateBattleWorld(true);
        using BlobAssetReference<Collider> sphere = Unity.Physics.SphereCollider.Create(
            new SphereGeometry { Radius = 0.5f }, CollisionFilter.Default);
        using BlobAssetReference<Collider> box = TestBox();
        Entity player = Player(world.EntityManager, 0);
        world.EntityManager.AddComponentData(player, new PhysicsCollider { Value = sphere });
        Body(world.EntityManager, box, new float3(2, 0, 0), false);
        using BlobAssetReference<StateScriptRuntimeRegistryBlob> registry = EnableEmptyStateScript(world, player);
        SetVelocity(world.EntityManager, player, new float2(4, 0));
        double elapsed = 0;
        for (int tick = 0; tick < 50; tick++)
            Step(world, elapsed += 0.033, 0.033f);
        ClientFrameManager frame = GetFrame(world);
        Guid id = world.EntityManager.GetComponentData<NetworkIdentityComponent>(player).id;
        frame.receivedOrder[10] = new Queue<NetworkState>(new[]
        {
            new NetworkState { data = new NetworkMoveStateData { unitId = id, positionX = 0.25f,
                velocityX = 4, baseMoveSpeed = 4, stateMoveMultiplier = 1 } },
        });
        Step(world, elapsed += 0.033, 0.033f);
        Assert.That(frame.HasPendingPredictionReplay, Is.False);
        foreach (ClientPlayerPredictionSnapshot snapshot in frame.playerStates.Values)
        {
            Assert.That(snapshot.TryGetMoveState(out NetworkMoveStateData move), Is.True);
            Assert.That(move.positionX, Is.LessThan(1.08f), "Replayed snapshots must contain collision results.");
        }
        SetVelocity(world.EntityManager, player, new float2(-4, 0));
        for (int tick = 0; tick < 30; tick++)
            Step(world, elapsed += 0.033, 0.033f);
        SetVelocity(world.EntityManager, player, float2.zero);
        for (int tick = 0; tick < 20; tick++)
            Step(world, elapsed += 0.033, 0.033f);
        float3 stopped = world.EntityManager.GetComponentData<UnitMoveComponent>(player).PredictedPosition;
        Assert.That(stopped.x, Is.LessThan(-2), "The player has left the collision area.");
        for (int renderFrame = 0; renderFrame < 240; renderFrame++)
        {
            Step(world, elapsed += 1d / 120, 1f / 120);
            Assert.That(math.distance(world.EntityManager.GetComponentData<UnitMoveComponent>(player).PredictedPosition, stopped),
                Is.LessThan(0.00001f));
            Assert.That(math.distance(world.EntityManager.GetComponentData<LocalToWorld>(player).Position, stopped),
                Is.LessThan(0.00001f));
        }
    }

    [Test]
    public void RemotePhysicsBodiesStayAtTheirAuthoritativePositionDuringLocalTicks()
    {
        using World world = CreateBattleWorld(true);
        using BlobAssetReference<Collider> sphere = Unity.Physics.SphereCollider.Create(
            new SphereGeometry { Radius = 0.5f }, CollisionFilter.Default);
        Entity remote = Body(world.EntityManager, sphere, new float3(8, 0, 0), true);
        world.EntityManager.SetComponentData(remote, new PhysicsVelocity { Linear = new float3(100, 0, 0) });
        float3 target = new(8, 2, 0);
        world.EntityManager.AddComponentData(remote, new ClientTransformInterpolationComponent
        {
            TargetPosition = target, Initialized = 1,
        });
        for (int tick = 0; tick < 10; tick++)
            Step(world, (tick + 1) * 0.033, 0.033f);
        Assert.That(world.EntityManager.GetComponentData<LocalTransform>(remote).Position, Is.EqualTo(target));
        Assert.That(world.EntityManager.GetComponentData<PhysicsVelocity>(remote).Linear, Is.EqualTo(float3.zero));
    }

    [Test]
    public void OnlyThePredictionErrorIsSmoothedAndLargeTeleportsSnap()
    {
        ClientPlayerMovePresentationComponent presentation = new()
        {
            CurrentPosition = new float3(10, 0, 0),
            PreviousPredictionPosition = new float3(9, 0, 0),
            LatestPredictionPosition = new float3(10, 0, 0),
            ReconciliationFromPosition = new float3(10, 0, 0),
            ReconciliationPending = 1,
        };
        presentation.CompleteReconciliation(new float3(9.9f, 0, 0));
        Assert.That(presentation.CorrectionOffset.x, Is.EqualTo(0.1f).Within(0.00001));
        Assert.That(presentation.PreviousPredictionPosition.x, Is.EqualTo(8.9f).Within(0.00001));
        presentation.ReconciliationPending = 1;
        presentation.ReconciliationFromPosition = new float3(9.9f, 0, 0);
        presentation.CompleteReconciliation(new float3(100, 0, 0));
        Assert.That(presentation.CorrectionOffset, Is.EqualTo(float3.zero));
        Assert.That(presentation.PreviousPredictionPosition.x, Is.EqualTo(100));
    }

    [Test]
    public void CameraReadsThePresentedPlayerPositionEvenIfLocalToWorldIsStale()
    {
        using World world = CreateBattleWorld(true);
        Entity player = Player(world.EntityManager, 3f);
        var previousWorld = World.DefaultGameObjectInjectionWorld;
        World.DefaultGameObjectInjectionWorld = world;
        GameObject root = new("Camera stability test");
        var camera = root.AddComponent<CameraComponent>();
        try
        {
            typeof(CameraComponent).GetField("_followQueryWorld", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, world);
            typeof(CameraComponent).GetField("_followTargetEntity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, player);
            object[] arguments = { Vector3.zero };
            bool found = (bool)typeof(CameraComponent).GetMethod("TryGetPlayerTargetPosition", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(camera, arguments);
            Assert.That(found, Is.True);
            Assert.That((Vector3)arguments[0], Is.EqualTo(new Vector3(3, 0, 0)));
        }
        finally
        {
            camera.Cleanup();
            UnityEngine.Object.DestroyImmediate(root);
            World.DefaultGameObjectInjectionWorld = previousWorld;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LateHardControlReplaysExactTimersAndRepeatedPacketsDoNotExtendImmunity(bool includeMove)
    {
        using World client = CreateBattleWorld(true);
        using World server = CreateBattleWorld(false);
        Entity predicted = Player(client.EntityManager, 0);
        Entity authority = Player(server.EntityManager, 0);
        UnitControlRuntimeComponent initial = new()
        {
            HardControlImmunityEnabled = 1,
            HardControlImmunityDurationMultiplier = 2,
            HardControlImmunityMinimumSeconds = 0.5f,
        };
        client.EntityManager.AddComponentData(predicted, initial);
        server.EntityManager.AddComponentData(authority, initial);
        SetVelocity(client.EntityManager, predicted, float2.zero);
        SetVelocity(server.EntityManager, authority, float2.zero);
        using BlobAssetReference<StateScriptRuntimeRegistryBlob> registry = EnableEmptyStateScript(client, predicted);
        UnitControlUtility.ApplyStun(server.EntityManager, authority, Entity.Null, 0.1f);
        Step(client, 0.034, 0.034f);
        Step(server, 0.034, 0.034f);
        Guid id = client.EntityManager.GetComponentData<NetworkIdentityComponent>(predicted).id;
        NetworkControlStateData control = NetworkControlStateData.Capture(server.EntityManager, id,
            server.EntityManager.GetComponentData<UnitControlRuntimeComponent>(authority), 0, 33);
        var states = new List<NetworkState> { new() { data = control } };
        if (includeMove)
            states.Add(new NetworkState { data = NetworkUnitStateSnapshotUtility.CreateMoveState(id,
                server.EntityManager.GetComponentData<UnitMoveComponent>(authority),
                server.EntityManager.GetComponentData<LocalTransform>(authority)) });
        for (int tick = 1; tick < 5; tick++)
        {
            Step(client, (tick + 1) * 0.033, 0.033f);
            Step(server, (tick + 1) * 0.033, 0.033f);
        }
        ClientFrameManager frame = GetFrame(client);
        frame.receivedOrder[0] = new Queue<NetworkState>(states);
        Step(client, 6 * 0.033, 0.033f);
        Assert.That(frame.HasPendingPredictionReplay, Is.False);
        UnitControlRuntimeComponent actual = client.EntityManager.GetComponentData<UnitControlRuntimeComponent>(predicted);
        Assert.That(actual.HasControl, Is.Zero);
        Assert.That(UnitControlUtility.GetImmunityRemaining(actual, UnitControlType.Stun),
            Is.EqualTo(0.6f - 6 * 0.033f).Within(0.00001), "Replay must advance protection once per historical tick.");
        foreach (var pair in frame.playerStates)
            Assert.That(UnitControlUtility.GetImmunityRemaining(pair.Value.Control, UnitControlType.Stun),
                Is.EqualTo(0.6f - (pair.Key + 1) * 0.033f).Within(0.00001));

        frame.receivedOrder[0] = new Queue<NetworkState>(states);
        Step(client, 7 * 0.033, 0.033f);
        actual = client.EntityManager.GetComponentData<UnitControlRuntimeComponent>(predicted);
        Assert.That(UnitControlUtility.GetImmunityRemaining(actual, UnitControlType.Stun),
            Is.EqualTo(0.6f - 7 * 0.033f).Within(0.00001));

        frame.receivedOrder[3] = new Queue<NetworkState>(new[]
        {
            new NetworkState { data = new NetworkMoveStateData { unitId = id, positionX = 0.25f,
                baseMoveSpeed = 4, stateMoveMultiplier = 1 } },
        });
        Step(client, 8 * 0.033, 0.033f);
        actual = client.EntityManager.GetComponentData<UnitControlRuntimeComponent>(predicted);
        Assert.That(UnitControlUtility.GetImmunityRemaining(actual, UnitControlType.Stun),
            Is.EqualTo(0.6f - 8 * 0.033f).Within(0.00001), "A movement-only rollback must restore the historical control timer.");
    }

    [Test]
    public void ControlArrivingAfterPositionConfirmationIsPreservedByLaterMovementRollback()
    {
        using World world = CreateBattleWorld(true);
        Entity player = Player(world.EntityManager, 0);
        SetVelocity(world.EntityManager, player, float2.zero);
        world.EntityManager.AddComponentData(player, new UnitControlRuntimeComponent
        {
            HardControlImmunityEnabled = 1,
            HardControlImmunityDurationMultiplier = 2,
            HardControlImmunityMinimumSeconds = 0.5f,
        });
        using BlobAssetReference<StateScriptRuntimeRegistryBlob> registry = EnableEmptyStateScript(world, player);
        Step(world, 0.034, 0.034f);
        ClientFrameManager frame = GetFrame(world);
        Guid id = world.EntityManager.GetComponentData<NetworkIdentityComponent>(player).id;
        frame.playerStates[0].TryGetMoveState(out NetworkMoveStateData move);
        var prediction = world.GetExistingSystemManaged<ClientPlayerPredictionSystem>();
        Assert.That(frame.TryHandlePlayerFrame(0, new NetworkStateData[] { move },
            new NetworkStateApplyContext(world.EntityManager, 0, 33)), Is.True);
        Assert.That(frame.playerStates.ContainsKey(0), Is.False);
        for (int tick = 1; tick < 5; tick++)
            Step(world, 0.034 + tick * 0.033, 0.033f);
        UnitControlRuntimeComponent control = world.EntityManager.GetComponentData<UnitControlRuntimeComponent>(player);
        control.Immunities.Add(new UnitControlImmunityEntry { ControlType = UnitControlType.Stun, RemainingTime = 1f });
        NetworkControlStateData state = NetworkControlStateData.Capture(world.EntityManager, id, control, 0, 33);
        Assert.That(frame.TryHandlePlayerFrame(0, new NetworkStateData[] { state },
            new NetworkStateApplyContext(world.EntityManager, 0, 33)), Is.True);
        frame.receivedOrder[2] = new Queue<NetworkState>(new[]
        {
            new NetworkState { data = new NetworkMoveStateData { unitId = id, positionX = 0.25f,
                baseMoveSpeed = 4, stateMoveMultiplier = 1 } },
        });
        Step(world, 0.034 + 5 * 0.033, 0.033f);
        control = world.EntityManager.GetComponentData<UnitControlRuntimeComponent>(player);
        Assert.That(UnitControlUtility.GetImmunityRemaining(control, UnitControlType.Stun),
            Is.EqualTo(1f - 5 * 0.033f).Within(0.00001));
        Assert.That(frame.HasPendingPredictionReplay, Is.False);
    }

    private static World CreateBattleWorld(bool client)
    {
        World world = new("Movement stability", client ? WorldFlags.GameClient : WorldFlags.GameServer);
        GameSingletonUtility.Create(world.EntityManager, client ? GameWorldRole.Client : GameWorldRole.Server, GameSceneMode.Dungeon);
        Entity tree = world.EntityManager.CreateEntity();
        world.EntityManager.AddBuffer<UnitQueryNode>(tree);
        world.EntityManager.AddBuffer<UnitQueryEntry>(tree);
        world.EntityManager.SetComponentData(world.EntityManager.CreateEntity(typeof(UnitQuerySingleton)),
            new UnitQuerySingleton { TreeEntity = tree });
        // Real package physics/transform systems; keep unrelated gameplay out of this fixture.
        var systems = DefaultWorldInitialization.GetAllSystems(client
            ? WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation
            : WorldSystemFilterFlags.ServerSimulation);
        List<Type> selected = new();
        foreach (Type system in systems)
        {
            string ns = system.Namespace ?? string.Empty;
            if (ns.StartsWith("Unity.Physics") || ns.StartsWith("Unity.Transforms") || ns == "Unity.Entities" ||
                system == typeof(ClientPhysicsGraphicalCleanupSystem) || system == typeof(ClientPhysicsProxySystem) ||
                system == typeof(UnitMoveSystem) || system == typeof(UnitControlSystem) ||
                system == typeof(UnitPhysicsRotationInitializationSystem) ||
                system == typeof(UnitBlockingSystem) || system == typeof(UnitBlockingContactSystem) ||
                system == typeof(UnitBlockingFinalizeSystem))
                selected.Add(system);
        }
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, selected);
        world.GetOrCreateSystemManaged<ClientInputSystemGroup>();
        world.GetOrCreateSystemManaged<UnitInitializationSystemGroup>();
        world.GetOrCreateSystemManaged<UnitDecisionSystemGroup>();
        world.GetOrCreateSystemManaged<UnitExecutionSystemGroup>();
        world.GetOrCreateSystemManaged<UnitPostProcessSystemGroup>();
        world.GetOrCreateSystemManaged<GamePresentationSystemGroup>();
        world.GetOrCreateSystem<PlayerPropCooldownSystem>();
        world.GetOrCreateSystem(typeof(UnitMoveComponent).Assembly.GetType("DestroyEntitySystem", true));
        var prediction = world.GetOrCreateSystemManaged<ClientPlayerPredictionSystemGroup>();
        world.GetOrCreateSystemManaged<FrameReceiveSystem>();
        world.GetOrCreateSystem<PlayerInputPulseResetSystem>();
        if (client)
        {
            world.GetOrCreateSystemManaged<StateScriptSystem>();
            prediction.AddSystemToUpdateList(world.GetOrCreateSystemManaged<ClientPlayerPredictionSystem>());
            prediction.AddSystemToUpdateList(world.GetOrCreateSystemManaged<ClientPlayerStateRecordSystem>());
        }
        FrameManager frame = client ? new ClientFrameManager() : new ServerFrameManager();
        frame.running = true;
        FrameManagerUtility.Bind(world.EntityManager, frame);
        if (client)
        {
            var render = world.GetOrCreateSystemManaged<ClientNetworkPresentationSystemGroup>();
            render.AddSystemToUpdateList(world.GetOrCreateSystem<ClientPlayerMovePresentationSystem>());
            var simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();
            simulation.AddSystemToUpdateList(render);
            simulation.SortSystems();
        }
        return world;
    }

    private static ClientFrameManager GetFrame(World world)
    {
        FrameManagerUtility.TryGet(world.EntityManager, out ClientFrameManager frame);
        return frame;
    }

    private static Entity Player(EntityManager manager, float x)
    {
        Entity entity = manager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(UnitMoveComponent),
            typeof(NetworkPlayerComponent), typeof(NetworkIdentityComponent), typeof(PlayerInputComponent),
            typeof(ClientPlayerMovePresentationComponent));
        Guid id = Guid.NewGuid();
        manager.SetComponentData(entity, new NetworkIdentityComponent { id = id });
        manager.SetComponentData(entity, new NetworkPlayerComponent { id = id });
        manager.SetComponentData(entity, LocalTransform.FromPosition(x, 0, 0));
        manager.AddComponentData(entity, default(PhysicsVelocity));
        manager.AddComponentData(entity, PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 1));
        manager.AddComponentData(entity, new PhysicsGravityFactor { Value = 0 });
        manager.AddComponent<UnitFactionComponent>(entity);
        manager.AddSharedComponent(entity, new PhysicsWorldIndex());
        manager.SetComponentData(entity, new UnitMoveComponent { BaseMoveSpeed = 1, StateMoveMultiplier = 1,
            Velocity = new float2(1, 0), HasPredictedPosition = 1, PredictedPosition = new float3(x, 0, 0), CommandMoveSpeed = -1 });
        manager.SetComponentData(entity, new ClientPlayerMovePresentationComponent { CurrentPosition = new float3(x, 0, 0),
            LatestPredictionPosition = new float3(x, 0, 0), PreviousPredictionPosition = new float3(x, 0, 0), Initialized = 1 });
        return entity;
    }

    private static Entity Body(EntityManager manager, BlobAssetReference<Collider> collider, float3 position, bool dynamic)
    {
        Entity entity = manager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider), typeof(PhysicsWorldIndex));
        manager.SetComponentData(entity, LocalTransform.FromPosition(position));
        manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
        if (dynamic)
        {
            manager.AddComponentData(entity, PhysicsMass.CreateDynamic(collider.Value.MassProperties, 1));
            manager.AddComponentData(entity, default(PhysicsVelocity));
            manager.AddComponentData(entity, new PhysicsGravityFactor { Value = 0 });
        }
        return entity;
    }

    private static BlobAssetReference<Collider> TestBox() => Unity.Physics.BoxCollider.Create(
        new BoxGeometry { Size = new float3(1, 12, 2), Orientation = quaternion.identity, BevelRadius = 0.01f },
        CollisionFilter.Default);

    private static void SetVelocity(EntityManager manager, Entity player, float2 velocity)
    {
        UnitMoveComponent move = manager.GetComponentData<UnitMoveComponent>(player);
        move.Velocity = velocity;
        move.BaseMoveSpeed = math.max(4, math.length(velocity));
        move.BaseMaxAcceleration = 0;
        manager.SetComponentData(player, move);
    }

    private static void Step(World world, double elapsed, float delta)
    {
        world.SetTime(new TimeData(elapsed, delta));
        world.GetExistingSystemManaged<SimulationSystemGroup>().Update();
        world.EntityManager.CompleteAllTrackedJobs();
    }

    private static BlobAssetReference<StateScriptRuntimeRegistryBlob> EnableEmptyStateScript(World world, Entity player)
    {
        using BlobBuilder builder = new(Allocator.Temp);
        builder.ConstructRoot<StateScriptRuntimeRegistryBlob>();
        BlobAssetReference<StateScriptRuntimeRegistryBlob> registry =
            builder.CreateBlobAssetReference<StateScriptRuntimeRegistryBlob>(Allocator.Persistent);
        world.EntityManager.SetComponentData(world.EntityManager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent)),
            new StateScriptRuntimeRegistryComponent { Value = registry });
        world.EntityManager.AddComponentData(player, new UnitStateScriptComponent { DefinitionIndex = -1 });
        world.EntityManager.AddBuffer<StateScriptGraphStateElement>(player);
        world.EntityManager.AddBuffer<StateScriptNodeStateElement>(player);
        return registry;
    }

    private static GameObject AttachVisual(EntityManager manager, Entity entity)
    {
        GameObject visual = new("Client player sprite", typeof(SpriteRenderer));
        visual.SetActive(false);
        manager.AddComponentObject(entity, visual.GetComponent<SpriteRenderer>());
        Assembly hybrid = typeof(Baker<>).Assembly;
        AddCompanionData(manager, entity, hybrid.GetType("Unity.Entities.CompanionLink", true),
            "Companion", (UnityObjectRef<GameObject>)visual);
        AddCompanionData(manager, entity, hybrid.GetType("Unity.Entities.CompanionLinkTransform", true),
            "CompanionTransform", (UnityObjectRef<Transform>)visual.transform);
        Type referenceType = hybrid.GetType("Unity.Entities.CompanionReference", true);
        object reference = Activator.CreateInstance(referenceType);
        referenceType.GetField("Companion").SetValue(reference, (UnityObjectRef<GameObject>)visual);
        manager.AddComponentObject(entity, reference);
        return visual;
    }

    private static void AddCompanionData(EntityManager manager, Entity entity, Type type, string field, object value)
    {
        object data = Activator.CreateInstance(type);
        type.GetField(field).SetValue(data, value);
        typeof(EntityManager).GetMethods().Single(method => method.Name == "AddComponentData" &&
            method.IsGenericMethod && method.GetParameters()[0].ParameterType == typeof(Entity))
            .MakeGenericMethod(type).Invoke(manager, new[] { (object)entity, data });
    }

    private sealed class CountAppliedState : NetworkStateData
    {
        public int Count;
        public override void Apply(NetworkStateApplyContext context) => Count++;
    }
}
