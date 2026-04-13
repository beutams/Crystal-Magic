using Unity.Entities;

public class UnitIsCastingSource : ISource
{
    private Entity _entity;
    private EntityManager _em;

    public void Init(Entity entity, EntityManager em)
    {
        _entity = entity;
        _em = em;
    }

    public float GetValue()
    {
        if (!_em.HasComponent<UnitCastComponent>(_entity))
            return 0f;

        return _em.GetComponentData<UnitCastComponent>(_entity).IsCasting ? 1f : 0f;
    }
}
