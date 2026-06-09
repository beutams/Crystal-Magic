using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum QuadAnimationVisualKind : byte
{
    Projectile = 0,
    Vfx = 1,
}

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

public sealed class QuadAnimationVisualComponent : IComponentData
{
    public QuadAnimationVisualKind VisualKind;
    public string PrefabName;
    public Texture2D Texture;
}
