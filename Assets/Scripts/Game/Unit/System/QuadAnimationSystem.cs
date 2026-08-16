using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(DestroyEntitySystem))]
public partial class QuadAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        List<PendingVisualApply> pendingVisualApplies = null;
        List<PendingFramePropertiesApply> pendingFramePropertyApplies = null;
        List<Entity> pendingDestroyFlags = null;

        foreach ((RefRW<QuadAnimationComponent> animation, RefRW<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRW<QuadAnimationComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            UpdateFollowTarget(entity, ref transform.ValueRW);
            QueueVisualApplyIfNeeded(entity, ref animation.ValueRW, ref pendingVisualApplies);
            AdvanceAnimation(
                entity,
                deltaTime,
                ref animation.ValueRW,
                ref pendingFramePropertyApplies,
                ref pendingDestroyFlags);
        }

        ApplyPendingVisuals(pendingVisualApplies);
        ApplyPendingFrameProperties(pendingFramePropertyApplies);
        ApplyPendingDestroyFlags(pendingDestroyFlags);
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

    private void QueueVisualApplyIfNeeded(
        Entity entity,
        ref QuadAnimationComponent animation,
        ref List<PendingVisualApply> pendingVisualApplies)
    {
        if (!EntityManager.HasComponent<MaterialMeshInfo>(entity) ||
            !EntityManager.HasComponent<QuadAnimationVisualComponent>(entity))
            return;

        QuadAnimationVisualComponent visual = EntityManager.GetComponentObject<QuadAnimationVisualComponent>(entity);
        if (visual?.Texture == null)
            return;

        int textureInstanceId = visual.Texture.GetInstanceID();
        int visualKeyHash = QuadAnimationVisualUtility.GetVisualKeyHash(visual.PrefabName);
        if (animation.LastTextureInstanceId == textureInstanceId &&
            animation.LastVisualKeyHash == visualKeyHash)
        {
            return;
        }

        if (!QuadAnimationVisualUtility.TryResolveVisual(
                visual.PrefabName,
                visual.Texture,
                out Mesh mesh,
                out Material material))
        return;

        pendingVisualApplies ??= new List<PendingVisualApply>();
        pendingVisualApplies.Add(new PendingVisualApply(entity, mesh, material));
        animation.LastTextureInstanceId = textureInstanceId;
        animation.LastVisualKeyHash = visualKeyHash;
    }

    private void AdvanceAnimation(
        Entity entity,
        float deltaTime,
        ref QuadAnimationComponent animation,
        ref List<PendingFramePropertiesApply> pendingFramePropertyApplies,
        ref List<Entity> pendingDestroyFlags)
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
                QueueCompletion(entity, ref animation, ref pendingDestroyFlags);
                return;
            }
        }

        int nextFrameIndex = ResolveFrameIndex(frameCount, fps, animation.ElapsedSeconds, animation.Loop != 0);
        if (nextFrameIndex != animation.FrameIndex)
        {
            animation.FrameIndex = nextFrameIndex;
            QueueFrameProperties(
                entity,
                gridColumns,
                gridRows,
                nextFrameIndex,
                animation.Width,
                animation.Height,
                animation.PivotOffset,
                ref pendingFramePropertyApplies);
        }

        if (animation.IsPlaying == 0 || animation.Loop != 0)
            return;

        float duration = frameCount / fps;
        if (animation.ElapsedSeconds < duration)
            return;

        animation.IsPlaying = 0;
        QueueCompletion(entity, ref animation, ref pendingDestroyFlags);
    }

    private static void QueueCompletion(
        Entity entity,
        ref QuadAnimationComponent animation,
        ref List<Entity> pendingDestroyFlags)
    {
        if (animation.AutoDestroyOnComplete == 0)
            return;

        pendingDestroyFlags ??= new List<Entity>();
        pendingDestroyFlags.Add(entity);
    }

    private static void QueueFrameProperties(
        Entity entity,
        int gridColumns,
        int gridRows,
        int frameIndex,
        float width,
        float height,
        float2 pivotOffset,
        ref List<PendingFramePropertiesApply> pendingFramePropertyApplies)
    {
        float uvWidth = 1f / gridColumns;
        float uvHeight = 1f / gridRows;
        int clampedFrameIndex = math.clamp(frameIndex, 0, (gridColumns * gridRows) - 1);
        int col = clampedFrameIndex % gridColumns;
        int rowTop = clampedFrameIndex / gridColumns;
        int row = (gridRows - 1) - rowTop;
        float uvMinX = col * uvWidth;
        float uvMinY = row * uvHeight;

        pendingFramePropertyApplies ??= new List<PendingFramePropertiesApply>();
        pendingFramePropertyApplies.Add(new PendingFramePropertiesApply(
            entity,
            new float4(uvMinX, uvMinY, 0f, 0f),
            new float4(uvWidth, uvHeight, 0f, 0f),
            new float4(math.max(0.01f, width), math.max(0.01f, height), 0f, 0f),
            new float4(pivotOffset.x, pivotOffset.y, 0f, 0f)));
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

    private void ApplyPendingVisuals(List<PendingVisualApply> pendingVisualApplies)
    {
        if (pendingVisualApplies == null)
            return;

        for (int i = 0; i < pendingVisualApplies.Count; i++)
        {
            PendingVisualApply pending = pendingVisualApplies[i];
            if (!EntityManager.Exists(pending.Entity) ||
                !EntityManager.HasComponent<MaterialMeshInfo>(pending.Entity))
            {
                continue;
            }

            EntityManager.SetSharedComponentManaged(
                pending.Entity,
                new RenderMeshArray(new[] { pending.Material }, new[] { pending.Mesh }));
            EntityManager.SetComponentData(pending.Entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }
    }

    private void ApplyPendingFrameProperties(List<PendingFramePropertiesApply> pendingFramePropertyApplies)
    {
        if (pendingFramePropertyApplies == null)
            return;

        for (int i = 0; i < pendingFramePropertyApplies.Count; i++)
        {
            PendingFramePropertiesApply pending = pendingFramePropertyApplies[i];
            if (!EntityManager.Exists(pending.Entity))
                continue;

            SetOrAddProperty(pending.Entity, new QuadAnimationFrameUvMinProperty { Value = pending.UvMin });
            SetOrAddProperty(pending.Entity, new QuadAnimationFrameUvSizeProperty { Value = pending.UvSize });
            SetOrAddProperty(pending.Entity, new QuadAnimationFrameWorldSizeProperty { Value = pending.WorldSize });
            SetOrAddProperty(pending.Entity, new QuadAnimationFramePivotOffsetProperty { Value = pending.PivotOffset });
        }
    }

    private void ApplyPendingDestroyFlags(List<Entity> pendingDestroyFlags)
    {
        if (pendingDestroyFlags == null)
            return;

        for (int i = 0; i < pendingDestroyFlags.Count; i++)
        {
            Entity entity = pendingDestroyFlags[i];
            if (!EntityManager.Exists(entity))
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }

    private readonly struct PendingVisualApply
    {
        public PendingVisualApply(Entity entity, Mesh mesh, Material material)
        {
            Entity = entity;
            Mesh = mesh;
            Material = material;
        }

        public Entity Entity { get; }
        public Mesh Mesh { get; }
        public Material Material { get; }
    }

    private readonly struct PendingFramePropertiesApply
    {
        public PendingFramePropertiesApply(
            Entity entity,
            float4 uvMin,
            float4 uvSize,
            float4 worldSize,
            float4 pivotOffset)
        {
            Entity = entity;
            UvMin = uvMin;
            UvSize = uvSize;
            WorldSize = worldSize;
            PivotOffset = pivotOffset;
        }

        public Entity Entity { get; }
        public float4 UvMin { get; }
        public float4 UvSize { get; }
        public float4 WorldSize { get; }
        public float4 PivotOffset { get; }
    }
}
