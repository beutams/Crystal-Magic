using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpriteEffectAnimationAuthoring : MonoBehaviour
{
    public AnimationClip EnterClip;
    public AnimationClip LoopClip;
    public AnimationClip ExitClip;

    private sealed class Baker : Baker<SpriteEffectAnimationAuthoring>
    {
        public override void Bake(SpriteEffectAnimationAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new SpriteEffectAnimationComponent
            {
                EnterClip = authoring.EnterClip,
                LoopClip = authoring.LoopClip,
                ExitClip = authoring.ExitClip,
                Phase = SpriteEffectAnimationPhase.Uninitialized,
                RemainingLoopSeconds = -1f,
            });
        }
    }
}

public enum SpriteEffectAnimationPhase : byte
{
    Uninitialized,
    Enter,
    Loop,
    Exit,
}

public sealed class SpriteEffectAnimationComponent : IComponentData
{
    public AnimationClip EnterClip;
    public AnimationClip LoopClip;
    public AnimationClip ExitClip;
    public AnimationClip CurrentClip;
    public SpriteRenderer Renderer;
    public SpriteEffectAnimationPhase Phase;
    public FixedString64Bytes CurrentAnimationName;
    public float PhaseElapsedSeconds;
    public float CurrentSampleTime;
    public float RemainingLoopSeconds;
    public Sprite CurrentSprite;
    public byte EndRequested;
}

public struct EffectVisualFollowComponent : IComponentData
{
    public Entity Target;
    public float3 Offset;
    public byte AlignRotation;
    public byte EndWhenTargetMissing;
}
