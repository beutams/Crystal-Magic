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

// This is a release-time snapshot. The execution layer never reads the caster's unit components again.
public sealed class SkillReleaseRequest
{
    public int SkillId = -1;
    public int SkillAdditionId = -1;
    public Entity OriginEntity = Entity.Null;
    public float3 OriginPosition;
    public float2 OriginFacing = new(1f, 0f);
    public bool HasTargetEntity;
    public Entity TargetEntity = Entity.Null;
    public bool HasTargetPosition;
    public float3 TargetPosition;
    public bool HasAttackSnapshot;
    public UnitAttackComponent AttackSnapshot;
    public bool HasElementSnapshot;
    public UnitElementComponent ElementSnapshot;
    public SkillModifierSet ModifierSnapshot = new();
}
