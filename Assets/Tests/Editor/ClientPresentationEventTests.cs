using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using CrystalMagic.Game.Skill;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed class ClientPresentationEventTests
{
    private World _world;
    private EntityManager Manager => _world.EntityManager;
    private ClientPresentationEventSystem _system;
    private Entity _eventEntity;
    private GameObject _eventRoot;
    private EventComponent _bus;
    private List<int> _played;

    [SetUp]
    public void SetUp()
    {
        _world = new World("Presentation buffer lifetime", WorldFlags.GameClient);
        _system = _world.GetOrCreateSystemManaged<ClientPresentationEventSystem>();
        _eventEntity = Manager.CreateEntity(typeof(ClientPresentationClockComponent));
        Manager.AddBuffer<ClientPresentationEventElement>(_eventEntity);
        _eventRoot = new GameObject("Presentation event bus");
        _bus = _eventRoot.AddComponent<EventComponent>();
        _played = new List<int>();
        _bus.Subscribe<NotificationSignalEvent>(signal => _played.Add(signal.Scope));
    }

    [TearDown]
    public void TearDown()
    {
        _world.Dispose();
        _bus.Cleanup();
        UnityEngine.Object.DestroyImmediate(_eventRoot);
    }

    [TestCase(ClientPresentationEventType.SpawnVfx, 1)]
    [TestCase(ClientPresentationEventType.SpawnFollowVfx, 1)]
    [TestCase(ClientPresentationEventType.SpawnLineVfx, 2)]
    [TestCase(ClientPresentationEventType.MoveVfx, 1)]
    public void CreatingEffectsDoesNotInvalidateTheRemainingPresentationEvents(ClientPresentationEventType type, int expectedEffects)
    {
        RegisterEffectPrefab();
        Entity target = Manager.CreateEntity(typeof(LocalTransform));
        Queue(new ClientPresentationEventElement
        {
            Type = type, Sequence = 1, Target = target, AssetName = "effect",
            Rotation = quaternion.identity, Scale = 1f, Duration = 1f,
            ValueA = 2f, ValueB = 1f, SecondaryPosition = new float3(1f, 0f, 0f),
        });
        Queue(Notification(2));
        Assert.DoesNotThrow(() => _system.Update());
        using EntityQuery effects = Manager.CreateEntityQuery(typeof(DungeonRuntimeOwnedEntity));
        Assert.That(effects.CalculateEntityCount(), Is.EqualTo(expectedEffects));
        Assert.That(_played, Is.EqualTo(new[] { 2 }));
        AssertDrained(2);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void ReconcilingAProjectileKeepsItsPredictionAnchorWithoutInvalidatingBuffers(bool hasFollow, bool hasAnchor)
    {
        Entity source = Manager.CreateEntity(), target = Manager.CreateEntity(), visual = Manager.CreateEntity();
        if (hasFollow) Manager.AddComponent<EffectVisualFollowComponent>(visual);
        Entity anchor = hasAnchor ? Manager.CreateEntity() : Entity.Null;
        SkillEffectIdentity identity = SkillEffectIdentity.Create(Manager, source, 10, 0).Child(0);
        if (hasAnchor)
        {
            Manager.AddComponentData(anchor, new ClientPredictedProjectileComponent { Identity = identity });
            if (hasFollow) Manager.SetComponentData(visual, new EffectVisualFollowComponent { Target = anchor });
        }
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(Manager);
        Manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).Add(new ClientPredictedSkillVisualEventElement
        {
            Identity = identity, Frame = 10, Source = source, SkillId = 7, Type = ClientPresentationEventType.SpawnFollowVfx,
            AssetName = "effect", IsProjectile = 1, VisualEntity = visual, AnchorEntity = anchor,
        });
        Queue(new ClientPresentationEventElement
        {
            Identity = identity, Frame = 11, Sequence = 1, Source = source, Target = target, SourceSkillId = 7,
            Type = ClientPresentationEventType.SpawnFollowVfx, AssetName = "effect",
        });
        Queue(Notification(2));
        Assert.DoesNotThrow(() => _system.Update());
        var predicted = Manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime)[0];
        Assert.That(predicted.IsConfirmed, Is.EqualTo(1));
        Assert.That(predicted.AnchorEntity, Is.EqualTo(anchor));
        if (hasFollow && hasAnchor)
            Assert.That(Manager.GetComponentData<EffectVisualFollowComponent>(visual).Target, Is.EqualTo(anchor));
        if (hasAnchor)
        {
            Assert.That(Manager.Exists(anchor), Is.True);
            Assert.That(Manager.GetComponentData<ClientPredictedProjectileLinkComponent>(target).Prediction, Is.EqualTo(anchor));
        }
        Assert.That(_played, Is.EqualTo(new[] { 2 }));
        AssertDrained(2);
    }

    [Test]
    public void CallbackStructuralChangesKeepNewEventsForNextUpdateAndPreserveNewClockData()
    {
        _bus.Subscribe<NotificationSignalEvent>(signal =>
        {
            if (signal.Scope != 1) return;
            Manager.CreateEntity();
            Queue(Notification(3));
            var clock = Manager.GetComponentData<ClientPresentationClockComponent>(_eventEntity);
            clock.LatestAppliedFrame = 99;
            clock.Revision = 4;
            Manager.SetComponentData(_eventEntity, clock);
        });
        Queue(Notification(1));
        Queue(Notification(2));
        Assert.DoesNotThrow(() => _system.Update());
        Assert.That(_played, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(Manager.GetBuffer<ClientPresentationEventElement>(_eventEntity).Length, Is.EqualTo(1));
        var current = Manager.GetComponentData<ClientPresentationClockComponent>(_eventEntity);
        Assert.That(current.LastConsumedEventSequence, Is.EqualTo(2));
        Assert.That(current.LatestAppliedFrame, Is.EqualTo(99));
        Assert.That(current.Revision, Is.EqualTo(4));
        Assert.DoesNotThrow(() => _system.Update());
        Assert.That(_played, Is.EqualTo(new[] { 1, 2, 3 }));
        AssertDrained(3);
    }

    [Test]
    public void DuplicateZeroAndAlreadyConsumedSequencesAreNotPlayedAgain()
    {
        Manager.SetComponentData(_eventEntity, new ClientPresentationClockComponent { LastConsumedEventSequence = 5 });
        foreach (uint sequence in new uint[] { 0, 4, 5, 6, 6, 7 }) Queue(Notification(sequence));
        _system.Update();
        _system.Update();
        Assert.That(_played, Is.EqualTo(new[] { 6, 7 }));
        AssertDrained(7);
    }

    [Test]
    public void CallbackCanRemoveEventEntityWithoutWritingToAStaleClock()
    {
        _bus.Subscribe<NotificationSignalEvent>(signal => Manager.DestroyEntity(_eventEntity));
        Queue(Notification(1));
        Assert.DoesNotThrow(() => _system.Update());
        Assert.That(Manager.Exists(_eventEntity), Is.False);
        Assert.That(_played, Is.EqualTo(new[] { 1 }));
    }

    private void RegisterEffectPrefab()
    {
        Entity prefab = Manager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
        Entity registry = Manager.CreateEntity(typeof(EntitySpawnRegistrySingleton));
        Manager.AddBuffer<VfxEntityPrefabRegistryEntry>(registry).Add(new VfxEntityPrefabRegistryEntry { Name = "effect", Prefab = prefab });
    }

    private void Queue(ClientPresentationEventElement value) => Manager.GetBuffer<ClientPresentationEventElement>(_eventEntity).Add(value);
    private static ClientPresentationEventElement Notification(uint sequence) => new()
    {
        Type = ClientPresentationEventType.Notification, Sequence = sequence, IntValue = (int)sequence, AssetName = "test",
    };
    private void AssertDrained(uint sequence)
    {
        Assert.That(Manager.GetBuffer<ClientPresentationEventElement>(_eventEntity).Length, Is.Zero);
        Assert.That(Manager.GetComponentData<ClientPresentationClockComponent>(_eventEntity).LastConsumedEventSequence, Is.EqualTo(sequence));
    }
}
