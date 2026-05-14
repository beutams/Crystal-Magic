using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public static class ProjectileVisualUtility
{
    public const string GenericProjectilePrefabName = "Projectile";

    private static readonly Dictionary<string, Mesh> s_projectileMeshes = new();
    private static readonly Dictionary<string, Material> s_projectileBaseMaterials = new();
    private static readonly Dictionary<string, Material> s_overrideMaterials = new();
    private static readonly HashSet<string> s_loggedMissingProjectilePrefabs = new();

    public static void ApplyProjectileVisual(
        EntityManager entityManager,
        Entity entity,
        in FixedString128Bytes projectileName,
        Texture2D texture,
        bool loop,
        int frameCount)
    {
        if (texture == null || !entityManager.HasComponent<MaterialMeshInfo>(entity))
            return;

        string projectileKey = projectileName.ToString();
        Material overrideMaterial = GetOrCreateOverrideMaterial(projectileKey, texture, loop, frameCount);
        Mesh projectileMesh = GetProjectileMesh(projectileKey);
        if (overrideMaterial == null || projectileMesh == null)
            return;

        entityManager.SetSharedComponentManaged(entity, new RenderMeshArray(new[] { overrideMaterial }, new[] { projectileMesh }));
        entityManager.SetComponentData(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
    }

    public static float GetAnimationLifetime(in FixedString128Bytes projectileName, int frameCount)
    {
        Material baseMaterial = GetProjectileBaseMaterial(projectileName.ToString());
        if (baseMaterial == null)
            return 1f;

        float clampedFrameCount = Mathf.Clamp(frameCount, 1, 16);
        float fps = baseMaterial.HasProperty("_FPS") ? baseMaterial.GetFloat("_FPS") : 16f;
        return Mathf.Max(clampedFrameCount / Mathf.Max(fps, 0.01f), 0.05f);
    }

    private static Material GetOrCreateOverrideMaterial(string projectileName, Texture2D texture, bool loop, int frameCount)
    {
        int clampedFrameCount = Mathf.Clamp(frameCount, 1, 16);
        string key = $"{projectileName}|{texture.GetInstanceID()}|{(loop ? 1 : 0)}|{clampedFrameCount}";
        if (s_overrideMaterials.TryGetValue(key, out Material cachedMaterial) && cachedMaterial != null)
            return cachedMaterial;

        Material baseMaterial = GetProjectileBaseMaterial(projectileName);
        if (baseMaterial == null)
            return null;

        Material overrideMaterial = new Material(baseMaterial);
        overrideMaterial.SetTexture("_BaseMap", texture);
        if (overrideMaterial.HasProperty("_FrameCount"))
            overrideMaterial.SetFloat("_FrameCount", clampedFrameCount);
        if (overrideMaterial.HasProperty("_Loop"))
            overrideMaterial.SetFloat("_Loop", loop ? 1f : 0f);

        s_overrideMaterials[key] = overrideMaterial;
        return overrideMaterial;
    }

    private static Mesh GetProjectileMesh(string projectileName)
    {
        if (s_projectileMeshes.TryGetValue(projectileName, out Mesh mesh) && mesh != null)
            return mesh;

        GameObject projectilePrefab = ResourceComponent.Instance.Load<GameObject>(AssetPathHelper.GetProjectilePrefabAsset(projectileName));
        mesh = projectilePrefab != null ? projectilePrefab.GetComponent<MeshFilter>()?.sharedMesh : null;
        if (mesh != null)
            s_projectileMeshes[projectileName] = mesh;
        else
            LogMissingProjectilePrefabOnce(projectileName);

        return mesh;
    }

    private static Material GetProjectileBaseMaterial(string projectileName)
    {
        if (s_projectileBaseMaterials.TryGetValue(projectileName, out Material material) && material != null)
            return material;

        GameObject projectilePrefab = ResourceComponent.Instance.Load<GameObject>(AssetPathHelper.GetProjectilePrefabAsset(projectileName));
        material = projectilePrefab != null ? projectilePrefab.GetComponent<MeshRenderer>()?.sharedMaterial : null;
        if (material != null)
            s_projectileBaseMaterials[projectileName] = material;
        else
            LogMissingProjectilePrefabOnce(projectileName);

        return material;
    }

    private static void LogMissingProjectilePrefabOnce(string projectileName)
    {
        if (!s_loggedMissingProjectilePrefabs.Add(projectileName))
            return;

        Debug.LogWarning(
            $"[ProjectileVisualUtility] Could not load projectile prefab '{projectileName}'. " +
            "Make sure the prefab exists under the projectile prefab directory and is registered for ECS spawning.");
    }
}
