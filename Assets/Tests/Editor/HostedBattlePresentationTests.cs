using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public sealed class HostedBattlePresentationTests
{
    private static readonly Type ActivationType = typeof(Baker<>).Assembly
        .GetType("Unity.Entities.CompanionGameObjectUpdateSystem", true);
    private static readonly Type LinkType = typeof(Baker<>).Assembly
        .GetType("Unity.Entities.CompanionLink", true);
    private static readonly Type ReferenceType = typeof(Baker<>).Assembly
        .GetType("Unity.Entities.CompanionReference", true);

    [TestCase(WorldFlags.Game, true)]
    [TestCase(WorldFlags.GameClient, true)]
    [TestCase(WorldFlags.GameServer, false)]
    public void OnlyServerWorldDisablesRenderingAndCompanionTransformUpdates(WorldFlags flags, bool visible)
    {
        using World world = CreateWorld("Presentation filtering", flags);
        BattleWorldContext.DisableServerPresentation(world);
        Assert.That(world.Unmanaged.ResolveSystemStateRef(world.GetExistingSystem(ActivationType)).Enabled, Is.EqualTo(visible));
        Assert.That(world.Unmanaged.ResolveSystemStateRef(world.GetExistingSystem<CompanionGameObjectUpdateTransformSystem>()).Enabled,
            Is.EqualTo(visible));
        Assert.That(world.GetExistingSystemManaged<PresentationSystemGroup>().Enabled, Is.EqualTo(visible));
        Assert.That(world.GetExistingSystemManaged<TransformSystemGroup>().Enabled, Is.True,
            "Authoritative ECS transforms must keep updating even when companion objects are hidden.");
    }

    [Test]
    public void DefaultUnityCompanionSystemAlsoActivatesAVisualInServerWorld()
    {
        using World world = CreateWorld("Original hosted rendering", WorldFlags.GameServer);
        Player(world, Guid.NewGuid(), out GameObject visual);
        world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
        Assert.That(visual.activeInHierarchy, Is.True,
            "The GameServer flag alone does not stop the default companion activation system.");
    }

    [Test]
    public void HostShowsOneCharacterWhenServerAndClientHaveTheSameNetworkPlayer()
    {
        using World server = CreateWorld("Hosted authority", WorldFlags.GameServer);
        using World client = CreateWorld("Hosted client", WorldFlags.GameClient);
        Guid id = Guid.NewGuid();
        Entity serverPlayer = Player(server, id, out GameObject serverVisual);
        Entity clientPlayer = Player(client, id, out GameObject clientVisual);
        BattleWorldContext.DisableServerPresentation(server);
        server.GetExistingSystemManaged<InitializationSystemGroup>().Update();
        client.GetExistingSystemManaged<InitializationSystemGroup>().Update();
        Assert.That(serverVisual.activeInHierarchy, Is.False);
        Assert.That(clientVisual.activeInHierarchy, Is.True);
        Assert.That(server.EntityManager.Exists(serverPlayer), Is.True);
        Assert.That(client.EntityManager.Exists(clientPlayer), Is.True);
        Assert.That(server.EntityManager.GetComponentData<NetworkIdentityComponent>(serverPlayer).id, Is.EqualTo(id));
        Assert.That(server.EntityManager.HasComponent<PhysicsCollider>(serverPlayer), Is.True);
        Assert.That(server.EntityManager.HasComponent<PlayerInputComponent>(serverPlayer), Is.True);
        Assert.That(server.EntityManager.GetComponentData<PhysicsVelocity>(serverPlayer).Linear,
            Is.EqualTo(new float3(2f, 0f, 0f)));
    }

    [Test]
    public void HiddenServerCompanionIsStillDestroyedWithItsEntity()
    {
        using World world = CreateWorld("Hidden companion ownership", WorldFlags.GameServer);
        Entity player = Player(world, Guid.NewGuid(), out GameObject visual);
        BattleWorldContext.DisableServerPresentation(world);
        world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
        world.EntityManager.DestroyEntity(player);
        Assert.That(visual == null, Is.True);
    }

    private static World CreateWorld(string name, WorldFlags flags)
    {
        World world = new(name, flags);
        var initialization = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
        initialization.AddSystemToUpdateList(world.GetOrCreateSystem(ActivationType));
        world.GetOrCreateSystemManaged<PresentationSystemGroup>();
        world.GetOrCreateSystem<CompanionGameObjectUpdateTransformSystem>();
        world.GetOrCreateSystemManaged<TransformSystemGroup>();
        return world;
    }

    private static Entity Player(World world, Guid id, out GameObject visual)
    {
        EntityManager manager = world.EntityManager;
        Entity player = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(PlayerInputComponent),
            typeof(PhysicsCollider), typeof(PhysicsVelocity));
        manager.SetComponentData(player, new NetworkIdentityComponent { id = id });
        manager.SetComponentData(player, new PhysicsVelocity { Linear = new float3(2f, 0f, 0f) });
        visual = new GameObject("Player visual", typeof(SpriteRenderer));
        visual.SetActive(false);
        manager.AddComponentObject(player, visual.GetComponent<SpriteRenderer>());
        object link = Activator.CreateInstance(LinkType);
        LinkType.GetField("Companion").SetValue(link, (UnityObjectRef<GameObject>)visual);
        typeof(EntityManager).GetMethods().Single(method => method.Name == "AddComponentData" &&
            method.IsGenericMethod && method.GetParameters()[0].ParameterType == typeof(Entity))
            .MakeGenericMethod(LinkType).Invoke(manager, new[] { (object)player, link });
        object reference = Activator.CreateInstance(ReferenceType);
        ReferenceType.GetField("Companion").SetValue(reference, (UnityObjectRef<GameObject>)visual);
        manager.AddComponentObject(player, reference);
        return player;
    }
}
