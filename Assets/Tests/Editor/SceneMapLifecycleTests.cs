using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class SceneMapLifecycleTests
{
    private readonly List<GameObject> _objects = new();
    private static Type RootType => typeof(SceneMapPresentation).Assembly.GetType("CrystalMagic.Core.DungeonSceneRuntimeRoot", true);

    private GameObject NewRoot(string name)
    {
        var root = new GameObject(name);
        _objects.Add(root);
        return root;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _objects) if (obj != null) Object.DestroyImmediate(obj);
        _objects.Clear();
    }

    [Test]
    public void TownTrainingDungeonNextFloorTownThenMenuHaveOnlyOneOwner()
    {
        var maps = new SceneMapPresentation();
        int released = 0;
        GameObject previous = null;
        foreach (string scene in new[] { "TownScene", "TrainingScene", "DungeonScene", "DungeonScene", "TownScene" })
        {
            GameObject root = NewRoot(scene);
            maps.Replace(scene, root, () =>
            {
                Assert.That(root.activeSelf, Is.False, "Hide before any delayed destruction.");
                Assert.That(maps.Root, Is.Null, "Revoke ownership before release callbacks.");
                released++;
            });
            if (previous != null)
            {
                Assert.That(previous.activeSelf, Is.False);
                maps.ClearIfCurrent(previous); // Old generation's finally must not clear the new one.
            }
            Assert.That(maps.Root, Is.SameAs(root));
            Assert.That(root.activeSelf, Is.True);
            previous = root;
        }
        maps.Clear();
        maps.Clear();
        Assert.That(released, Is.EqualTo(5));
        Assert.That(maps.SceneName, Is.Null);
        Assert.That(previous.activeSelf, Is.False);
    }

    [Test]
    public void ReleaseFailureStillRevokesLeaseAndDoesNotReleaseTwice()
    {
        var maps = new SceneMapPresentation();
        GameObject root = NewRoot("Partial build");
        maps.Replace("DungeonScene", root, () => throw new InvalidOperationException("cleanup failure"));
        Assert.Throws<InvalidOperationException>(() => maps.Clear());
        Assert.That(root.activeSelf, Is.False);
        Assert.That(maps.Root, Is.Null);
        Assert.DoesNotThrow(() => maps.Clear());
    }

    [Test]
    public void PartialBuildEntitiesAndRuntimeDescendantsAreReleasedBeforeNextDungeon()
    {
        using var world = new World("Map lease");
        EntityManager em = world.EntityManager;
        var tracked = new List<Entity>();
        GameObject old = NewRoot("Old dungeon");
        Component runtime = old.AddComponent(RootType);
        RootType.GetMethod("Initialize").Invoke(runtime, new object[] { "test-map", tracked, world });
        Entity partial = em.CreateEntity();
        tracked.Add(partial); // Added after Initialize, as in an interrupted coroutine.
        Entity descendant = em.CreateEntity(typeof(DungeonRuntimeOwnedEntity), typeof(Disabled));
        Entity unrelated = em.CreateEntity();
        RootType.GetMethod("ReleaseContents").Invoke(runtime, new object[] { true });
        Assert.That(em.Exists(partial), Is.False);
        Assert.That(em.Exists(descendant), Is.False);
        Assert.That(em.Exists(unrelated), Is.True);
        Entity nextDungeon = em.CreateEntity(typeof(DungeonRuntimeOwnedEntity));
        RootType.GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
        Object.DestroyImmediate(old); // Delayed OnDestroy from previous generation.
        Assert.That(em.Exists(nextDungeon), Is.True);
    }

    [Test]
    public void OnDestroyFallbackCannotDeleteOtherGenerationsOrAnUnrelatedWorld()
    {
        using var world = new World("Old world");
        using var server = new World("Headless authority");
        Entity oldEntity = world.EntityManager.CreateEntity();
        Entity newEntity = world.EntityManager.CreateEntity(typeof(DungeonRuntimeOwnedEntity));
        Entity serverEntity = server.EntityManager.CreateEntity(typeof(DungeonRuntimeOwnedEntity));
        GameObject old = NewRoot("Interrupted root");
        Component runtime = old.AddComponent(RootType);
        RootType.GetMethod("Initialize").Invoke(runtime, new object[] { "test-fallback", new List<Entity> { oldEntity }, world });
        // Unity does not send OnDestroy to ordinary MonoBehaviours in EditMode.
        RootType.GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
        Object.DestroyImmediate(old);
        Assert.That(world.EntityManager.Exists(oldEntity), Is.False);
        Assert.That(world.EntityManager.Exists(newEntity), Is.True);
        Assert.That(server.EntityManager.Exists(serverEntity), Is.True);
    }

    [Test]
    public void CleanupAfterWorldDisposalIsSafe()
    {
        var world = new World("Already disposed world");
        GameObject root = NewRoot("Root");
        Component runtime = root.AddComponent(RootType);
        RootType.GetMethod("Initialize").Invoke(runtime, new object[] { "test-disposed", new List<Entity>(), world });
        world.Dispose();
        Assert.DoesNotThrow(() => RootType.GetMethod("ReleaseContents").Invoke(runtime, new object[] { true }));
    }

    [UnityTest]
    public IEnumerator ProceduralPoolReleaseHidesEntireHierarchyImmediately()
    {
        yield return new EnterPlayMode();
        var host = new GameObject("Map pool test");
        PoolComponent pool = host.AddComponent<PoolComponent>();
        pool.Initialize();
        GameObject old = pool.CreateTransient("Dungeon");
        GameObject child = pool.CreateTransient("Terrain", old.transform);
        GameObject next = pool.CreateTransient("Next dungeon");
        pool.Release(old);
        Assert.That(child.activeInHierarchy, Is.False);
        Assert.That(next.activeInHierarchy, Is.True);
        yield return null;
        Assert.That(old == null && child == null, Is.True);
        Assert.That(next != null, Is.True);
        pool.Cleanup();
        Object.Destroy(host);
        yield return null;
        Assert.That(next == null, Is.True);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
