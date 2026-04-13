using Unity.Entities;

public class UnitWantToCastSource : ISource
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
        if (!_em.HasComponent<UnitIntentComponent>(_entity))
            return 0f;

        return _em.GetComponentData<UnitIntentComponent>(_entity).WantToCast ? 1f : 0f;
    }
}
