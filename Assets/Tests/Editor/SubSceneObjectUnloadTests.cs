using System.Collections;
using CrystalMagic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SubSceneObjectUnloadTests
{
    [UnityTest]
    public IEnumerator OpenTownObjectsAreHiddenImmediatelyAndDestroyedBeforeUnloadCompletes()
    {
        yield return new EnterPlayMode();
        Scene town = SceneManager.CreateScene("Old Town SubScene");
        Scene dungeon = SceneManager.CreateScene("Current Dungeon");
        GameObject townNpc = new("Town NPC", typeof(SpriteRenderer), typeof(BoxCollider2D));
        SceneManager.MoveGameObjectToScene(townNpc, town);
        GameObject dungeonUnit = new("Dungeon unit");
        SceneManager.MoveGameObjectToScene(dungeonUnit, dungeon);

        AsyncOperation unload = SceneComponent.UnloadSubSceneGameObjects(town);
        Assert.That(unload, Is.Not.Null);
        Assert.That(townNpc.activeInHierarchy, Is.False, "Old sprites and colliders must stop before the next frame.");
        Assert.That(dungeonUnit.activeInHierarchy, Is.True);
        yield return unload;
        Assert.That(townNpc == null, Is.True);
        Assert.That(town.isLoaded, Is.False);
        Assert.That(dungeonUnit != null && dungeonUnit.activeInHierarchy, Is.True);
        Assert.That(SceneComponent.UnloadSubSceneGameObjects(town), Is.Null);
        yield return SceneManager.UnloadSceneAsync(dungeon);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }
}
