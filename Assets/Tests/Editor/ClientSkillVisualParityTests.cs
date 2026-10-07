using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using CrystalMagic.Game.Unit;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server;

public sealed class ClientSkillVisualParityTests
{
    private struct TestVisual : IComponentData { }
    private World _local, _client;
    private GameObject _dataRoot;
    private DataTable<SkillData> _skills;
    private Entity _localCaster, _clientCaster;
    private ClientSkillVisualExecutionSystem _prediction;
    private static readonly float3 Origin = new(2f, 3f, 0f);
    private static readonly float3 Aim = new(-4f, 9f, 0f);

    [SetUp]
    public void SetUp()
    {
        _local = CreateWorld(WorldFlags.Game, GameWorldRole.Standalone);
        _client = CreateWorld(WorldFlags.GameClient, GameWorldRole.Client);
        _localCaster = CreateCaster(_local.EntityManager);
        _clientCaster = CreateCaster(_client.EntityManager);
        _prediction = _client.GetOrCreateSystemManaged<ClientSkillVisualExecutionSystem>();
        _dataRoot = new GameObject("Skill visual test data");
        DataComponent data = _dataRoot.AddComponent<DataComponent>();
        _skills = new DataTable<SkillData>();
        var tables = (Dictionary<Type, object>)typeof(DataComponent)
            .GetField("_tables", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
        tables[typeof(SkillData)] = _skills;
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _local.Dispose();
        _dataRoot.GetComponent<DataComponent>().Cleanup();
        UnityEngine.Object.DestroyImmediate(_dataRoot);
    }

    [TestCase(1f, 0f)]
    [TestCase(0f, 1f)]
    [TestCase(-1f, 0f)]
    [TestCase(0f, -1f)]
    [TestCase(1f, 1f)]
    [TestCase(-1f, 1f)]
    [TestCase(-1f, -1f)]
    [TestCase(1f, -1f)]
    [TestCase(0f, 0f)]
    public void NetworkProjectileRotationMatchesLocalMovementAndFollowOffset(float x, float y)
    {
        EntityManager local = _local.EntityManager, client = _client.EntityManager;
        Entity localProjectile = local.CreateEntity(typeof(LocalTransform), typeof(SkillProjectileComponent), typeof(SkillProjectilePayloadComponent));
        local.SetComponentData(localProjectile, LocalTransform.FromPositionRotationScale(Origin, quaternion.identity, 2f));
        local.SetComponentData(localProjectile, new SkillProjectileComponent { Direction = new float3(x, y, 0f) });
        Entity tree = local.CreateEntity();
        local.AddBuffer<UnitQueryNode>(tree);
        local.AddBuffer<UnitQueryEntry>(tree);
        local.SetComponentData(local.CreateEntity(typeof(UnitQuerySingleton)), new UnitQuerySingleton { TreeEntity = tree });
        local.CreateEntity(typeof(GameInteractionComponent));
        local.CreateEntity(typeof(PlayerSkillDefinitionRegistryComponent));
        local.CreateEntity(typeof(WorldVariableComponent));
        _local.SetTime(new TimeData(0d, 0f));
        var execution = _local.GetOrCreateSystemManaged<UnitExecutionSystemGroup>();
        execution.AddSystemToUpdateList(_local.GetOrCreateSystem<SkillProjectileSystem>());
        execution.Update();
        local.CompleteAllTrackedJobs();

        Guid id = Guid.NewGuid();
        Entity clientProjectile = client.CreateEntity(typeof(LocalTransform), typeof(NetworkIdentityComponent));
        client.SetComponentData(clientProjectile, new NetworkIdentityComponent { id = id });
        client.SetComponentData(clientProjectile, LocalTransform.FromPositionRotationScale(Origin, quaternion.identity, 2f));
        var state = new NetworkProjectileStateData
        {
            unitId = id, directionX = x, directionY = y,
            positionX = Origin.x, positionY = Origin.y,
        };
        state.Apply(new NetworkStateApplyContext(client, 10, 50));
        AssertTransform(local.GetComponentData<LocalTransform>(localProjectile), client.GetComponentData<LocalTransform>(clientProjectile));
        Entity localVisual = AddFollow(local, localProjectile), clientVisual = AddFollow(client, clientProjectile);
        _local.GetOrCreateSystemManaged<EffectVisualFollowSystem>().Update();
        _client.GetOrCreateSystemManaged<EffectVisualFollowSystem>().Update();
        AssertTransform(local.GetComponentData<LocalTransform>(localVisual), client.GetComponentData<LocalTransform>(clientVisual));

        state.directionX = -y;
        state.directionY = x;
        state.Apply(new NetworkStateApplyContext(client, 11, 50));
        AssertRotation(client.GetComponentData<LocalTransform>(clientProjectile).Rotation,
            UnitFacingUtility.CreateRotation(new float2(-y, x)));
    }

    [TestCase("spawn")]
    [TestCase("follow")]
    [TestCase("line")]
    [TestCase("line-preserved")]
    [TestCase("follow-static")]
    [TestCase("move")]
    [TestCase("projectile")]
    public void PredictedSkillsMatchLocalVisualTransformsAndCanBeReplayedWithoutDuplicates(string kind)
    {
        EffectData data = MakeEffect(kind);
        _skills.Add(new SkillData { Id = 7, EffectChain = new[] { data } });
        ExecuteLocal(data);
        Predict();
        AssertVisualsMatch();
        using EntityQuery journalQuery = _client.EntityManager.CreateEntityQuery(typeof(ClientPredictedSkillVisualEventElement));
        Entity runtime = journalQuery.GetSingletonEntity();
        Assert.That(_client.EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).Length, Is.EqualTo(1));
        int count = GetVisuals(_client.EntityManager).Length;
        Predict();
        Assert.That(GetVisuals(_client.EntityManager).Length, Is.EqualTo(count));
        AssertVisualsMatch();
    }

