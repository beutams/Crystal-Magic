using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class NPCAuthoringUtility
{
    public static TextAsset GetNpcDataTableAsset()
    {
        string path = AssetPathHelper.GetDataAsset(typeof(NPCData).Name + "Table");
        return EditorComponents.Resource.Load<TextAsset>(path);
    }

    public static NPCData ResolveNpcData(Component component)
    {
        if (component == null)
        {
            return null;
        }

        string prefabPath = GetPrefabAssetPath(component);
        if (!string.IsNullOrWhiteSpace(prefabPath))
        {
            NPCData dataByPath = EditorComponents.Data.Find<NPCData>(row =>
                string.Equals(row.PrefabPath, prefabPath, StringComparison.Ordinal));
            if (dataByPath != null)
            {
                return dataByPath;
            }
        }

        string fallbackName = component.transform.root.name;
        if (string.IsNullOrWhiteSpace(fallbackName))
        {
            return null;
        }

        return EditorComponents.Data.Find<NPCData>(row =>
            string.Equals(row.NPC, fallbackName, StringComparison.Ordinal));
    }

    private static string GetPrefabAssetPath(Component component)
    {
#if UNITY_EDITOR
        if (component == null)
        {
            return null;
        }

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject);
        return string.IsNullOrWhiteSpace(path) ? null : path;
#else
        return null;
#endif
    }
}
