using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSystem))]
[UpdateBefore(typeof(DestroyEntitySystem))]
public partial class QuadAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<QuadAnimationComponent> animation, RefRW<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRW<QuadAnimationComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            UpdateFollowTarget(entity, ref transform.ValueRW);
            ApplyVisualIfNeeded(entity, ref animation.ValueRW);
            AdvanceAnimation(entity, deltaTime, ref animation.ValueRW);
        }
    }

    private void UpdateFollowTarget(Entity entity, ref LocalTransform transform)
    {
        if (!EntityManager.HasComponent<FollowEntityComponent>(entity))
            return;

        FollowEntityComponent follow = EntityManager.GetComponentData<FollowEntityComponent>(entity);
        if (follow.Target == Entity.Null ||
            !EntityManager.Exists(follow.Target) ||
            !EntityManager.HasComponent<LocalTransform>(follow.Target))
        {
            return;
        }

        LocalTransform targetTransform = EntityManager.GetComponentData<LocalTransform>(follow.Target);
        quaternion rotation = transform.Rotation;
        if (follow.AlignRotation != 0)
            rotation = targetTransform.Rotation;

        transform.Position = targetTransform.Position + math.rotate(rotation, follow.Offset);
        if (follow.AlignRotation != 0)
            transform.Rotation = rotation;
    }

    private void ApplyVisualIfNeeded(Entity entity, ref QuadAnimationComponent animation)
    {
        if (!EntityManager.HasComponent<QuadAnimationVisualComponent>(entity))
            return;

        QuadAnimationVisualComponent visual = EntityManager.GetComponentObject<QuadAnimationVisualComponent>(entity);
        if (visual?.Texture == null)
            return;

        int textureInstanceId = visual.Texture.GetInstanceID();
        int visualKeyHash = QuadAnimationVisualUtility.GetVisualKeyHash(visual.VisualKind, visual.PrefabName);
        if (animation.LastTextureInstanceId == textureInstanceId &&
            animation.LastVisualKeyHash == visualKeyHash)
        {
            return;
        }

        if (!QuadAnimationVisualUtility.ApplyVisual(EntityManager, entity, visual.VisualKind, visual.PrefabName, visual.Texture))
            return;

        animation.LastTextureInstanceId = textureInstanceId;
        animation.LastVisualKeyHash = visualKeyHash;
    }

    private void AdvanceAnimation(Entity entity, float deltaTime, ref QuadAnimationComponent animation)
    {
        int gridColumns = math.max(1, animation.GridColumns);
        int gridRows = math.max(1, animation.GridRows);
        int frameCount = math.clamp(animation.FrameCount, 1, gridColumns * gridRows);
        float fps = math.max(0.01f, animation.FramesPerSecond);

        if (animation.IsPlaying != 0)
            animation.ElapsedSeconds += deltaTime;

        if (animation.RemainingLifetimeSeconds > 0f)
        {
            animation.RemainingLifetimeSeconds = math.max(0f, animation.RemainingLifetimeSeconds - deltaTime);
            if (animation.RemainingLifetimeSeconds <= 0f)
            {
                animation.IsPlaying = 0;
                TriggerCompletion(entity, ref animation);
                return;
            }
        }

        int nextFrameIndex = ResolveFrameIndex(frameCount, fps, animation.ElapsedSeconds, animation.Loop != 0);
        if (nextFrameIndex != animation.FrameIndex)
        {
            animation.FrameIndex = nextFrameIndex;
            ApplyFrameProperties(entity, gridColumns, gridRows, nextFrameIndex, animation.Width, animation.Height, animation.PivotOffset);
        }

        if (animation.IsPlaying == 0 || animation.Loop != 0)
            return;

        float duration = frameCount / fps;
        if (animation.ElapsedSeconds < duration)
            return;

        animation.IsPlaying = 0;
        TriggerCompletion(entity, ref animation);
    }

    private void TriggerCompletion(Entity entity, ref QuadAnimationComponent animation)
    {
        if (animation.AutoDestroyOnComplete == 0)
            return;

        if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
            EntityManager.AddComponent<DestroyEntityFlag>(entity);

        EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
    }

    private void ApplyFrameProperties(
        Entity entity,
        int gridColumns,
        int gridRows,
        int frameIndex,
        float width,
        float height,
        float2 pivotOffset)
    {
        float uvWidth = 1f / gridColumns;
        float uvHeight = 1f / gridRows;
        int clampedFrameIndex = math.clamp(frameIndex, 0, (gridColumns * gridRows) - 1);
        int col = clampedFrameIndex % gridColumns;
        int rowTop = clampedFrameIndex / gridColumns;
        int row = (gridRows - 1) - rowTop;
        float uvMinX = col * uvWidth;
        float uvMinY = row * uvHeight;

        SetOrAddProperty(entity, new UnitAnimationFrameUvMinProperty
        {
            Value = new float4(uvMinX, uvMinY, 0f, 0f),
        });
        SetOrAddProperty(entity, new UnitAnimationFrameUvSizeProperty
        {
            Value = new float4(uvWidth, uvHeight, 0f, 0f),
        });
        SetOrAddProperty(entity, new UnitAnimationFrameWorldSizeProperty
        {
            Value = new float4(math.max(0.01f, width), math.max(0.01f, height), 0f, 0f),
        });
        SetOrAddProperty(entity, new UnitAnimationFramePivotOffsetProperty
        {
            Value = new float4(pivotOffset.x, pivotOffset.y, 0f, 0f),
        });
    }

    private static int ResolveFrameIndex(int frameCount, float fps, float elapsedSeconds, bool loop)
    {
        int rawIndex = (int)math.floor(math.max(0f, elapsedSeconds) * fps);
        if (loop)
            return rawIndex % frameCount;

        return math.clamp(rawIndex, 0, frameCount - 1);
    }

    private void SetOrAddProperty<T>(Entity entity, T value)
        where T : unmanaged, IComponentData
    {
        if (EntityManager.HasComponent<T>(entity))
            EntityManager.SetComponentData(entity, value);
        else
            EntityManager.AddComponentData(entity, value);
    }
}
