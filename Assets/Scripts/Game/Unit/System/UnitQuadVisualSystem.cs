using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
partial class UnitQuadVisualSystem : SystemBase
{
    private const string DefaultUnitMaterialPath = "Assets/Res/Material/Unit.mat";

    private static readonly Dictionary<string, VisualSource> s_visualSources = new();
    private static readonly Dictionary<string, Material> s_overrideMaterials = new();
    private static readonly HashSet<string> s_loggedMissingVisuals = new();
    private static Material s_defaultUnitMaterial;
    private static bool s_loggedMissingDefaultMaterial;

    protected override void OnUpdate()
    {
        List<PendingVisualApply> pendingVisuals = null;
        using EntityCommandBuffer ecb = new(Allocator.Temp);
        foreach ((RefRW<UnitQuadVisualRequest> request, Entity entity) in SystemAPI.Query<RefRW<UnitQuadVisualRequest>>().WithEntityAccess())
        {
            if (request.ValueRO.IsApplied != 0)
                continue;

            string visualKey = request.ValueRO.VisualKey.ToString();
            if (string.IsNullOrWhiteSpace(visualKey))
                continue;

            if (!TryGetVisualSource(visualKey, out VisualSource source) ||
                !TryGetOverrideMaterial(visualKey, source.Texture, out Material material))
                continue;

            bool rootApplied = QueueVisualApply(entity, source.Mesh, material, required: true, ref pendingVisuals);
            bool extraApplied = QueueVisualApply(
                request.ValueRO.ExtraVisualEntity,
                source.Mesh,
                material,
                required: false,
                ref pendingVisuals);

            if (rootApplied && extraApplied)
            {
                UnitQuadVisualRequest appliedRequest = request.ValueRO;
                appliedRequest.IsApplied = 1;
                request.ValueRW = appliedRequest;
            }
        }

        if (pendingVisuals != null)
        {
            for (int i = 0; i < pendingVisuals.Count; i++)
                ApplyQueuedVisual(pendingVisuals[i]);
        }

        ecb.Playback(EntityManager);
    }

    private bool QueueVisualApply(
        Entity entity,
        Mesh mesh,
        Material material,
        bool required,
        ref List<PendingVisualApply> pendingVisuals)
    {
        if (entity == Entity.Null || !EntityManager.Exists(entity))
            return !required;

        if (!EntityManager.HasComponent<MaterialMeshInfo>(entity))
            return !required;

        pendingVisuals ??= new List<PendingVisualApply>();
        pendingVisuals.Add(new PendingVisualApply(entity, mesh, material));
        return true;
    }

    private void ApplyQueuedVisual(PendingVisualApply pendingVisual)
    {
        Entity entity = pendingVisual.Entity;
        if (entity == Entity.Null ||
            !EntityManager.Exists(entity) ||
            !EntityManager.HasComponent<MaterialMeshInfo>(entity))
        {
            return;
        }

        EntityManager.SetSharedComponentManaged(
            entity,
            new RenderMeshArray(new[] { pendingVisual.Material }, new[] { pendingVisual.Mesh }));
        EntityManager.SetComponentData(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
    }

    private static bool TryGetVisualSource(string visualKey, out VisualSource source)
    {
        if (s_visualSources.TryGetValue(visualKey, out source) &&
            source.Mesh != null &&
            source.Texture != null)
        {
            return true;
        }

        GameObject prefab = ResourceComponent.Instance.Load<GameObject>(AssetPathHelper.GetUnitPrefabAsset(visualKey));
        Mesh mesh = prefab != null ? prefab.GetComponent<MeshFilter>()?.sharedMesh : null;
        Material sourceMaterial = prefab != null ? prefab.GetComponent<MeshRenderer>()?.sharedMaterial : null;
        Texture texture = null;
        if (sourceMaterial != null)
        {
            if (sourceMaterial.HasProperty("_BaseMap"))
                texture = sourceMaterial.GetTexture("_BaseMap");

            texture ??= sourceMaterial.mainTexture;
        }

        if (mesh == null || texture == null)
        {
            LogMissingVisualOnce(visualKey);
            source = default;
            return false;
        }

        source = new VisualSource(mesh, texture);
        s_visualSources[visualKey] = source;
        return true;
    }

    private static bool TryGetOverrideMaterial(string visualKey, Texture texture, out Material material)
    {
        if (s_overrideMaterials.TryGetValue(visualKey, out material) && material != null)
            return true;

        s_defaultUnitMaterial ??= ResourceComponent.Instance.Load<Material>(DefaultUnitMaterialPath);
        if (s_defaultUnitMaterial == null)
        {
            if (!s_loggedMissingDefaultMaterial)
            {
                s_loggedMissingDefaultMaterial = true;
                Debug.LogWarning($"[UnitQuadVisualSystem] Could not load default unit material: {DefaultUnitMaterialPath}");
            }

            material = null;
            return false;
        }

        material = new Material(s_defaultUnitMaterial);
        material.SetTexture("_BaseMap", texture);
        s_overrideMaterials[visualKey] = material;
        return true;
    }

    private static void LogMissingVisualOnce(string visualKey)
    {
        if (!s_loggedMissingVisuals.Add(visualKey))
            return;

        Debug.LogWarning(
            $"[UnitQuadVisualSystem] Could not resolve quad visual source for '{visualKey}'. " +
            "Make sure the prefab has a MeshFilter, a MeshRenderer, and a texture on _BaseMap.");
    }

    private readonly struct VisualSource
    {
        public VisualSource(Mesh mesh, Texture texture)
        {
            Mesh = mesh;
            Texture = texture;
        }

        public Mesh Mesh { get; }
        public Texture Texture { get; }
    }

    private readonly struct PendingVisualApply
    {
        public PendingVisualApply(Entity entity, Mesh mesh, Material material)
        {
            Entity = entity;
            Mesh = mesh;
            Material = material;
        }

        public Entity Entity { get; }
        public Mesh Mesh { get; }
        public Material Material { get; }
    }
}
