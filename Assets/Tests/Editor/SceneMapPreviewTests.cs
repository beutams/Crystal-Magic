using System;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Editor.Map;
using CrystalMagic.Game.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class SceneMapPreviewTests
{
    [TearDown]
    public void ClearPreview() => SceneMapPreviewUtility.Clear();

    [Test]
    public void TrainingPreviewUsesRuntimeAssetScaleAndVisibleBounds()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var preview = SceneMapPreviewUtility.CreatePreview(scene, SceneMapLayout.Training, "Training");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(preview), Is.EqualTo(SceneMapLayout.Training.PrefabPath));
            Assert.That(SceneMapLayout.Training.PrefabPath, Is.EqualTo("Assets/Res/Tile/OcclusionMaps/TrainingMap/TrainingMap_Occlusion.prefab"));
            Assert.That(preview.GetComponent<TileOcclusionMap>(), Is.Not.Null);
            Assert.That(preview.GetComponentsInChildren<TileOcclusionSortAnchor>().Length, Is.GreaterThan(0));
            Assert.That(preview.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(preview.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(SceneMapPreviewUtility.TryGetVisibleBounds(preview, out Bounds bounds), Is.True);
            Assert.That(bounds.center.x, Is.EqualTo(7.5f).Within(0.001f));
            Assert.That(bounds.center.y, Is.EqualTo(-7.5f).Within(0.001f));
            Assert.That(bounds.size.x, Is.EqualTo(15f).Within(0.001f));
            Assert.That(bounds.size.y, Is.EqualTo(15f).Within(0.001f));
        }
        finally { SceneMapPreviewUtility.Clear(); EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void SwitchingPreviewReplacesOnlyBackdropAndPreservesPlacedObjects()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        var placedPlayer = new GameObject("Placed PlayerTown");
        SceneManager.MoveGameObjectToScene(placedPlayer, scene);
        Vector3 spawn = new Vector3(36.454f, -10.463f, -0.01f);
        placedPlayer.transform.position = spawn;
        try
        {
            var town = SceneMapPreviewUtility.CreatePreview(scene, SceneMapLayout.Town, "Town");
            var training = SceneMapPreviewUtility.CreatePreview(scene, SceneMapLayout.Training, "Training");
            Assert.That(town == null, Is.True);
            Assert.That(training != null, Is.True);
            Assert.That(placedPlayer.transform.position, Is.EqualTo(spawn));
            SceneMapPreviewUtility.Clear();
            SceneMapPreviewUtility.Clear();
            Assert.That(training == null, Is.True);
            Assert.That(placedPlayer != null, Is.True);
        }
        finally { SceneMapPreviewUtility.Clear(); EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void SavingLayoutDoesNotSerializeThePreview()
    {
        // Save a copy of the active scene: Unity cannot add a normal scene while
        // its initial untitled scene is unsaved, and preview scenes cannot save.
        // Never close or save over the active scene; remove only this test's objects.
        Scene scene = SceneManager.GetActiveScene();
        string path = "Assets/__MapPreviewSaveTest_" + Guid.NewGuid().ToString("N") + ".unity";
        GameObject player = null;
        try
        {
            player = new GameObject("Authored Spawn");
            SceneManager.MoveGameObjectToScene(player, scene);
            player.transform.position = new Vector3(36.454f, -10.463f, -0.01f);
            SceneMapPreviewUtility.CreatePreview(scene, SceneMapLayout.Training, "Training");
            Assert.That(EditorSceneManager.SaveScene(scene, path, saveAsCopy: true), Is.True);
            string saved = File.ReadAllText(path);
            Assert.That(saved, Does.Contain("Authored Spawn"));
            Assert.That(saved, Does.Contain("x: 36.454"));
            Assert.That(saved, Does.Not.Contain("[Editor Preview]"));
        }
        finally
        {
            SceneMapPreviewUtility.Clear();
            if (player != null) Object.DestroyImmediate(player);
            AssetDatabase.DeleteAsset(path);
        }
    }
}
