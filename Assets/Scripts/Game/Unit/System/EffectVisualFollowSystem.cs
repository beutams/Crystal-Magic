using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[RunInGameWorld(GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSystem))]
[UpdateBefore(typeof(SpriteEffectAnimationSystem))]
partial class EffectVisualFollowSystem : SystemBase
{
    protected override void OnUpdate()
    {
        List<Entity> pendingDestroy = null;
        foreach ((RefRO<EffectVisualFollowComponent> followReference, RefRW<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<EffectVisualFollowComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            EffectVisualFollowComponent follow = followReference.ValueRO;
            if (follow.Target == Entity.Null || !EntityManager.Exists(follow.Target) ||
                !EntityManager.HasComponent<LocalTransform>(follow.Target))
            {
                if (follow.EndWhenTargetMissing == 0)
                    continue;

                if (EntityManager.HasComponent<SpriteEffectAnimationComponent>(entity))
                {
                    SpriteEffectAnimationSystem.RequestEnd(EntityManager, entity);
                    continue;
                }

                pendingDestroy ??= new List<Entity>();
                pendingDestroy.Add(entity);
                continue;
            }

            LocalTransform targetTransform = EntityManager.GetComponentData<LocalTransform>(follow.Target);
            LocalTransform visualTransform = transform.ValueRO;
            quaternion rotation = follow.AlignRotation != 0 ? targetTransform.Rotation : visualTransform.Rotation;
            visualTransform.Position = targetTransform.Position + math.rotate(rotation, follow.Offset);
            if (follow.AlignRotation != 0)
                visualTransform.Rotation = rotation;
            transform.ValueRW = visualTransform;
        }

        MarkForDestroy(pendingDestroy);
    }

    private void MarkForDestroy(List<Entity> entities)
    {
        if (entities == null)
            return;

        for (int index = 0; index < entities.Count; index++)
        {
            Entity entity = entities[index];
            if (!EntityManager.Exists(entity))
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
