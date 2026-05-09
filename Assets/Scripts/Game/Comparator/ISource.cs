using Unity.Entities;
using UnityEngine;

public readonly struct SourceContext
{
    public SourceContext(
        Entity entity,
        EntityManager entityManager,
        Entity originEntity,
        bool hasOriginEntity,
        GameObject unitPrefab = null,
        CrystalMagic.Game.Data.UnitData unitData = null,
        bool hasRuntimeEntity = true)
    {
        Entity = entity;
        EntityManager = entityManager;
        OriginEntity = originEntity;
        HasOriginEntity = hasOriginEntity;
        UnitPrefab = unitPrefab;
        UnitData = unitData;
        HasRuntimeEntity = hasRuntimeEntity;
    }

    public Entity Entity { get; }

    public EntityManager EntityManager { get; }

    public Entity OriginEntity { get; }

    public bool HasOriginEntity { get; }

    public GameObject UnitPrefab { get; }

    public CrystalMagic.Game.Data.UnitData UnitData { get; }

    public bool HasRuntimeEntity { get; }
}

public interface ISource
{
    float GetValue();

    void Init(Entity entity, EntityManager em) { }

    void Init(SourceContext context)
    {
        Init(context.Entity, context.EntityManager);
    }

    bool CanUse()
    {
        return true;
    }
}
