using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public struct UnitAnimationComponent : IComponentData
{
    public FixedString128Bytes VisualKey;
    public int ClipId;
    public int FrameIndex;
    public float ElapsedSeconds;
    public float SpeedMultiplier;
    public int RequestedVariant;
    public int LastStateHash;
    public int LastSkillId;
    public int SelectionCursor;
    public int LastDirectionalVariantHash;
    public byte IsCurrentClipFinished;
    public byte IsCurrentClipLooping;

    public static UnitAnimationComponent CreateDefault(in FixedString128Bytes visualKey)
    {
        return new UnitAnimationComponent
        {
            VisualKey = visualKey,
            ClipId = -1,
            FrameIndex = -1,
            ElapsedSeconds = 0f,
            SpeedMultiplier = 1f,
            RequestedVariant = -1,
            LastStateHash = 0,
            LastSkillId = -1,
            SelectionCursor = 0,
            LastDirectionalVariantHash = 0,
            IsCurrentClipFinished = 0,
            IsCurrentClipLooping = 0,
        };
    }
}

[MaterialProperty("_FrameUvMin")]
public struct UnitAnimationFrameUvMinProperty : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_FrameUvSize")]
public struct UnitAnimationFrameUvSizeProperty : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_FrameWorldSize")]
public struct UnitAnimationFrameWorldSizeProperty : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_FramePivotOffset")]
public struct UnitAnimationFramePivotOffsetProperty : IComponentData
{
    public float4 Value;
}
