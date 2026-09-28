using System.Collections.Generic;
using Unity.Entities;
using Unity.Scenes.Editor;
using UnityEditor;

public sealed class CrystalMagicEntitySceneBuildAdditions : IEntitySceneBuildAdditions
{
    private const string SubSceneRoot = "Assets/Scenes/SubScene";

    public HashSet<Hash128> RegisterAdditionalEntityScenesToBuild()
    {
        HashSet<Hash128> sceneGuids = new();
        string[] assetGuids = AssetDatabase.FindAssets("t:Scene", new[] { SubSceneRoot });

        for (int index = 0; index < assetGuids.Length; index++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[index]);
            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            sceneGuids.Add(AssetDatabase.GUIDFromAssetPath(assetPath));
        }

        return sceneGuids;
    }
}
