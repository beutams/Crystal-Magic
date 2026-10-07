using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Entities;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SubSceneImportWarmupTests
{
    private static readonly string[] SceneNames = { "TrainingSubScene", "TownSubScene", "DungeonRegistrySubScene" };

    [TestCase(false)]
    [TestCase(true)]
    public void CancelledSceneMetadataDoesNotRemainPending(bool destroyMetadata)
    {
        using var warmup = new SubSceneImportWarmup();
        var guid = new Unity.Entities.Hash128(Guid.NewGuid().ToString("N"));
        warmup.Request(guid, "Cancelled scene");
        Entity scene = SceneSystem.GetSceneEntity(warmup.World.Unmanaged, guid);
        if (destroyMetadata)
            SceneSystem.UnloadScene(warmup.World.Unmanaged, scene, SceneSystem.UnloadParameters.DestroyMetaEntities);
        else
            warmup.World.EntityManager.RemoveComponent<RequestSceneLoaded>(scene);

        warmup.Tick();

        Assert.That(warmup.IsPending(guid), Is.False);
        Assert.That(warmup.IsComplete, Is.True, "Cancellation must complete without waiting for the import timeout.");
    }

    [UnityTest]
    public IEnumerator ForegroundSceneLoadsWithPendingMenuImportAndWithoutPlayerLoop()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        FieldInfo gameWorldField = typeof(GameWorldManager).GetField("_gameWorld", flags);
        FieldInfo playerLoopField = typeof(GameWorldManager).GetField("_appendedToPlayerLoop", flags);
        object originalGameWorld = gameWorldField.GetValue(null);
        object originalPlayerLoop = playerLoopField.GetValue(null);
        World originalDefault = World.DefaultGameObjectInjectionWorld;
        float originalTimeScale = Time.timeScale;
        string scenePath = "Assets/Tests/TownLoadHandoff_" + Guid.NewGuid().ToString("N") + ".unity";
        GameObject componentRoot = null;
        GameObject subSceneRoot = null;
        SubSceneImportWarmup warmup = null;
        using World gameWorld = new("Foreground scene loading test", WorldFlags.Game);
        try
        {
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(gameWorld,
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Streaming));
            gameWorldField.SetValue(null, gameWorld);
            playerLoopField.SetValue(null, false);
            World.DefaultGameObjectInjectionWorld = gameWorld;

            CreateLoadProbeScene(scenePath);

            subSceneRoot = new GameObject(TownState.SubSceneName);
            subSceneRoot.SetActive(false);
            SubScene subScene = subSceneRoot.AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            componentRoot = new GameObject("Foreground scene loader");
            SceneComponent loader = componentRoot.AddComponent<SceneComponent>();
            loader.enabled = false;
            warmup = new SubSceneImportWarmup();
            World importWorld = warmup.World;
            warmup.Request(subScene.SceneGUID, TownState.SubSceneName);
            typeof(SceneComponent).GetField("_importWarmup", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(loader, warmup);
            Assert.That(warmup.IsComplete, Is.False);

            // A transition pauses gameplay; the loader must still drive scene streaming.
            Time.timeScale = 0f;
            SceneLoadTiming.Begin("Foreground scene load regression");
            IEnumerator activate = loader.SetSubScenesActiveCoroutine(new[] { TownState.SubSceneName });
            while (activate.MoveNext())
                yield return activate.Current;
            Assert.That(importWorld.IsCreated, Is.False,
                "The menu World must be disposed before enabling a gameplay SubScene in all Worlds.");

            Entity scene = SceneSystem.GetSceneEntity(gameWorld.Unmanaged, subScene.SceneGUID);
            Assert.That(gameWorld.EntityManager.GetComponentData<RequestSceneLoaded>(scene).LoadFlags &
                SceneLoadFlags.BlockOnImport, Is.EqualTo(SceneLoadFlags.BlockOnImport),
                "Foreground registration must keep Unity's editor import flag without test-side overrides.");

            IEnumerator load = loader.WaitForSubSceneLoadedCoroutine(TownState.SubSceneName);
            double deadline = EditorApplication.timeSinceStartup + SubSceneImportWarmup.TimeoutSeconds + 15;
            while (load.MoveNext())
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                    "Foreground loading must make progress instead of waiting on the menu import.");
                yield return load.Current;
            }
            Assert.That(loader.IsSubSceneLoaded(TownState.SubSceneName), Is.True,
                SubSceneLoadTiming.DescribeScene(gameWorld, scene));
            using EntityQuery contents = gameWorld.EntityManager.CreateEntityQuery(typeof(SkillProjectileComponent));
            Assert.That(contents.CalculateEntityCount(), Is.EqualTo(1), "Scene content must load exactly once.");
        }
        finally
        {
            SceneLoadTiming.Finish();
            Time.timeScale = originalTimeScale;
            if (subSceneRoot != null) UnityEngine.Object.DestroyImmediate(subSceneRoot);
            if (componentRoot != null) UnityEngine.Object.DestroyImmediate(componentRoot);
            warmup?.Dispose();
            AssetDatabase.DeleteAsset(scenePath);
            gameWorldField.SetValue(null, originalGameWorld);
            playerLoopField.SetValue(null, originalPlayerLoop);
            World.DefaultGameObjectInjectionWorld = originalDefault;
        }
    }

    private static void CreateLoadProbeScene(string path)
    {
        // Import a tiny scene without replacing or saving the user's open scenes.
        string scriptGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Game/Component/SkillProjectileAuthoring.cs");
        Assert.That(scriptGuid, Is.Not.Empty);
        const string scene = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {fileID: 2}
  - component: {fileID: 3}
  m_Layer: 0
  m_Name: Scene content probe
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &2
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 1}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
--- !u!114 &3
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 1}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: PROJECTILE_SCRIPT_GUID, type: 3}
  m_Name:
  m_EditorClassIdentifier:
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_Roots:
  - {fileID: 2}
