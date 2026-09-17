using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class CrystalMagicBuild
{
    private const string StartScene = "Assets/Scenes/Start.unity";
    private const string BuildRoot = "Build";

    [MenuItem("Crystal Magic/Build/Build All Windows")]
    public static void BuildAllWindows()
    {
        BuildClient();
        BuildLobbyServer();
        BuildBattleServer();
    }

    [MenuItem("Crystal Magic/Build/Build Client")]
    public static void BuildClient()
    {
        Build("Client", "CrystalMagicClient", "CRYSTAL_MAGIC_CLIENT", false);
    }

    [MenuItem("Crystal Magic/Build/Build Lobby Server")]
    public static void BuildLobbyServer()
    {
        Build("LobbyServer", "CrystalMagicLobbyServer", "CRYSTAL_MAGIC_LOBBY_SERVER", true);
    }

    [MenuItem("Crystal Magic/Build/Build Battle Server")]
    public static void BuildBattleServer()
    {
        Build("BattleServer", "CrystalMagicBattleServer", "CRYSTAL_MAGIC_BATTLE_SERVER", true);
    }

    private static void Build(string folderName, string fileName, string define, bool dedicatedServer)
    {
        string outputFolder = Path.Combine(BuildRoot, folderName);
        Directory.CreateDirectory(outputFolder);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { StartScene },
            locationPathName = Path.Combine(outputFolder, fileName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            subtarget = dedicatedServer ? (int)StandaloneBuildSubtarget.Server : 0,
            extraScriptingDefines = string.IsNullOrEmpty(define) ? Array.Empty<string>() : new[] { define },
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Failed to build {fileName}: {report.summary.result}");
        }
    }
}
