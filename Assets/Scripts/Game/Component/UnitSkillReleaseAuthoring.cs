using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class UnitSkillReleaseAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitSkillReleaseAuthoring>
    {
        public override void Bake(UnitSkillReleaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitSkillReleaseComponent());
        }
    }
}

public sealed class UnitSkillReleaseComponent : IComponentData
{
    public List<SkillReleaseRequest> PendingRequests = new();
}

[UnitSourceAuthoring(typeof(UnitSkillReleaseAuthoring))]
public sealed class UnitSkillReleaseSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = Array.Empty<ComparatorParameterDefinition>();

    public override Type ComponentType => typeof(UnitSkillReleaseComponent);

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("unit.self.entity", ComponentType, UnitValueCategory.Entity, s_noParameters);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitSkillReleaseComponent>(entity))
            return;

        table.AddGet(new UnitSourceGet(
            "unit.self.entity",
            UnitValueCategory.Entity,
            s_noParameters,
            _ => entityManager.Exists(entity) && entityManager.HasComponent<UnitSkillReleaseComponent>(entity)
                ? UnitValue.FromEntity(entity)
                : UnitValue.None));
    }
}

// This is a raw release request. SkillReleaseSystem creates the immutable release snapshot.
public sealed class SkillReleaseRequest
{
    public int SkillId = -1;
    public Entity OriginEntity = Entity.Null;
    public float3 OriginPosition;
    public float2 OriginFacing = new(1f, 0f);
    public bool HasTargetEntity;
    public Entity TargetEntity = Entity.Null;
    public bool HasTargetPosition;
    public float3 TargetPosition;
    public SkillModifierSet ExtraModifiers = new();
}
