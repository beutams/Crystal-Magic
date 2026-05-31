using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public static class UnitAnimationVisualUtility
{
    private const string DefaultUnitMaterialPath = "Assets/Res/Material/Unit.mat";

    private static readonly Dictionary<string, Mesh> s_meshCache = new();
    private static readonly Dictionary<string, Material> s_materialCache = new();
    private static readonly HashSet<string> s_loggedMissingVisuals = new();
    private static Material s_defaultUnitMaterial;
    private static bool s_loggedMissingDefaultMaterial;

    public static bool TryResolveAnimatedAtlas(
        in FixedString128Bytes visualKey,
        string atlasTexturePath,
        out Mesh mesh,
        out Material material)
    {
        mesh = null;
        material = null;

        if (string.IsNullOrWhiteSpace(atlasTexturePath))
        {
            return false;
        }

        Texture2D texture = ResourceComponent.Instance.Load<Texture2D>(atlasTexturePath);
        if (texture == null)
        {
            LogMissingVisualOnce($"{visualKey}|{atlasTexturePath}");
            return false;
        }

        string key = visualKey.ToString();
        mesh = GetMesh(key);
        material = GetMaterial(key, texture, atlasTexturePath);
        if (mesh == null || material == null)
            return false;
        return true;
    }

    private static Mesh GetMesh(string visualKey)
    {
        if (s_meshCache.TryGetValue(visualKey, out Mesh cachedMesh) && cachedMesh != null)
            return cachedMesh;

        GameObject prefab = ResourceComponent.Instance.Load<GameObject>(AssetPathHelper.GetUnitPrefabAsset(visualKey));
        Mesh mesh = prefab != null ? prefab.GetComponent<MeshFilter>()?.sharedMesh : null;
        if (mesh == null)
        {
            LogMissingVisualOnce(visualKey);
            return null;
        }

        s_meshCache[visualKey] = mesh;
        return mesh;
    }

    private static Material GetMaterial(string visualKey, Texture2D texture, string atlasTexturePath)
    {
        string key = $"{visualKey}|{atlasTexturePath}";
        if (s_materialCache.TryGetValue(key, out Material cachedMaterial) && cachedMaterial != null)
            return cachedMaterial;

        s_defaultUnitMaterial ??= ResourceComponent.Instance.Load<Material>(DefaultUnitMaterialPath);
        if (s_defaultUnitMaterial == null)
        {
            if (!s_loggedMissingDefaultMaterial)
            {
                s_loggedMissingDefaultMaterial = true;
                Debug.LogWarning($"[UnitAnimationVisualUtility] Could not load default unit material: {DefaultUnitMaterialPath}");
            }

            return null;
        }

        Material material = new Material(s_defaultUnitMaterial);
        material.SetTexture("_BaseMap", texture);
        s_materialCache[key] = material;
        return material;
    }

    private static void LogMissingVisualOnce(string key)
    {
        if (!s_loggedMissingVisuals.Add(key))
            return;

        Debug.LogWarning($"[UnitAnimationVisualUtility] Missing animated visual resource: {key}");
    }
}
