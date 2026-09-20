using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class PersistentEffectUtility
{
    public static void AddEffect(PersistentEffectData data, SkillContent sourceContext, Vector3 releasePosition)
    {
        if (data == null || sourceContext == null)
            return;

        EntityManager entityManager = sourceContext.EntityManager;
        EffectRequestContext requestContext = EffectUtility.CaptureContext(
            entityManager,
            sourceContext,
            out bool ownsManagedContext);
        Entity queueEntity = GetOrCreateEntity(entityManager);
        entityManager.GetBuffer<PersistentEffectRequest>(queueEntity).Add(new PersistentEffectRequest
        {
            PersistentDataId = EffectDataBridgeUtility.Register(
                entityManager,
                new EffectData[] { data }),
            SourceContext = requestContext,
            ReleasePosition = new float3(releasePosition.x, releasePosition.y, releasePosition.z),
            ReleaseManagedContextAfterConsumption = ownsManagedContext ? (byte)1 : (byte)0,
        });
    }

    public static Entity GetOrCreateEntity(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PersistentEffectQueueComponent>());
        if (!query.IsEmptyIgnoreFilter)
            return query.GetSingletonEntity();

        Entity entity = entityManager.CreateEntity();
        entityManager.AddComponent<PersistentEffectQueueComponent>(entity);
        entityManager.AddBuffer<PersistentEffectRequest>(entity);
        return entity;
    }
}
