using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CrystalMagic.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SubSceneHierarchyCleanupTests
{
    private string _directory;
    private Scene _originalActiveScene;
    private bool _originalPlayOptionsEnabled;
    private EnterPlayModeOptions _originalPlayOptions;

    [SetUp]
    public void SetUp()
    {
        _originalActiveScene = SceneManager.GetActiveScene();
        _originalPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _originalPlayOptions = EditorSettings.enterPlayModeOptions;
        _directory = "Assets/__SubSceneHierarchyCleanupTests-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(_directory);
        AssetDatabase.Refresh();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = EditorSceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = EditorSceneManager.GetSceneAt(i);
            if (scene.path.StartsWith(_directory + "/", StringComparison.Ordinal))
                EditorSceneManager.CloseScene(scene, true);
        }
        if (_originalActiveScene.IsValid() && _originalActiveScene.isLoaded)
            SceneManager.SetActiveScene(_originalActiveScene);
        AssetDatabase.DeleteAsset(_directory);
        EditorSettings.enterPlayModeOptionsEnabled = _originalPlayOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _originalPlayOptions;
    }

    private Scene CreateScene(string name, bool isSubScene)
    {
        string path = _directory + "/" + name + ".unity";
        // Save a copy without changing the runner's unsaved active scene. Keep
        // Unity's scene settings, but never instantiate copied authoring objects.
        Assert.That(EditorSceneManager.SaveScene(_originalActiveScene, path, saveAsCopy: true), Is.True);
        string serialized = File.ReadAllText(path);
        var emptyScene = new StringBuilder("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
        foreach (Match settings in Regex.Matches(serialized, @"(?ms)^--- !u!(?:29|104|157|196) &.*?(?=^--- !u!|\z)"))
            emptyScene.Append(settings.Value);
        emptyScene.Append("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n");
        File.WriteAllText(path, emptyScene.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        scene.isSubScene = isSubScene;
        return scene;
    }

    [Test]
    public void RemovesUnloadedSubSceneEntryAndCanBeRepeated()
    {
        Scene subScene = CreateScene("TrainingSubScene", true);
        string path = subScene.path;
        Assert.That(EditorSceneManager.CloseScene(subScene, false), Is.True);
        Assert.That(subScene.IsValid() && !subScene.isLoaded, Is.True);

        Assert.That(SubSceneHierarchyCleanup.RemoveUnloadedSubScenes(), Is.GreaterThanOrEqualTo(1));
        Assert.That(SceneManager.GetSceneByPath(path).IsValid(), Is.False);
        Assert.That(SubSceneHierarchyCleanup.RemoveUnloadedSubScenes(), Is.Zero);
    }

    [Test]
    public void PreservesLoadedDirtySubSceneAndOrdinaryUnloadedScene()
    {
        Scene subScene = CreateScene("OpenTrainingSubScene", true);
        var placedObject = new GameObject("Unsaved authoring edit");
        SceneManager.MoveGameObjectToScene(placedObject, subScene);
        EditorSceneManager.MarkSceneDirty(subScene);
        Scene ordinary = CreateScene("UnloadedOrdinaryScene", false);
        Assert.That(EditorSceneManager.CloseScene(ordinary, false), Is.True);

        SubSceneHierarchyCleanup.RemoveUnloadedSubScenes();

        Assert.That(subScene.IsValid() && subScene.isLoaded && subScene.isDirty, Is.True);
        Assert.That(placedObject != null, Is.True);
        Assert.That(ordinary.IsValid() && !ordinary.isLoaded, Is.True);
    }

    [UnityTest]
    public IEnumerator RestoresUnsavedAuthoringSceneAfterRuntimeUnloadWithSceneReloadEnabled()
    {
        Scene authored = CreateScene("AuthoredTrainingSubScene", true);
        string path = authored.path;
        var placedObject = new GameObject("Unsaved placed object");
        SceneManager.MoveGameObjectToScene(placedObject, authored);
        placedObject.transform.position = new Vector3(42f, 7f, 0f);
        EditorSceneManager.MarkSceneDirty(authored);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        SubSceneHierarchyCleanup.PrepareForPlayMode();
        Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(EnterPlayModeOptions.DisableDomainReload));
        Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);

        yield return new EnterPlayMode();
        Scene runtimeScene = SceneManager.GetSceneByPath(path);
        Assert.That(runtimeScene.IsValid() && runtimeScene.isLoaded, Is.True);
        yield return SceneManager.UnloadSceneAsync(runtimeScene);
        Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False);
        yield return new ExitPlayMode();

        Scene restored = SceneManager.GetSceneByPath(path);
        Assert.That(restored.IsValid() && restored.isLoaded && restored.isDirty, Is.True);
        GameObject[] roots = restored.GetRootGameObjects();
        Assert.That(roots, Has.Length.EqualTo(1));
        Assert.That(roots[0].name, Is.EqualTo("Unsaved placed object"));
        Assert.That(roots[0].transform.position, Is.EqualTo(new Vector3(42f, 7f, 0f)));
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }
}
