using Unity.Entities;

public readonly struct SourceContext
{
    public SourceContext(Entity entity, EntityManager entityManager, Entity originEntity, bool hasOriginEntity)
    {
        Entity = entity;
        EntityManager = entityManager;
        OriginEntity = originEntity;
        HasOriginEntity = hasOriginEntity;
    }

    public Entity Entity { get; }

    public EntityManager EntityManager { get; }

    public Entity OriginEntity { get; }

    public bool HasOriginEntity { get; }
}

public interface ISource
{
    float GetValue();

    void Init(Entity entity, EntityManager em) { }

    void Init(SourceContext context)
    {
        Init(context.Entity, context.EntityManager);
    }
}