";
        File.WriteAllText(path, scene.Replace("PROJECTILE_SCRIPT_GUID", scriptGuid), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    [Test]
    public void DuplicateRequestsStayInAnIsolatedWorldAndDisposalReleasesIt()
    {
        World originalDefault = World.DefaultGameObjectInjectionWorld;
        World originalGame = GameWorldManager.GameWorld;
        var warmup = new SubSceneImportWarmup();
        World warmupWorld = warmup.World;
        try
        {
            Unity.Entities.Hash128 guid = GetGuid(SceneNames[0]);
            warmup.Request(guid, SceneNames[0]);
            warmup.Request(guid, SceneNames[0]);
            warmup.Request(default, "Invalid");
            using EntityQuery query = warmupWorld.EntityManager.CreateEntityQuery(typeof(SceneReference));
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(1));
            Entity scene = query.GetSingletonEntity();
            Assert.That(warmupWorld.EntityManager.GetComponentData<RequestSceneLoaded>(scene).LoadFlags,
                Is.EqualTo(SceneLoadFlags.DisableAutoLoad));
            Assert.That(World.DefaultGameObjectInjectionWorld, Is.SameAs(originalDefault));
            Assert.That(GameWorldManager.GameWorld, Is.SameAs(originalGame));
        }
        finally
        {
            warmup.Dispose();
        }
        Assert.That(warmupWorld.IsCreated, Is.False);
        Assert.That(warmup.IsComplete, Is.True);
    }

    [UnityTest]
    public IEnumerator PrewarmsGameSubScenesWithoutLoadingGameplayContents()
    {
        World originalDefault = World.DefaultGameObjectInjectionWorld;
        World originalGame = GameWorldManager.GameWorld;
        using var warmup = new SubSceneImportWarmup();
        foreach (string name in SceneNames)
            warmup.Request(GetGuid(name), name);

        double deadline = Time.realtimeSinceStartupAsDouble + SubSceneImportWarmup.TimeoutSeconds + 5;
        while (!warmup.IsComplete && Time.realtimeSinceStartupAsDouble < deadline)
        {
            warmup.Tick();
            yield return null;
        }
        Assert.That(warmup.IsComplete, Is.True);
        foreach (string name in SceneNames)
        {
            Entity scene = SceneSystem.GetSceneEntity(warmup.World.Unmanaged, GetGuid(name));
            Assert.That(warmup.World.EntityManager.HasBuffer<ResolvedSectionEntity>(scene), Is.True, name);
            var sections = warmup.World.EntityManager.GetBuffer<ResolvedSectionEntity>(scene, true);
            Assert.That(sections.Length, Is.GreaterThan(0), name);
            foreach (ResolvedSectionEntity section in sections)
                Assert.That(warmup.World.EntityManager.HasComponent<RequestSceneLoaded>(section.SectionEntity), Is.False,
                    name + " must resolve metadata without requesting gameplay content.");
        }
        using EntityQuery content = warmup.World.EntityManager.CreateEntityQuery(typeof(SceneSection));
        Assert.That(content.CalculateEntityCount(), Is.Zero);
        Assert.That(World.DefaultGameObjectInjectionWorld, Is.SameAs(originalDefault));
        Assert.That(GameWorldManager.GameWorld, Is.SameAs(originalGame));
    }

    private static Unity.Entities.Hash128 GetGuid(string sceneName)
    {
        string guid = AssetDatabase.AssetPathToGUID($"Assets/Scenes/SubScene/{sceneName}.unity");
        Assert.That(guid, Is.Not.Empty, sceneName);
        return new Unity.Entities.Hash128(guid);
    }
}
