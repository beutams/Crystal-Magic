using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitJumpArcAuthoring : MonoBehaviour
{
    private sealed class UnitJumpArcBaker : Baker<UnitJumpArcAuthoring>
    {
        public override void Bake(UnitJumpArcAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitJumpArcComponent
            {
                StartPosition = float3.zero,
                EndPosition = float3.zero,
                Duration = 0f,
                Elapsed = 0f,
                ArcHeight = 0f,
                IsActive = 0,
                IsCompleted = 1,
            });
        }
    }
}

public struct UnitJumpArcComponent : IComponentData
{
    public float3 StartPosition;
    public float3 EndPosition;
    public float Duration;
    public float Elapsed;
    public float ArcHeight;
    public byte IsActive;
    public byte IsCompleted;
}

[UnitSourceAuthoring(typeof(UnitJumpArcAuthoring))]
public sealed class UnitJumpArcSource : UnitComponentSource<UnitJumpArcComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitJumpArcComponent> builder)
    {
        builder.AddGet("unit.jump.startPosition", UnitValueCategory.Float3,
            (in UnitJumpArcComponent value) => UnitValue.FromFloat3(value.StartPosition));
        builder.AddGet("unit.jump.endPosition", UnitValueCategory.Float3,
            (in UnitJumpArcComponent value) => UnitValue.FromFloat3(value.EndPosition));
        builder.AddGet("unit.jump.duration", UnitValueCategory.Number,
            (in UnitJumpArcComponent value) => UnitValue.FromFloat(value.Duration));
        builder.AddGet("unit.jump.elapsed", UnitValueCategory.Number,
            (in UnitJumpArcComponent value) => UnitValue.FromFloat(value.Elapsed));
        builder.AddGet("unit.jump.arcHeight", UnitValueCategory.Number,
            (in UnitJumpArcComponent value) => UnitValue.FromFloat(value.ArcHeight));
        builder.AddGet("unit.jump.isActive", UnitValueCategory.Bool,
            (in UnitJumpArcComponent value) => UnitValue.FromBool(value.IsActive != 0));
        builder.AddGet("unit.jump.isCompleted", UnitValueCategory.Bool,
            (in UnitJumpArcComponent value) => UnitValue.FromBool(value.IsCompleted != 0));
        builder.AddGet("unit.jump.progress", UnitValueCategory.Number,
            (in UnitJumpArcComponent value) => UnitValue.FromFloat(value.Duration > 0f ? math.saturate(value.Elapsed / value.Duration) : 0f));
    }
}
