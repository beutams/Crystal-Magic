using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class UnitAuthoringUtility
{
    public static TextAsset GetUnitDataTableAsset()
    {
        string path = AssetPathHelper.GetDataAsset(typeof(UnitData).Name + "Table");
        return EditorComponents.Resource.Load<TextAsset>(path);
    }

    public static UnitData ResolveUnitData(Component component)
    {
        if (component == null)
        {
            return null;
        }

        string prefabPath = GetPrefabAssetPath(component);
        if (!string.IsNullOrWhiteSpace(prefabPath))
        {
            UnitData dataByPath = EditorComponents.Data.Find<UnitData>(r => string.Equals(r.PrefabPath, prefabPath, StringComparison.Ordinal));
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

        return EditorComponents.Data.Find<UnitData>(r => r.Name == fallbackName);
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
