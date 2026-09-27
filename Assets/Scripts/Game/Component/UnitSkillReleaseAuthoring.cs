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
            AddComponent<UnitSkillReleaseComponent>(entity);
        }
    }
}

public struct UnitSkillReleaseComponent : IComponentData
{
}

// Synchronous input used while resolving a state-script skill command.
public struct SkillReleaseRequest
{
    public int SkillId;
    public Entity OriginEntity;
    public float3 OriginPosition;
    public float2 OriginFacing;
    public Entity TargetEntity;
    public float3 TargetPosition;
    public SkillModifierSet ExtraModifiers;
    public byte HasTargetEntityFlag;
    public byte HasTargetPositionFlag;

    public bool HasTargetEntity
    {
        readonly get => HasTargetEntityFlag != 0;
        set => HasTargetEntityFlag = value ? (byte)1 : (byte)0;
    }

    public bool HasTargetPosition
    {
        readonly get => HasTargetPositionFlag != 0;
        set => HasTargetPositionFlag = value ? (byte)1 : (byte)0;
    }
}