    [Test]
    public void ConsecutiveDifferentEffectsKeepTheJournalValidAcrossEveryInstantiation()
    {
        EffectData[] effects = { MakeEffect("spawn"), MakeEffect("follow"), MakeEffect("line"), MakeEffect("move"), MakeEffect("projectile") };
        _skills.Add(new SkillData { Id = 7, EffectChain = effects });
        foreach (EffectData effect in effects) ExecuteLocal(effect);
        Predict();
        AssertVisualsMatch();
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(_client.EntityManager);
        Assert.That(_client.EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).Length, Is.EqualTo(5));
    }

    [TestCase("spawn")]
    [TestCase("follow")]
    [TestCase("follow-static")]
    [TestCase("line")]
    [TestCase("line-preserved")]
    [TestCase("move")]
    [TestCase("projectile")]
    public void ServerEventsProduceTheSameVisualsAsLocalSkills(string kind)
    {
        using World server = CreateWorld(WorldFlags.GameServer, GameWorldRole.Server);
        EntityManager manager = server.EntityManager;
        Entity caster = CreateCaster(manager);
        Guid sourceId = Guid.NewGuid();
        manager.AddComponentData(caster, new NetworkIdentityComponent { id = sourceId });
        _client.EntityManager.AddComponentData(_clientCaster, new NetworkIdentityComponent { id = sourceId });
        EffectData data = MakeEffect(kind);
        ExecuteLocal(data);
        ExecuteEffect(data, manager, caster);
        var context = new NetworkStateApplyContext(_client.EntityManager, 10, 50);
        using (EntityQuery query = manager.CreateEntityQuery(typeof(SkillProjectileComponent), typeof(LocalTransform), typeof(NetworkIdentityComponent)))
        using (NativeArray<Entity> projectiles = query.ToEntityArray(Allocator.Temp))
        {
            foreach (Entity projectile in projectiles)
            {
                Guid id = manager.GetComponentData<NetworkIdentityComponent>(projectile).id;
                Entity target = _client.EntityManager.CreateEntity(typeof(NetworkIdentityComponent));
                _client.EntityManager.SetComponentData(target, new NetworkIdentityComponent { id = id });
                context.RegisterEntity(id, target);
                var direction = manager.GetComponentData<SkillProjectileComponent>(projectile).Direction;
                var position = manager.GetComponentData<LocalTransform>(projectile).Position;
                new NetworkProjectileStateData
                {
                    unitId = id, directionX = direction.x, directionY = direction.y,
                    positionX = position.x, positionY = position.y, positionZ = position.z,
                }.Apply(context);
            }
        }
        using EntityQuery events = manager.CreateEntityQuery(typeof(NetworkPresentationEventQueueComponent));
        var queue = manager.GetComponentObject<NetworkPresentationEventQueueComponent>(events.GetSingletonEntity());
        Assert.That(queue.Events.Count, Is.EqualTo(1));
        foreach (var state in queue.Events) state.Apply(context);
        _client.GetOrCreateSystemManaged<ClientPresentationEventSystem>().Update();
        _local.GetOrCreateSystemManaged<EffectVisualFollowSystem>().Update();
        _client.GetOrCreateSystemManaged<EffectVisualFollowSystem>().Update();
        AssertVisualsMatch();
    }

    [TestCase(0f, false)]
    [TestCase(0.5f, false)]
    [TestCase(-1f, false)]
    [TestCase(0f, true)]
    [TestCase(0.5f, true)]
    [TestCase(-1f, true)]
    public void BothClientMovePathsFinishAtTheSamePositionAndDestroyAsLocal(float duration, bool fromNetwork)
    {
        var data = (MoveVfxEffectData)MakeEffect("move");
        data.Duration = duration;
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        ExecuteLocal(data);
        if (fromNetwork)
        {
            Entity events = _client.EntityManager.CreateEntity(typeof(ClientPresentationClockComponent));
            _client.EntityManager.AddBuffer<ClientPresentationEventElement>(events).Add(new ClientPresentationEventElement
            {
                Type = ClientPresentationEventType.MoveVfx, Sequence = 1, AssetName = "effect",
                Position = Aim + (float3)data.StartOffset, SecondaryPosition = Aim + (float3)data.StartOffset + (float3)data.MoveOffset,
                Scale = data.Scale, Rotation = quaternion.identity, Duration = duration, FlagB = 1,
            });
            _client.GetOrCreateSystemManaged<ClientPresentationEventSystem>().Update();
        }
        else Predict();

        Entity localVisual = GetVisuals(_local.EntityManager)[0], clientVisual = GetVisuals(_client.EntityManager)[0];
        Assert.That(_client.EntityManager.HasComponent<VfxLifetimeComponent>(clientVisual), Is.False);
        var interpolation = _client.EntityManager.GetComponentData<ClientTransformInterpolationComponent>(clientVisual);
        interpolation.StartRealtime -= 2d;
        _client.EntityManager.SetComponentData(clientVisual, interpolation);
        _client.GetOrCreateSystemManaged<ClientTransformInterpolationSystem>().Update();
        _local.SetTime(new TimeData(2d, 2f));
        SystemHandle arrival = _local.GetOrCreateSystem<VfxArrivalSystem>();
        var execution = _local.GetOrCreateSystemManaged<UnitExecutionSystemGroup>();
        execution.AddSystemToUpdateList(arrival);
        execution.Update();
        _local.EntityManager.CompleteAllTrackedJobs();
        AssertTransform(_local.EntityManager.GetComponentData<LocalTransform>(localVisual),
            _client.EntityManager.GetComponentData<LocalTransform>(clientVisual));
        Assert.That(_local.EntityManager.IsComponentEnabled<DestroyEntityFlag>(localVisual), Is.True);
        Assert.That(_client.EntityManager.IsComponentEnabled<DestroyEntityFlag>(clientVisual), Is.True);
        Assert.That(_client.EntityManager.GetBuffer<ClientPredictedSkillVisualEventElement>(
            ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(_client.EntityManager)).Length, Is.EqualTo(fromNetwork ? 0 : 1));
    }

    [Test]
    public void OrdinaryNetworkInterpolationDoesNotDestroyAnEntityAtItsDestination()
    {
        Entity entity = _client.EntityManager.CreateEntity(typeof(LocalTransform), typeof(ClientTransformInterpolationComponent));
        _client.EntityManager.SetComponentData(entity, new ClientTransformInterpolationComponent
        {
            FromPosition = Origin, TargetPosition = Aim, Initialized = 1, Duration = 0f,
        });
        _client.GetOrCreateSystemManaged<ClientTransformInterpolationSystem>().Update();
        Assert.That(_client.EntityManager.GetComponentData<LocalTransform>(entity).Position, Is.EqualTo(Aim));
        Assert.That(_client.EntityManager.HasComponent<DestroyEntityFlag>(entity), Is.False);
    }

    [Test]
    public void RollbackDestroysUnconfirmedPredictionsAndPreservesConfirmedVisuals()
    {
        _skills.Add(new SkillData { Id = 7, EffectChain = new[] { MakeEffect("spawn"), MakeEffect("follow") } });
        Predict();
        EntityManager manager = _client.EntityManager;
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        var journal = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
        var confirmed = journal[0];
        confirmed.IsConfirmed = 1;
        journal[0] = confirmed;
        Entity rejectedVisual = journal[1].VisualEntity;
        manager.SetComponentData(runtime, new ClientSkillVisualRuntimeComponent { HasPendingRollback = 1, RollbackFrame = 9 });
        Assert.DoesNotThrow(() => _prediction.Update());
        Assert.That(manager.Exists(confirmed.VisualEntity), Is.True);
        Assert.That(manager.Exists(rejectedVisual), Is.False);
        Assert.That(manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).Length, Is.EqualTo(1));
        Predict();
        Assert.That(manager.Exists(confirmed.VisualEntity), Is.True);
        Assert.That(GetVisuals(manager).Length, Is.EqualTo(2));
    }

    [TestCase(6, 0f)]
    [TestCase(1, 4f)]
    [TestCase(1, 0f)]
    public void OnlyDeterministicSinglePersistentPlacementsArePredicted(int count, float radius)
    {
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { new PersistentEffectData
        {
            PlacementCount = count, PlacementRadius = radius, OnStartEffects = new[] { MakeEffect("spawn") },
        } } });
        Predict();
        Assert.That(GetVisuals(_client.EntityManager).Length, Is.EqualTo(count == 1 && radius == 0f ? 1 : 0));
    }

    [Test]
    public void ExpiredPredictionDoesNotSuppressTheAuthoritativeEffect()
    {
        _skills.Add(new SkillData { Id = 7, EffectChain = new[] { MakeEffect("spawn") } });
        Predict();
        EntityManager manager = _client.EntityManager;
        manager.DestroyEntity(GetVisuals(manager)[0]);
        Assert.That(ClientSkillVisualPredictionUtility.TryReconcileNetworkEvent(manager, new ClientPresentationEventElement
        {
            Frame = 10, Source = _clientCaster, SourceSkillId = 7,
            Type = ClientPresentationEventType.SpawnVfx, AssetName = "effect",
        }), Is.False);
    }

    [TestCase(0.05f)]
    [TestCase(0.15f)]
    [TestCase(0.3f)]
    public void ProjectileConfirmationKeepsThePositionAlreadyPresentedToThePlayer(float delay)
    {
        var data = (SpawnProjectileEffectData)MakeEffect("projectile");
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        Predict();
        EntityManager manager = _client.EntityManager;
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        ClientPredictedSkillVisualEventElement predicted =
            manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime)[0];
        LocalTransform start = manager.GetComponentData<LocalTransform>(predicted.AnchorEntity);
        float3 direction = manager.GetComponentData<SkillProjectileComponent>(predicted.AnchorEntity).Direction;
        TickProjectile(_client, delay);
        var followSystem = _client.GetOrCreateSystemManaged<EffectVisualFollowSystem>();
        followSystem.Update();
        float3 presented = manager.GetComponentData<LocalTransform>(predicted.VisualEntity).Position;

        Guid id = Guid.NewGuid();
        Entity authority = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(LocalTransform));
        manager.SetComponentData(authority, new NetworkIdentityComponent { id = id });
        new NetworkProjectileStateData
        {
            unitId = id, directionX = direction.x, directionY = direction.y, speed = data.Speed,
            maxRange = data.MaxRange, positionX = start.Position.x, positionY = start.Position.y,
        }.Apply(new NetworkStateApplyContext(manager, 10, 33));
        Assert.That(ClientSkillVisualPredictionUtility.TryReconcileNetworkEvent(manager,
            new ClientPresentationEventElement
            {
                Identity = predicted.Identity, Frame = 10, Source = _clientCaster, SourceSkillId = 7,
                Type = ClientPresentationEventType.SpawnFollowVfx, AssetName = "effect", Target = authority,
                SecondaryPosition = data.VisualOffset, FlagA = 1,
            }), Is.True);
        followSystem.Update();
        Assert.That(math.distance(manager.GetComponentData<LocalTransform>(predicted.VisualEntity).Position, presented),
            Is.LessThan(0.0001f), "A delayed confirmation must not pull an already flying projectile back to its spawn point.");
        Assert.That(manager.GetComponentData<EffectVisualFollowComponent>(predicted.VisualEntity).Target, Is.EqualTo(predicted.AnchorEntity));
        Assert.That(manager.Exists(predicted.AnchorEntity), Is.True);
        Assert.That(manager.GetComponentData<ClientPredictedProjectileLinkComponent>(authority).Prediction, Is.EqualTo(predicted.AnchorEntity));
        Assert.That(GetVisuals(manager).Length, Is.EqualTo(1));

        const float step = 0.02f;
        float3 previous = manager.GetComponentData<LocalTransform>(predicted.AnchorEntity).Position;
        for (int tick = 1; tick <= 6; tick++)
        {
            _client.SetTime(new TimeData(delay + step * tick, step));
            _client.GetExistingSystem<ClientPredictedProjectileSystem>().Update(_client.Unmanaged);
            manager.CompleteAllTrackedJobs();
        }
        float3 after = manager.GetComponentData<LocalTransform>(predicted.AnchorEntity).Position;
        Assert.That(math.dot(after - previous, direction), Is.EqualTo(data.Speed * step * 6).Within(0.01f));
        Assert.That(manager.HasComponent<SkillProjectilePayloadComponent>(predicted.AnchorEntity), Is.True,
            "Confirmation must preserve the collision payload and continue local hit prediction.");
    }

    [Test]
    public void NetworkProjectileFliesBetweenPacketsAndEndsAtTheAuthoritativeImpactPosition()
    {
        EntityManager manager = _client.EntityManager;
        Guid id = Guid.NewGuid();
        Entity projectile = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(LocalTransform));
        manager.SetComponentData(projectile, new NetworkIdentityComponent { id = id });
        new NetworkProjectileStateData
        {
            unitId = id, directionX = 1, speed = 10, maxRange = 20,
        }.Apply(new NetworkStateApplyContext(manager, 10, 33));
        ClientProjectilePresentationComponent presentation =
            manager.GetComponentData<ClientProjectilePresentationComponent>(projectile);
        // Supply elapsed wall time without sleeping or depending on the test runner's speed.
        presentation.SnapshotRealtime -= 0.1;
        manager.SetComponentData(projectile, presentation);
        _client.SetTime(new TimeData(0.1, 0.1f));
        _client.GetOrCreateSystemManaged<ClientTransformInterpolationSystem>().Update();
        LocalTransform flying = manager.GetComponentData<LocalTransform>(projectile);
        Assert.That(flying.Position.x, Is.GreaterThanOrEqualTo(1f));
        Assert.That(manager.HasComponent<ClientTransformInterpolationComponent>(projectile), Is.False);
        Assert.That(manager.GetComponentData<SkillProjectileComponent>(projectile).TraveledDistance, Is.Zero);
        Entity visual = AddFollow(manager, projectile);
        var follow = manager.GetComponentData<EffectVisualFollowComponent>(visual);
        follow.EndWhenTargetMissing = 1;
        manager.SetComponentData(visual, follow);
        new NetworkEntityDespawnStateData
        {
            unitId = id, hasPosition = true, positionX = 0.75f, positionY = 0.25f,
        }.Apply(new NetworkStateApplyContext(manager, 11, 33));
        _client.GetOrCreateSystemManaged<ClientTransformInterpolationSystem>().Update();
        _client.GetOrCreateSystemManaged<ClientEntityLifetimePresentationSystem>().Update();
        _client.GetOrCreateSystemManaged<EffectVisualFollowSystem>().Update();
        Assert.That(manager.GetComponentData<LocalTransform>(projectile).Position,
            Is.EqualTo(new float3(0.75f, 0.25f, 0)), "The impact must stop extrapolation at the authoritative final position.");
        Assert.That(manager.HasComponent<ClientProjectilePresentationComponent>(projectile), Is.False);
        Assert.That(manager.IsComponentEnabled<DestroyEntityFlag>(visual), Is.True);
        Assert.That(math.distance(manager.GetComponentData<LocalTransform>(visual).Position,
            new float3(0.75f, 0.25f, 0) + follow.Offset), Is.LessThan(0.0001f));
    }

    [Test]
    public void NetworkProjectilePresentationCannotFlyPastItsRemainingRange()
    {
        ClientProjectilePresentationComponent presentation = new()
        {
            SnapshotPosition = Origin, SnapshotRealtime = 1, Initialized = 1,
        };
        SkillProjectileComponent projectile = new()
        {
            Direction = new float3(1, 0, 0), Speed = 10, MaxRange = 2, TraveledDistance = 1.8f,
        };
        LocalTransform flight = LocalTransform.FromPosition(Origin);
        for (int tick = 1; tick <= 10; tick++)
            presentation.Advance(in projectile, ref flight, 1 + tick * 0.05, 0.05f);
        Assert.That(flight.Position.x, Is.EqualTo(Origin.x + 0.2f).Within(0.0001f));
        Assert.That(projectile.TraveledDistance, Is.EqualTo(1.8f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FollowEffectsEndOnTheProjectileFinalPositionBeforeItIsDestroyed(bool animated)
    {
        foreach (World world in new[] { _local, _client })
        {
            EntityManager manager = world.EntityManager;
            Entity target = manager.CreateEntity(typeof(LocalTransform), typeof(DestroyEntityFlag));
            manager.SetComponentData(target, LocalTransform.FromPositionRotationScale(Aim, quaternion.RotateZ(1f), 1f));
            Entity visual = AddFollow(manager, target);
            var follow = manager.GetComponentData<EffectVisualFollowComponent>(visual);
            follow.EndWhenTargetMissing = 1;
            manager.SetComponentData(visual, follow);
            if (animated) manager.AddComponentObject(visual, new SpriteEffectAnimationComponent());
            world.GetOrCreateSystemManaged<EffectVisualFollowSystem>().Update();
            Assert.That(manager.Exists(target), Is.True);
            Assert.That(math.distance(manager.GetComponentData<LocalTransform>(visual).Position,
                Aim + math.rotate(quaternion.RotateZ(1f), new float3(1f, 2f, 0f))), Is.LessThan(0.0001f));
            if (animated)
                Assert.That(manager.GetComponentObject<SpriteEffectAnimationComponent>(visual).EndRequested, Is.EqualTo(1));
            else
                Assert.That(manager.IsComponentEnabled<DestroyEntityFlag>(visual), Is.True);
        }
    }

    [TestCase(0.033f)]
    [TestCase(0.15f)]
    public void PredictedProjectileUsesSharedCollisionAndExplodesWithoutAnyServerPacket(float delta)
    {
        EntityManager manager = _client.EntityManager;
        SpawnProjectileEffectData data = ImpactProjectile();
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        Entity target = CreateTarget(manager, Origin + math.normalize(Aim - Origin) * 2f);
        Predict();
        Entity anchor = PredictionAnchor();
        TickProjectile(_client, delta);
        if (delta < 0.1f) TickProjectile(_client, 0.07f);
        Assert.That(manager.GetComponentData<ClientPredictedProjectileComponent>(anchor).HasPredictedEnd, Is.EqualTo(1));
        Assert.That(manager.GetComponentData<SkillProjectileComponent>(anchor).HitSequence, Is.EqualTo(1));
        Assert.That(manager.GetComponentData<UnitVitalityComponent>(target).CurrentHealth, Is.EqualTo(100),
            "Prediction can play effects but cannot apply damage.");
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        var journal = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
        Assert.That(journal.Length, Is.EqualTo(2), "Flight and impact must both be generated locally.");
        var explosion = journal[1];
        Assert.That(explosion.Identity.Phase, Is.EqualTo(2));
        Assert.That(explosion.Identity.ImpactSequence, Is.EqualTo(1));
        Assert.That(manager.Exists(explosion.VisualEntity), Is.True);
        int count = GetVisuals(manager).Length;
        Assert.That(ClientSkillVisualPredictionUtility.TryReconcileNetworkEvent(manager, new ClientPresentationEventElement
        {
            Identity = explosion.Identity, Frame = 1000, Type = explosion.Type,
            Position = manager.GetComponentData<LocalTransform>(target).Position,
        }), Is.True, "Impact confirmation must match even hundreds of frames after the cast.");
        Assert.That(GetVisuals(manager).Length, Is.EqualTo(count));
        TickProjectile(_client, 0.2f);
        Assert.That(manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).Length, Is.EqualTo(2));
    }

    [Test]
    public void PierceAndRepeatHitsHaveSeparateStableEffectIdentities()
    {
        EntityManager manager = _client.EntityManager;
        var data = ImpactProjectile(); data.CanPierce = true; data.Speed = 0;
        data.OnCollisionEffects = new EffectData[] { MakeEffect("spawn") };
        data.OnDestroyEffects = null; data.RepeatHitIntervalSeconds = 0.2f;
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        CreateTarget(manager, Origin + math.normalize(Aim - Origin));
        Predict();
        TickProjectile(_client, 0.01f); TickProjectile(_client, 0.1f); TickProjectile(_client, 0.1f);
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        var journal = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
        Assert.That(journal.Length, Is.EqualTo(3));
        Assert.That(journal[1].Identity.ImpactSequence, Is.EqualTo(1));
        Assert.That(journal[2].Identity.ImpactSequence, Is.EqualTo(2));
        Assert.That(journal[1].Identity.Equals(journal[2].Identity), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MaxRangeUsesTheConfiguredDestroyEffectRule(bool trigger)
    {
        var data = ImpactProjectile(); data.MaxRange = 0.1f; data.TriggerDestroyEffectsOnMaxRange = trigger;
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        Predict(); TickProjectile(_client, 0.1f);
        var manager = _client.EntityManager;
        var anchor = PredictionAnchor();
        Assert.That(manager.GetComponentData<ClientPredictedProjectileComponent>(anchor).HasPredictedEnd, Is.EqualTo(1));
        Assert.That(manager.GetComponentData<SkillProjectileComponent>(anchor).TraveledDistance, Is.EqualTo(0.1f).Within(0.0001));
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        Assert.That(manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).Length, Is.EqualTo(trigger ? 2 : 1));
    }

    [Test]
    public void ServerMissCorrectionCancelsThePredictedExplosionAndResumesFlight()
    {
        EntityManager manager = _client.EntityManager;
        var data = ImpactProjectile();
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        CreateTarget(manager, Origin + math.normalize(Aim - Origin) * 2f);
        Predict(); Entity anchor = PredictionAnchor(); TickProjectile(_client, 0.1f);
        var prediction = manager.GetComponentData<ClientPredictedProjectileComponent>(anchor);
        Assert.That(prediction.HasPredictedEnd, Is.EqualTo(1));
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        Entity explosion = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime)[1].VisualEntity;
        Entity authority = manager.CreateEntity(typeof(LocalTransform));
        SkillProjectileComponent state = manager.GetComponentData<SkillProjectileComponent>(anchor);
        state.Ended = 0; state.HitSequence = 0;
        ClientProjectilePredictionUtility.ApplySnapshot(new NetworkStateApplyContext(manager, 20, 33),
            authority, prediction.Identity, state, Origin + math.normalize(Aim - Origin), 0);
        Assert.That(manager.Exists(explosion), Is.False);
        Assert.That(manager.GetComponentData<ClientPredictedProjectileComponent>(anchor).HasPredictedEnd, Is.Zero);
        Assert.That(manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).Length, Is.EqualTo(1));
    }

    [Test]
    public void EffectTargetsFilterBranchesAndKeepDamageAuthorityOnly()
    {
        EntityManager manager = _client.EntityManager;
        Entity target = CreateTarget(manager, Aim);
        var clientVfx = MakeEffect("spawn"); clientVfx.ExecutionTargets = GameWorldExecutionTarget.Client;
        var serverVfx = MakeEffect("spawn"); serverVfx.ExecutionTargets = GameWorldExecutionTarget.Server;
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[]
        {
            clientVfx, serverVfx, new DamageEffectData { FlatDamageBonus = 1000, ExecutionTargets = GameWorldExecutionTarget.All },
        } });
        Predict();
        Assert.That(GetVisuals(manager).Length, Is.EqualTo(1));
        Assert.That(manager.GetComponentData<UnitVitalityComponent>(target).CurrentHealth, Is.EqualTo(100));
        SkillContent context = SkillContentReferencePool.Get();
        try
        {
            context.EntityManager = manager;
            context.HasTargetEntity = true; context.TargetEntity = target;
            SkillExecutor.ExecuteEffects(new EffectData[]
            {
                new DamageEffectData { FlatDamageBonus = 1000, ExecutionTargets = GameWorldExecutionTarget.All },
            }, context);
            Assert.That(manager.GetComponentData<UnitVitalityComponent>(target).CurrentHealth, Is.EqualTo(100));
        }
        finally { SkillContentReferencePool.Return(context); }
    }

    [Test]
    public void RealFireballConfigurationIgnoresAlliesAndPredictsItsExplosionWithoutApplyingDamage()
    {
        JObject row = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Res/Data/SkillDataTable.json")))["Rows"][0];
        var data = row["EffectChain"][0].ToObject<SpawnProjectileEffectData>(JsonSerializer.Create(
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
        data.VisualPrefabName = "effect";
        ((SpawnVfxEffectData)data.OnDestroyEffects[0]).VfxPrefabName = "effect";
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        EntityManager manager = _client.EntityManager;
        manager.AddComponentData(_clientCaster, new UnitFactionComponent { Value = UnitFactionType.Player });
        float3 direction = math.normalize(Aim - Origin);
        Entity friend = CreateTarget(manager, Origin + direction * 1.1f);
        manager.SetComponentData(friend, new UnitFactionComponent { Value = UnitFactionType.Friend });
        Entity enemy = CreateTarget(manager, Origin + direction * 2.5f);
        Predict(); TickProjectile(_client, 0.01f);
        Assert.That(manager.GetComponentData<ClientPredictedProjectileComponent>(PredictionAnchor()).HasPredictedEnd, Is.Zero);
        TickProjectile(_client, 0.15f);
        Assert.That(manager.GetComponentData<ClientPredictedProjectileComponent>(PredictionAnchor()).HasPredictedEnd, Is.EqualTo(1));
        Assert.That(manager.GetComponentData<UnitVitalityComponent>(enemy).CurrentHealth, Is.EqualTo(100));
        Assert.That(manager.GetComponentData<UnitVitalityComponent>(friend).CurrentHealth, Is.EqualTo(100));
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        var journal = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
        Assert.That(journal.Length, Is.EqualTo(3), "Flight, explosion and impact sound are predicted; damage and ignite are authority-only.");
        Assert.That(journal[1].Identity.TargetId, Is.EqualTo(manager.GetComponentData<NetworkIdentityComponent>(enemy).id));
    }

    [Test]
    public void ServerAndClientProduceTheSameImpactIdentityAndServerOnlyAppliesDamage()
    {
        using World server = CreateWorld(WorldFlags.GameServer, GameWorldRole.Server);
        EntityManager manager = server.EntityManager, client = _client.EntityManager;
        Entity caster = CreateCaster(manager);
        Guid casterId = Guid.NewGuid(), targetId = Guid.NewGuid();
        manager.AddComponentData(caster, new NetworkIdentityComponent { id = casterId });
        client.AddComponentData(_clientCaster, new NetworkIdentityComponent { id = casterId });
        float3 targetPosition = Origin + math.normalize(Aim - Origin) * 2f;
        Entity serverTarget = CreateTarget(manager, targetPosition), clientTarget = CreateTarget(client, targetPosition);
        manager.SetComponentData(serverTarget, new NetworkIdentityComponent { id = targetId });
        client.SetComponentData(clientTarget, new NetworkIdentityComponent { id = targetId });
        SpawnProjectileEffectData data = ImpactProjectile();
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        Predict(); TickProjectile(_client, 0.1f);
        SkillContent context = SkillContentReferencePool.Get();
        try
        {
            context.EntityManager = manager; context.HasOriginEntity = true; context.OriginEntity = caster;
            context.HasOriginPositionSnapshot = true; context.OriginPositionSnapshot = Origin;
            context.HasPosition = true; context.Position = Aim; context.SourceSkillId = 7;
            context.EffectIdentity = SkillEffectIdentity.Create(manager, caster, 10, 0);
            SkillExecutor.ExecuteEffects(new EffectData[] { data }, context);
        }
        finally { SkillContentReferencePool.Return(context); }
        EnsureSources(manager);
        server.SetTime(new TimeData(0.1, 0.1f));
        server.GetOrCreateSystem<UnitQueryBuildSystem>().Update(server.Unmanaged);
        server.GetOrCreateSystem<SkillProjectileSystem>().Update(server.Unmanaged);
        manager.CompleteAllTrackedJobs();
        server.GetOrCreateSystemManaged<SkillProjectileCleanupSystem>().Update();
        server.GetOrCreateSystemManaged<EffectExecutionSystem>().Update();
        Assert.That(manager.GetComponentData<UnitVitalityComponent>(serverTarget).CurrentHealth, Is.EqualTo(50));
        Assert.That(client.GetComponentData<UnitVitalityComponent>(clientTarget).CurrentHealth, Is.EqualTo(100));
        Assert.That(GetVisuals(manager), Is.Empty, "The host's authority world must publish presentation events without rendering a second copy.");
        using EntityQuery queue = manager.CreateEntityQuery(typeof(NetworkPresentationEventQueueComponent));
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(client);
        var predicted = client.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime)[1];
        var events = manager.GetComponentObject<NetworkPresentationEventQueueComponent>(queue.GetSingletonEntity()).Events;
        var explosion = events.Find(item => item.eventType == ClientPresentationEventType.SpawnVfx);
        Assert.That(explosion, Is.Not.Null);
        Assert.That(explosion.identity.Equals(predicted.Identity), Is.True);
        var impact = events.Find(item => item.eventType == ClientPresentationEventType.ProjectileImpact);
        Assert.That(impact, Is.Not.Null);
        Entity anchor = PredictionAnchor();
        ClientProjectilePredictionUtility.ApplyImpact(client, new ClientPresentationEventElement
        {
            Identity = impact.identity, FlagA = impact.flagA, FlagB = impact.flagB,
        });
        Assert.That(client.Exists(anchor), Is.False);
        Assert.That(ClientSkillVisualPredictionUtility.TryReconcileNetworkEvent(client, new ClientPresentationEventElement
        {
            Identity = explosion.identity, Type = explosion.eventType, Position = targetPosition,
        }), Is.True);
    }

    [Test]
    public void ExpiredIdentifiedVisualIsNotReplayedAndAnotherCastDoesNotMatchIt()
    {
        _skills.Add(new SkillData { Id = 7, EffectChain = new[] { MakeEffect("spawn") } });
        Predict();
        EntityManager manager = _client.EntityManager;
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        var entry = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime)[0];
        manager.DestroyEntity(entry.VisualEntity);
        Assert.That(ClientSkillVisualPredictionUtility.TryReconcileNetworkEvent(manager, new ClientPresentationEventElement
        {
            Identity = entry.Identity, Type = entry.Type, Frame = 1000,
        }), Is.True);
        SkillEffectIdentity other = entry.Identity; other.CastOrdinal++;
        Assert.That(ClientSkillVisualPredictionUtility.TryReconcileNetworkEvent(manager, new ClientPresentationEventElement
        {
            Identity = other, Type = entry.Type, Frame = 10,
        }), Is.False);
    }

    [Test]
    public void ImpactConfirmationPreservesAreaEffectsOnSecondaryTargets()
    {
        EntityManager manager = _client.EntityManager;
        var data = ImpactProjectile();
        data.OnDestroyEffects = new EffectData[] { new AreaSearchEffectData
        {
            Radius = 1f, OnAfterSearch = new[] { MakeEffect("spawn") },
        } };
        _skills.Add(new SkillData { Id = 7, EffectChain = new EffectData[] { data } });
        float3 position = Origin + math.normalize(Aim - Origin) * 2f;
        Entity first = CreateTarget(manager, position);
        CreateTarget(manager, position + new float3(0.3f, 0.3f, 0));
        Predict(); Entity anchor = PredictionAnchor(); TickProjectile(_client, 0.1f);
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        var journal = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
        Assert.That(journal.Length, Is.EqualTo(3));
        Entity firstVisual = journal[1].VisualEntity, secondVisual = journal[2].VisualEntity;
        var identity = manager.GetComponentData<ClientPredictedProjectileComponent>(anchor).Identity;
        ClientProjectilePredictionUtility.ApplyImpact(manager, new ClientPresentationEventElement
        {
            Identity = identity.Impact(3, 1, manager.GetComponentData<NetworkIdentityComponent>(first).id), FlagA = 1, FlagB = 1,
        });
        Assert.That(manager.Exists(firstVisual), Is.True);
        Assert.That(manager.Exists(secondVisual), Is.True);
    }

    [Test]
    public void ProjectileAndImpactIdentitiesSurviveTheNetworkCodec()
    {
        SkillEffectIdentity identity = SkillEffectIdentity.Create(_client.EntityManager, _clientCaster, uint.MaxValue - 1, 2)
            .Child(4).Impact(2, 3, Guid.NewGuid()).Child(1);
        identity.CasterId = Guid.NewGuid();
        var message = new General_FrameStateData { frames = new List<NetworkFrameData>
        {
            new() { frameId = 12, datas = new List<NetworkStateData>
            {
                new NetworkProjectileStateData { identity = identity, ended = 1, hitSequence = 3 },
                new NetworkPresentationEventStateData { identity = identity, eventType = ClientPresentationEventType.ProjectileImpact },
            } },
        } };
        MessageCodec.Init();
        Assert.That(MessageCodec.TryDecode(MessageCodec.Encode(message), out _, out IMessage decoded, out Exception error),
            Is.EqualTo(MessageDecodeResult.Success), error?.ToString());
        var states = ((General_FrameStateData)decoded).frames[0].datas;
        Assert.That(((NetworkProjectileStateData)states[0]).identity.Equals(identity), Is.True);
        Assert.That(((NetworkPresentationEventStateData)states[1]).identity.Equals(identity), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReleasedProjectileSpawnsAndMovesOnceInTheSameWorldUpdate(bool online)
    {
        using World world = CreateWorld(online ? WorldFlags.GameServer : WorldFlags.Game,
            online ? GameWorldRole.Server : GameWorldRole.Standalone);
        EntityManager manager = world.EntityManager;
        Entity caster = CreateCaster(manager);
        EnsureSources(manager);
        world.GetOrCreateSystem<UnitQueryBuildSystem>().Update(world.Unmanaged);
        SpawnProjectileEffectData data = ImpactProjectile();
        _skills.Add(new SkillData { Id = 7, RuntimeType = "CommonSkill", EffectChain = new EffectData[] { data } });
        var group = world.GetOrCreateSystemManaged<SkillProjectileSimulationSystemGroup>();
        group.AddSystemToUpdateList(world.GetOrCreateSystem<SkillProjectileSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystemManaged<SkillProjectileCleanupSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystemManaged<EffectExecutionSystem>());
        group.SortSystems();
        var request = new SkillReleaseRequest
        {
            SkillId = 7, OriginEntity = caster, OriginPosition = Origin,
            HasTargetPosition = true, TargetPosition = Aim,
        };
        var context = SkillContentReferencePool.Get();
        try
        {
            Assert.That(SkillReleaseSnapshotUtility.TryCreate(manager, in request, out ResolvedSkillData resolved), Is.True);
            Assert.That(SkillReleaseUtility.TryExecute(manager, in request, resolved, context), Is.True);
        }
        finally { SkillContentReferencePool.Return(context); }
        using EntityQuery query = manager.CreateEntityQuery(typeof(SkillProjectileComponent), typeof(SkillProjectilePayloadComponent));
        Assert.That(query.IsEmptyIgnoreFilter, Is.True, "Release stays queued until the ordered skill phase.");
        world.SetTime(new TimeData(0.016, 0.016f));
        group.Update();
        manager.CompleteAllTrackedJobs();
        Entity projectile = query.GetSingletonEntity();
        float speed = manager.GetComponentData<SkillProjectileComponent>(projectile).Speed;
        Assert.That(manager.GetComponentData<SkillProjectileComponent>(projectile).TraveledDistance,
            Is.EqualTo(speed * 0.016f).Within(0.00001));
        group.Update();
        manager.CompleteAllTrackedJobs();
        Assert.That(manager.GetComponentData<SkillProjectileComponent>(projectile).TraveledDistance,
            Is.EqualTo(speed * 0.032f).Within(0.00001));
    }

    [Test]
    public void PlayerTickReleaseReachesMovingProjectileBeforeBattleUpdateFinishes()
    {
        using World world = CreateWorld(WorldFlags.GameServer, GameWorldRole.Server);
        EntityManager manager = world.EntityManager;
        Entity caster = CreateCaster(manager);
        EnsureSources(manager);
        world.GetOrCreateSystem<UnitQueryBuildSystem>().Update(world.Unmanaged);
        _skills.Add(new SkillData { Id = 7, RuntimeType = "CommonSkill", EffectChain = new EffectData[] { ImpactProjectile() } });
        world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        world.GetOrCreateSystemManaged<ClientInputSystemGroup>();
        world.GetOrCreateSystemManaged<UnitInitializationSystemGroup>();
        world.GetOrCreateSystemManaged<UnitDecisionSystemGroup>();
        world.GetOrCreateSystemManaged<UnitExecutionSystemGroup>();
        world.GetOrCreateSystemManaged<UnitPostProcessSystemGroup>();
        world.GetOrCreateSystemManaged<GamePresentationSystemGroup>();
        world.GetOrCreateSystemManaged<ClientPlayerPredictionSystemGroup>();
        world.GetOrCreateSystemManaged<FrameReceiveSystem>();
        world.GetOrCreateSystem<PlayerInputPulseResetSystem>();
        var projectiles = world.GetOrCreateSystemManaged<SkillProjectileSimulationSystemGroup>();
        projectiles.AddSystemToUpdateList(world.GetOrCreateSystem<SkillProjectileSystem>());
        projectiles.AddSystemToUpdateList(world.GetOrCreateSystemManaged<SkillProjectileCleanupSystem>());
        projectiles.AddSystemToUpdateList(world.GetOrCreateSystemManaged<EffectExecutionSystem>());
        var frame = new ServerFrameManager { running = true };
        FrameManagerUtility.Bind(manager, frame);
        var release = world.GetOrCreateSystemManaged<SkillReleaseTimingProbe>();
        release.Release = () =>
        {
            var request = new SkillReleaseRequest { SkillId = 7, OriginEntity = caster, OriginPosition = Origin,
                HasTargetPosition = true, TargetPosition = Aim };
            var context = SkillContentReferencePool.Get();
            try
            {
                Assert.That(SkillReleaseSnapshotUtility.TryCreate(manager, in request, out ResolvedSkillData resolved), Is.True);
                Assert.That(SkillReleaseUtility.TryExecute(manager, in request, resolved, context), Is.True);
            }
            finally { SkillContentReferencePool.Return(context); }
        };
        world.GetExistingSystemManaged<BattlePlayerSimulationSystemGroup>().AddSystemToUpdateList(release);
        // A float 0.033f rounds just below 33 ms when promoted to double.
        world.SetTime(new TimeData(0.034, 0.034f));
        world.GetExistingSystemManaged<BattleSimulationSystemGroup>().Update();
        manager.CompleteAllTrackedJobs();
        using EntityQuery query = manager.CreateEntityQuery(typeof(SkillProjectileComponent), typeof(SkillProjectilePayloadComponent));
        var projectile = manager.GetComponentData<SkillProjectileComponent>(query.GetSingletonEntity());
        Assert.That(projectile.TraveledDistance, Is.EqualTo(projectile.Speed * 0.034f).Within(0.00001));
        Assert.That(frame.currentFrame, Is.EqualTo(1));
    }

    private SpawnProjectileEffectData ImpactProjectile()
    {
        var data = (SpawnProjectileEffectData)MakeEffect("projectile");
        data.HitRadius = 0.2f;
        data.OnDestroyEffects = new EffectData[] { MakeEffect("spawn"), new DamageEffectData { FlatDamageBonus = 50 } };
        return data;
    }

    private Entity PredictionAnchor()
    {
        using EntityQuery query = _client.EntityManager.CreateEntityQuery(typeof(ClientPredictedProjectileComponent));
        return query.GetSingletonEntity();
    }

    private static Entity CreateTarget(EntityManager manager, float3 position)
    {
        Entity target = manager.CreateEntity(typeof(LocalTransform), typeof(UnitFactionComponent),
            typeof(NetworkIdentityComponent), typeof(UnitVitalityComponent));
        manager.SetComponentData(target, LocalTransform.FromPosition(position));
        manager.SetComponentData(target, new UnitFactionComponent { Value = UnitFactionType.Enemy });
        manager.SetComponentData(target, new NetworkIdentityComponent { id = Guid.NewGuid() });
        manager.SetComponentData(target, new UnitVitalityComponent { BaseMaxHealth = 100, CurrentHealth = 100 });
        return target;
    }

    private static void TickProjectile(World world, float delta)
    {
        world.SetTime(new TimeData(world.Time.ElapsedTime + delta, delta));
        EnsureSources(world.EntityManager);
        world.GetOrCreateSystem<UnitQueryBuildSystem>().Update(world.Unmanaged);
        world.GetOrCreateSystemManaged<UnitSourceDispatcherSystem>().Update();
        world.GetOrCreateSystem<ClientPredictedProjectileSystem>().Update(world.Unmanaged);
        world.EntityManager.CompleteAllTrackedJobs();
        world.GetOrCreateSystemManaged<SkillProjectileCleanupSystem>().Update();
        world.GetOrCreateSystemManaged<EffectExecutionSystem>().Update();
    }

    private static World CreateWorld(WorldFlags flags, GameWorldRole role)
    {
        World world = new("Skill parity " + role, flags);
        EntityManager manager = world.EntityManager;
        manager.SetComponentData(manager.CreateEntity(typeof(GameWorldContextComponent)),
            new GameWorldContextComponent { Role = role, SceneMode = GameSceneMode.Dungeon });
        Entity prefab = manager.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(TestVisual));
        manager.SetComponentData(prefab, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.RotateZ(0.3f), 1f));
        Entity projectile = manager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
        Entity registry = manager.CreateEntity(typeof(EntitySpawnRegistrySingleton));
        manager.AddBuffer<VfxEntityPrefabRegistryEntry>(registry).Add(new VfxEntityPrefabRegistryEntry { Name = "effect", Prefab = prefab });
        manager.AddBuffer<ProjectileEntityPrefabRegistryEntry>(registry).Add(new ProjectileEntityPrefabRegistryEntry { Name = "Projectile", Prefab = projectile });
        return world;
    }

    private static void EnsureSources(EntityManager manager)
    {
        foreach (Type type in new[] { typeof(GameInteractionComponent), typeof(PlayerSkillDefinitionRegistryComponent), typeof(WorldVariableComponent) })
        {
            using EntityQuery query = manager.CreateEntityQuery(ComponentType.ReadOnly(type));
            if (query.IsEmptyIgnoreFilter) manager.CreateEntity(ComponentType.ReadWrite(type));
        }
    }

    private static Entity CreateCaster(EntityManager manager)
    {
        Entity caster = manager.CreateEntity(typeof(LocalTransform), typeof(UnitFacingComponent));
        // The live caster has moved since the release snapshot.
        manager.SetComponentData(caster, LocalTransform.FromPositionRotationScale(new float3(20f, 30f, 0f), quaternion.RotateZ(1f), 1f));
        manager.SetComponentData(caster, new UnitFacingComponent { Direction = new float2(0f, 1f) });
        return caster;
    }

    private void Predict()
    {
        EntityManager manager = _client.EntityManager;
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        manager.GetBuffer<ClientSkillVisualRequestElement>(runtime).Add(new ClientSkillVisualRequestElement
        {
            Frame = 10, RequestOrdinal = 0,
            Request = new SkillReleaseRequest { SkillId = 7, OriginEntity = _clientCaster,
                OriginPosition = Origin, HasTargetPosition = true, TargetPosition = Aim },
        });
        Assert.DoesNotThrow(() => _prediction.Update());
    }

    private void ExecuteLocal(EffectData data) => ExecuteEffect(data, _local.EntityManager, _localCaster);

    private static void ExecuteEffect(EffectData data, EntityManager manager, Entity caster)
    {
        SkillContent context = SkillContentReferencePool.Get();
        try
        {
            context.EntityManager = manager;
            context.HasOriginEntity = true;
            context.OriginEntity = caster;
            context.HasOriginPositionSnapshot = true;
            context.OriginPositionSnapshot = Origin;
            context.HasPosition = true;
            context.Position = Aim;
            context.SourceSkillId = 7;
            Effect effect = data switch
            {
                SpawnVfxEffectData spawn => new SpawnVfxEffect(spawn),
                SpawnFollowVfxEffectData follow => new SpawnFollowVfxEffect(follow),
                SpawnLineVfxEffectData line => new SpawnLineVfxEffect(line),
                MoveVfxEffectData move => new MoveVfxEffect(move),
                SpawnProjectileEffectData projectile => new SpawnProjectileEffect(projectile),
                _ => throw new ArgumentException(),
            };
            effect.Execute(context);
        }
        finally { SkillContentReferencePool.Return(context); }
    }

    private static EffectData MakeEffect(string kind) => kind switch
    {
        "spawn" => new SpawnVfxEffectData { VfxPrefabName = "effect", Duration = 1f, Scale = 1.5f, AlignToCasterForward = true, SpawnOffset = new Vector3(1f, 2f, 0f) },
        "follow" => new SpawnFollowVfxEffectData { VfxPrefabName = "effect", Duration = 1f, Scale = 1.5f, AlignToTargetForward = true, SpawnOffset = new Vector3(1f, 2f, 0f) },
        "follow-static" => new SpawnFollowVfxEffectData { VfxPrefabName = "effect", Duration = 1f, Scale = 1.5f, AlignToTargetForward = false, SpawnOffset = new Vector3(1f, 2f, 0f) },
        "line" => new SpawnLineVfxEffectData { VfxPrefabName = "effect", Duration = 1f, Scale = 1.5f, Length = 4f, SegmentSpacing = 1f, OriginOffsetDistance = 1f, AlignToLineDirection = true },
        "line-preserved" => new SpawnLineVfxEffectData { VfxPrefabName = "effect", Duration = 1f, Scale = 1.5f, Length = 4f, SegmentSpacing = 1f, OriginOffsetDistance = 1f, AlignToLineDirection = false },
        "move" => new MoveVfxEffectData { VfxPrefabName = "effect", Duration = 0.5f, Scale = 1.5f, PreservePrefabRotation = true, StartOffset = new Vector3(0f, 6f, 0f), MoveOffset = new Vector3(0f, -6f, 0f) },
        "projectile" => new SpawnProjectileEffectData { VisualPrefabName = "effect", VisualScale = 1.5f, VisualOffset = new Vector3(0.5f, 1f, 0f), Speed = 10f, MaxRange = 20f, SpawnOffsetDistance = 1f },
        _ => throw new ArgumentException(kind),
    };

    private static Entity AddFollow(EntityManager manager, Entity target)
    {
        Entity visual = manager.CreateEntity(typeof(LocalTransform), typeof(EffectVisualFollowComponent));
        manager.SetComponentData(visual, LocalTransform.Identity);
        manager.SetComponentData(visual, new EffectVisualFollowComponent { Target = target, Offset = new float3(1f, 2f, 0f), AlignRotation = 1 });
        return visual;
    }

    private static Entity[] GetVisuals(EntityManager manager)
    {
        using EntityQuery query = manager.CreateEntityQuery(typeof(TestVisual));
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        return entities.ToArray();
    }

    private void AssertVisualsMatch()
    {
        var local = new List<LocalTransform>();
        var client = new List<LocalTransform>();
        foreach (Entity entity in GetVisuals(_local.EntityManager)) local.Add(_local.EntityManager.GetComponentData<LocalTransform>(entity));
        foreach (Entity entity in GetVisuals(_client.EntityManager)) client.Add(_client.EntityManager.GetComponentData<LocalTransform>(entity));
        Comparison<LocalTransform> order = (a, b) => a.Position.x != b.Position.x ? a.Position.x.CompareTo(b.Position.x) : a.Position.y.CompareTo(b.Position.y);
        local.Sort(order); client.Sort(order);
        Assert.That(client.Count, Is.EqualTo(local.Count));
        for (int index = 0; index < local.Count; index++) AssertTransform(local[index], client[index]);
    }

    private static void AssertTransform(LocalTransform expected, LocalTransform actual)
    {
        Assert.That(math.distance(expected.Position, actual.Position), Is.LessThan(0.0001f));
        Assert.That(actual.Scale, Is.EqualTo(expected.Scale).Within(0.0001f));
        AssertRotation(actual.Rotation, expected.Rotation);
    }

    private static void AssertRotation(quaternion actual, quaternion expected) =>
        Assert.That(math.abs(math.dot(actual.value, expected.value)), Is.GreaterThan(0.9999f));
}

[DisableAutoCreation]
public partial class SkillReleaseTimingProbe : SystemBase
{
    public Action Release;
    protected override void OnUpdate()
    {
        Action release = Release;
        Release = null;
        release?.Invoke();
    }
}
