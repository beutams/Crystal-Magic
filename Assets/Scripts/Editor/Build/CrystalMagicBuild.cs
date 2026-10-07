using System;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Editor.Resource;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CrystalMagicBuild
{
    private const string StartScene = "Assets/Scenes/Start.unity";
    private const string BuildRoot = "Build";
    private const BuildTarget RuntimeBuildTarget = BuildTarget.StandaloneWindows64;
    private const string RuntimeResourceRoot = "Assets/Res";

    [MenuItem("Crystal Magic/Build/Build All Windows")]
    public static void BuildAllWindows()
    {
        BuildRuntimeAssetBundles();
        Build("Client", "CrystalMagicClient", "CRYSTAL_MAGIC_CLIENT", false);
        Build("LobbyServer", "CrystalMagicLobbyServer", "CRYSTAL_MAGIC_LOBBY_SERVER", true);
    }

    [MenuItem("Crystal Magic/Build/Build Client")]
    public static void BuildClient()
    {
        BuildRuntimeAssetBundles();
        Build("Client", "CrystalMagicClient", "CRYSTAL_MAGIC_CLIENT", false);
    }

    [MenuItem("Crystal Magic/Build/Build Test Client (Development)")]
    public static void BuildTestClient()
    {
        BuildRuntimeAssetBundles();
        Build("TestClient", "CrystalMagicTestClient", "CRYSTAL_MAGIC_CLIENT", false, true);
    }

    [MenuItem("Crystal Magic/Build/Build Lobby Server")]
    public static void BuildLobbyServer()
    {
        BuildRuntimeAssetBundles();
        Build("LobbyServer", "CrystalMagicLobbyServer", "CRYSTAL_MAGIC_LOBBY_SERVER", true);
    }

    private static void BuildRuntimeAssetBundles()
    {
        BundleBuildConfigData config = BundleBuildUtility.LoadConfig();
        bool configChanged = false;

        if (!string.Equals(config.OutputRootFolder, "Assets/StreamingAssets/AssetBundles", StringComparison.OrdinalIgnoreCase))
        {
            config.OutputRootFolder = "Assets/StreamingAssets/AssetBundles";
            configChanged = true;
        }

        if (config.BuildTarget != RuntimeBuildTarget)
        {
            config.BuildTarget = RuntimeBuildTarget;
            configChanged = true;
        }

        config.Rules ??= new System.Collections.Generic.List<BundleBuildRuleData>();
        bool hasCompleteRuntimeRule = false;
        for (int i = 0; i < config.Rules.Count; i++)
        {
            BundleBuildRuleData rule = config.Rules[i];
            if (rule == null || !rule.Enabled)
                continue;

            string folderPath = AssetBundlePlatformUtility.NormalizeAssetPath(rule.FolderPath);
            if (string.Equals(folderPath, RuntimeResourceRoot, StringComparison.OrdinalIgnoreCase)
                && rule.IncludeSubfolders
                && !string.IsNullOrWhiteSpace(rule.BundleName))
            {
                hasCompleteRuntimeRule = true;
                break;
            }
        }

        if (!hasCompleteRuntimeRule)
        {
            config.Rules.Add(new BundleBuildRuleData
            {
                FolderPath = RuntimeResourceRoot,
                BundleName = "res",
                PackingMode = BundlePackingMode.SingleBundle,
                IncludeSubfolders = true,
            });
            configChanged = true;
        }

        if (configChanged || !File.Exists(BundleBuildUtility.ConfigPath))
            BundleBuildUtility.SaveConfig(config);

        if (!BundleBuildUtility.Build(config))
        {
            throw new BuildFailedException("Failed to build runtime AssetBundles.");
        }

        EnsureRuntimeShaders();
    }

    private static void EnsureRuntimeShaders()
    {
        UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (settingsAssets == null || settingsAssets.Length == 0)
        {
            Debug.LogWarning("[CrystalMagicBuild] Could not open ProjectSettings/GraphicsSettings.asset.");
            return;
        }

        SerializedObject settings = new(settingsAssets[0]);
        SerializedProperty alwaysIncludedShaders = settings.FindProperty("m_AlwaysIncludedShaders");
        if (alwaysIncludedShaders == null || !alwaysIncludedShaders.isArray)
        {
            Debug.LogWarning("[CrystalMagicBuild] GraphicsSettings.m_AlwaysIncludedShaders is unavailable.");
            return;
        }

        string[] shaderGuids = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Dependency/TextMesh Pro/Shaders" });
        int addedShaderCount = 0;

        for (int i = 0; i < shaderGuids.Length; i++)
        {
            string shaderPath = AssetDatabase.GUIDToAssetPath(shaderGuids[i]);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null || ContainsShader(alwaysIncludedShaders, shader))
                continue;

            int index = alwaysIncludedShaders.arraySize;
            alwaysIncludedShaders.InsertArrayElementAtIndex(index);
            alwaysIncludedShaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            addedShaderCount++;
        }

        if (addedShaderCount == 0)
            return;

        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrystalMagicBuild] Added {addedShaderCount} TextMesh Pro shaders to player build settings.");
    }

    private static bool ContainsShader(SerializedProperty shaders, Shader target)
    {
        for (int i = 0; i < shaders.arraySize; i++)
        {
            if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == target)
                return true;
        }

        return false;
    }

    private static void Build(string folderName, string fileName, string define, bool dedicatedServer, bool development = false)
    {
        string outputFolder = Path.Combine(BuildRoot, folderName);
        Directory.CreateDirectory(outputFolder);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { StartScene },
            locationPathName = Path.Combine(outputFolder, fileName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            subtarget = dedicatedServer
                ? (int)StandaloneBuildSubtarget.Server
                : (int)StandaloneBuildSubtarget.Player,
            extraScriptingDefines = string.IsNullOrEmpty(define) ? Array.Empty<string>() : new[] { define },
            options = development ? BuildOptions.Development : BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Failed to build {fileName}: {report.summary.result}");
        }
    }
}
