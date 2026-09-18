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

[UnitSourceProvider(typeof(UnitSkillReleaseComponent), typeof(UnitSkillReleaseAuthoring))]
public static class UnitSkillReleaseSource
{
    [UnitSourceGet(0, "unit.self.entity", UnitValueCategory.Entity)]
    public static bool TryGet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        bool valid = operation == 0 && entityManager.Exists(entity) &&
                     entityManager.HasComponent<UnitSkillReleaseComponent>(entity);
        result = valid ? UnitSourceValue.FromEntity(entity) : default;
        return valid;
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
