using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public static class QuadAnimationVisualUtility
{
    public const string GenericProjectilePrefabName = "Projectile";
    public const string GenericVfxPrefabName = "VFX";

    private static readonly Dictionary<string, VisualSource> s_visualSources = new();
    private static readonly Dictionary<string, Material> s_overrideMaterials = new();
    private static readonly HashSet<string> s_loggedMissingVisuals = new();

    public static bool TryResolveVisual(
        QuadAnimationVisualKind visualKind,
        string prefabName,
        Texture2D texture,
        out Mesh mesh,
        out Material material)
    {
        mesh = null;
        material = null;

        if (texture == null)
            return false;

        string visualKey = GetVisualKey(visualKind, prefabName);
        if (!TryGetVisualSource(visualKind, prefabName, out VisualSource source))
            return false;

        material = GetOrCreateOverrideMaterial(visualKey, source.BaseMaterial, texture);
        if (material == null || source.Mesh == null)
            return false;

        mesh = source.Mesh;
        return true;
    }

    public static int GetVisualKeyHash(QuadAnimationVisualKind visualKind, string prefabName)
    {
        return string.IsNullOrWhiteSpace(prefabName)
            ? 0
            : System.StringComparer.Ordinal.GetHashCode(GetVisualKey(visualKind, prefabName));
    }

    private static Material GetOrCreateOverrideMaterial(string visualKey, Material baseMaterial, Texture2D texture)
    {
        if (baseMaterial == null || texture == null)
            return null;

        string overrideKey = $"{visualKey}|{texture.GetInstanceID()}";
        if (s_overrideMaterials.TryGetValue(overrideKey, out Material cachedMaterial) && cachedMaterial != null)
            return cachedMaterial;

        Material overrideMaterial = new Material(baseMaterial);
        overrideMaterial.SetTexture("_BaseMap", texture);
        s_overrideMaterials[overrideKey] = overrideMaterial;
        return overrideMaterial;
    }

    private static bool TryGetVisualSource(
        QuadAnimationVisualKind visualKind,
        string prefabName,
        out VisualSource source)
    {
        string visualKey = GetVisualKey(visualKind, prefabName);
        if (s_visualSources.TryGetValue(visualKey, out source) &&
            source.Mesh != null &&
            source.BaseMaterial != null)
        {
            return true;
        }

        GameObject prefab = ResourceComponent.Instance.Load<GameObject>(ResolvePrefabAssetPath(visualKind, prefabName));
        Mesh mesh = prefab != null ? prefab.GetComponent<MeshFilter>()?.sharedMesh : null;
        Material material = prefab != null ? prefab.GetComponent<MeshRenderer>()?.sharedMaterial : null;
        if (mesh == null || material == null)
        {
            LogMissingVisualOnce(visualKind, prefabName);
            source = default;
            return false;
        }

        source = new VisualSource(mesh, material);
        s_visualSources[visualKey] = source;
        return true;
    }

    private static string ResolvePrefabAssetPath(QuadAnimationVisualKind visualKind, string prefabName)
    {
        return visualKind switch
        {
            QuadAnimationVisualKind.Vfx => AssetPathHelper.GetVfxPrefabAsset(prefabName),
            _ => AssetPathHelper.GetProjectilePrefabAsset(prefabName),
        };
    }

    private static string GetVisualKey(QuadAnimationVisualKind visualKind, string prefabName)
    {
        return $"{(int)visualKind}|{prefabName}";
    }

    private static void LogMissingVisualOnce(QuadAnimationVisualKind visualKind, string prefabName)
    {
        string key = GetVisualKey(visualKind, prefabName);
        if (!s_loggedMissingVisuals.Add(key))
            return;

        Debug.LogWarning(
            $"[QuadAnimationVisualUtility] Could not resolve visual source for '{prefabName}' ({visualKind}). " +
            "Make sure the prefab exists and has a MeshFilter and MeshRenderer.");
    }

    private readonly struct VisualSource
    {
        public VisualSource(Mesh mesh, Material baseMaterial)
        {
            Mesh = mesh;
            BaseMaterial = baseMaterial;
        }

        public Mesh Mesh { get; }
        public Material BaseMaterial { get; }
    }
}
