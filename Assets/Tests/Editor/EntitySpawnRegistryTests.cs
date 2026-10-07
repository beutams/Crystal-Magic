using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

public sealed class EntitySpawnRegistryTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void AllPrefabKindsCanSpawnFromTheSecondRegistry(int kind)
    {
        using var world = new World("Multiple spawn registries");
        EntityManager manager = world.EntityManager;
        Entity first = Registry(manager), second = Registry(manager);
        Entity prefab = manager.CreateEntity(typeof(Prefab));
        AddEntries(manager, first, "Other", prefab);
        AddEntries(manager, first, "Requested", Entity.Null);
        AddEntries(manager, second, "Requested", prefab);
        FixedString128Bytes name = "Requested";
        Entity instance;
        bool spawned = kind switch
        {
            0 => EntitySpawnRegistryUtility.TryInstantiateUnit(manager, name, out instance),
            1 => EntitySpawnRegistryUtility.TryInstantiateProjectile(manager, name, out instance),
            2 => EntitySpawnRegistryUtility.TryInstantiateDrop(manager, name, out instance),
            3 => EntitySpawnRegistryUtility.TryInstantiateEnvironment(manager, name, out instance),
            _ => EntitySpawnRegistryUtility.TryInstantiateVfx(manager, name, out instance),
        };
        Assert.That(spawned, Is.True);
        Assert.That(manager.Exists(instance), Is.True);
        Assert.That(manager.HasComponent<Prefab>(instance), Is.False);
        Assert.That(manager.IsComponentEnabled<DestroyEntityFlag>(instance), Is.False);
        Assert.That(manager.Exists(first) && manager.Exists(second), Is.True);
    }

    [Test]
    public void UnitListDeduplicatesNamesAndSkipsDestroyedPrefabs()
    {
        using var world = new World("Registry names");
        EntityManager manager = world.EntityManager;
        Entity first = Registry(manager), second = Registry(manager);
        Entity prefab = manager.CreateEntity(typeof(Prefab));
        Entity destroyed = manager.CreateEntity(typeof(Prefab));
        manager.DestroyEntity(destroyed);
        AddEntries(manager, first, "Zombie", prefab);
        AddEntries(manager, second, "Zombie", prefab);
        AddEntries(manager, second, "Bat", prefab);
        AddEntries(manager, first, "Destroyed", destroyed);
        var names = new List<string> { "Previous scene" };
        EntitySpawnRegistryUtility.GetRegisteredUnitNames(manager, names);
        Assert.That(names, Is.EqualTo(new[] { "Bat", "Zombie" }));
    }

    [Test]
    public void OldSceneRegistryIsIgnoredWhenItsMetadataIsRemoved()
    {
        using var world = new World("Unloading registry");
        EntityManager manager = world.EntityManager;
        Entity oldRegistry = Registry(manager), currentRegistry = Registry(manager);
        Entity oldPrefab = manager.CreateEntity(typeof(Prefab));
        Entity currentPrefab = manager.CreateEntity(typeof(Prefab));
        AddEntries(manager, oldRegistry, "Rock", oldPrefab);
        AddEntries(manager, currentRegistry, "Rock", currentPrefab);
        Entity scene = manager.CreateEntity(typeof(RequestSceneLoaded));
        Entity section = manager.CreateEntity(typeof(SceneEntityReference));
        manager.SetComponentData(section, new SceneEntityReference { SceneEntity = scene });
        manager.AddSharedComponent(oldRegistry, new SceneTag { SceneEntity = section });
        manager.RemoveComponent<RequestSceneLoaded>(scene);
        Assert.That(EntitySpawnRegistryUtility.TryGetEnvironmentPrefab(manager, new FixedString128Bytes("Rock"), out Entity prefab), Is.True);
        Assert.That(prefab, Is.EqualTo(currentPrefab));
        manager.DestroyEntity(section);
        Assert.That(EntitySpawnRegistryUtility.TryGetUnitPrefab(manager, new FixedString128Bytes("Rock"), out prefab), Is.True);
        Assert.That(prefab, Is.EqualTo(currentPrefab));
        manager.DestroyEntity(currentRegistry);
        Assert.That(EntitySpawnRegistryUtility.HasRegistry(manager), Is.False);
        Assert.That(EntitySpawnRegistryUtility.TryGetEnvironmentPrefab(manager, new FixedString128Bytes("Rock"), out _), Is.False);
    }

    [Test]
    public void LiveBakedSectionUsesItsParentSceneRequest()
    {
        using var world = new World("Live baked registry");
        EntityManager manager = world.EntityManager;
        Entity registry = Registry(manager);
        Entity prefab = manager.CreateEntity(typeof(Prefab));
        AddEntries(manager, registry, "Rock", prefab);
        Entity scene = manager.CreateEntity(typeof(RequestSceneLoaded));
        Entity section = manager.CreateEntity(typeof(SceneEntityReference));
        manager.SetComponentData(section, new SceneEntityReference { SceneEntity = scene });
        manager.AddSharedComponent(registry, new SceneTag { SceneEntity = section });
        // Live baking does not put RequestSceneLoaded on the section itself.
        Assert.That(EntitySpawnRegistryUtility.HasRegistry(manager), Is.True);
        Assert.That(EntitySpawnRegistryUtility.TryGetEnvironmentPrefab(manager, new FixedString128Bytes("Rock"), out _), Is.True);
        manager.DestroyEntity(scene);
        Assert.That(EntitySpawnRegistryUtility.HasRegistry(manager), Is.False);
    }

    [Test]
    public void UnloadRemovesEveryOldSceneCopyAndPreservesOtherScenes()
    {
        using var world = new World("Scene unload");
        var oldGuid = new Hash128("11111111111111111111111111111111");
        var currentGuid = new Hash128("22222222222222222222222222222222");
        Entity first = SceneSystem.LoadSceneAsync(world.Unmanaged, oldGuid);
        Entity second = SceneSystem.LoadSceneAsync(world.Unmanaged, oldGuid,
            new SceneSystem.LoadParameters { Flags = SceneLoadFlags.NewInstance });
        Entity current = SceneSystem.LoadSceneAsync(world.Unmanaged, currentGuid);
        SceneComponent.UnloadSubScene(world, oldGuid);
        Assert.That(world.EntityManager.Exists(first), Is.False);
        Assert.That(world.EntityManager.Exists(second), Is.False);
        Assert.That(world.EntityManager.Exists(current), Is.True);
        SceneComponent.UnloadSubScene(world, oldGuid); // Repeated cleanup is harmless.
    }

    private static Entity Registry(EntityManager manager) => manager.CreateEntity(
        typeof(EntitySpawnRegistrySingleton), typeof(UnitEntityPrefabRegistryEntry),
        typeof(ProjectileEntityPrefabRegistryEntry), typeof(DropEntityPrefabRegistryEntry),
        typeof(EnvironmentEntityPrefabRegistryEntry), typeof(VfxEntityPrefabRegistryEntry));

    private static void AddEntries(EntityManager manager, Entity registry, FixedString128Bytes name, Entity prefab)
    {
        manager.GetBuffer<UnitEntityPrefabRegistryEntry>(registry).Add(new UnitEntityPrefabRegistryEntry { Name = name, Prefab = prefab });
        manager.GetBuffer<ProjectileEntityPrefabRegistryEntry>(registry).Add(new ProjectileEntityPrefabRegistryEntry { Name = name, Prefab = prefab });
        manager.GetBuffer<DropEntityPrefabRegistryEntry>(registry).Add(new DropEntityPrefabRegistryEntry { Name = name, Prefab = prefab });
        manager.GetBuffer<EnvironmentEntityPrefabRegistryEntry>(registry).Add(new EnvironmentEntityPrefabRegistryEntry { Name = name, Prefab = prefab });
        manager.GetBuffer<VfxEntityPrefabRegistryEntry>(registry).Add(new VfxEntityPrefabRegistryEntry { Name = name, Prefab = prefab });
    }
}
