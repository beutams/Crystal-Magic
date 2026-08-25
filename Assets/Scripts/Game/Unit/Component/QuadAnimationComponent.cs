using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public struct QuadAnimationComponent : IComponentData
{
    public int GridColumns;
    public int GridRows;
    public int FrameCount;
    public float FramesPerSecond;
    public float ElapsedSeconds;
    public float Width;
    public float Height;
    public float2 PivotOffset;
    public float RemainingLifetimeSeconds;
    public int FrameIndex;
    public int LastTextureInstanceId;
    public int LastVisualKeyHash;
    public byte Loop;
    public byte AutoDestroyOnComplete;
    public byte IsPlaying;
}

public struct FollowEntityComponent : IComponentData
{
    public Entity Target;
    public float3 Offset;
    public byte AlignRotation;
}

public struct QuadOverlayPulseComponent : IComponentData, IEnableableComponent
{
    public float4 OverlayColor;
    public float DurationSeconds;
    public float RemainingSeconds;
    public float PeakStrength;
}

public sealed class QuadAnimationVisualComponent : IComponentData
{
    public string PrefabName;
    public Texture2D Texture;
}

[MaterialProperty("_FrameUvMin")]
public struct QuadAnimationFrameUvMinProperty : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_FrameUvSize")]
public struct QuadAnimationFrameUvSizeProperty : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_FrameWorldSize")]
public struct QuadAnimationFrameWorldSizeProperty : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_FramePivotOffset")]
public struct QuadAnimationFramePivotOffsetProperty : IComponentData
{
    public float4 Value;
}
