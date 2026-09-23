using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Sprites;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GamePresentationSystemGroup))]
public partial class DropSpritePresentationSystem : SystemBase
{
    private const float SortingPrecision = 100f;
    private const float BorderWidth = 0.065f;
    private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
    private static readonly int BorderWidthId = Shader.PropertyToID("_BorderWidth");
    private static readonly int SpriteUvRectId = Shader.PropertyToID("_SpriteUvRect");

    private readonly Dictionary<string, Sprite> _sprites = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingSprites = new(StringComparer.Ordinal);
    private MaterialPropertyBlock _propertyBlock;

    protected override void OnCreate()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    protected override void OnUpdate()
    {
        if (ResourceComponent.Instance == null ||
            ConfigComponent.Instance == null ||
            DataComponent.Instance == null)
        {
            return;
        }

        EntityCommandBuffer commandBuffer = new(Allocator.Temp);
        foreach ((RefRO<UnitInteractableComponent> interactableRef, Entity entity) in
                 SystemAPI.Query<RefRO<UnitInteractableComponent>>()
                     .WithNone<DropPresentationInitializedTag>()
                     .WithEntityAccess())
        {
            UnitInteractionData interaction = interactableRef.ValueRO.Data;
            if (interaction.Kind != InteractionKind.Drop ||
                !EntityManager.HasComponent<SpriteRenderer>(entity))
            {
                continue;
            }

            ConfigureSprite(EntityManager.GetComponentObject<SpriteRenderer>(entity), interaction);
            commandBuffer.AddComponent<DropPresentationInitializedTag>(entity);
        }

        commandBuffer.Playback(EntityManager);
        commandBuffer.Dispose();

        foreach ((RefRO<LocalTransform> transformRef, Entity entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<DropPresentationInitializedTag>()
                     .WithEntityAccess())
        {
            if (!EntityManager.HasComponent<SpriteRenderer>(entity))
                continue;

            float sortingY = transformRef.ValueRO.Position.y;
            if (EntityManager.HasComponent<DropScatterComponent>(entity))
                sortingY = EntityManager.GetComponentData<DropScatterComponent>(entity).TargetPosition.y;

            SpriteRenderer spriteRenderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
            spriteRenderer.sortingOrder = Mathf.RoundToInt(-sortingY * SortingPrecision);
        }
    }

    protected override void OnDestroy()
    {
        if (ResourceComponent.Instance != null)
        {
            foreach (string path in _sprites.Keys)
                ResourceComponent.Instance.Unload(path);
        }

        _sprites.Clear();
        _missingSprites.Clear();
        _propertyBlock = null;
        base.OnDestroy();
    }

    private void ConfigureSprite(SpriteRenderer spriteRenderer, in UnitInteractionData interaction)
    {
        string iconPath = GetIconPath(interaction);
        Sprite sprite = GetOrLoadSprite(iconPath);
        spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = sprite != null;
        if (sprite == null)
            return;

        Vector4 outerUv = DataUtility.GetOuterUV(sprite);
        _propertyBlock.Clear();
        spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(BorderColorId, GetBorderColor(interaction));
        _propertyBlock.SetFloat(BorderWidthId, BorderWidth);
        _propertyBlock.SetVector(SpriteUvRectId, outerUv);
        spriteRenderer.SetPropertyBlock(_propertyBlock);
    }

    private Sprite GetOrLoadSprite(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (_sprites.TryGetValue(path, out Sprite cached))
            return cached;

        Sprite sprite = ResourceComponent.Instance.LoadSprite(path);
        _sprites[path] = sprite;
        if (sprite == null && _missingSprites.Add(path))
            Debug.LogWarning($"[DropSpritePresentationSystem] Missing drop sprite: {path}");
        return sprite;
    }

    private static string GetIconPath(in UnitInteractionData interaction)
    {
        DropRewardType dropType = (DropRewardType)interaction.Variant;
        if (dropType == DropRewardType.Money)
            return ConfigComponent.Instance.Get<GameConfig>().MoneyIconPath;

        ItemData itemData = DataComponent.Instance.Get<ItemData>(interaction.DataId);
        return itemData?.IconPath;
    }

    private static Color GetBorderColor(in UnitInteractionData interaction)
    {
        DropRewardType dropType = (DropRewardType)interaction.Variant;
        if (dropType == DropRewardType.Money)
            return new Color32(255, 201, 61, 255);

        int rarity = DataComponent.Instance.Get<ItemData>(interaction.DataId)?.Rarity ?? 0;
        return rarity switch
        {
            <= 0 => new Color32(205, 205, 205, 255),
            1 => new Color32(80, 210, 105, 255),
            2 => new Color32(70, 145, 255, 255),
            3 => new Color32(180, 90, 255, 255),
            4 => new Color32(255, 150, 45, 255),
            _ => new Color32(255, 70, 70, 255),
        };
    }
}
