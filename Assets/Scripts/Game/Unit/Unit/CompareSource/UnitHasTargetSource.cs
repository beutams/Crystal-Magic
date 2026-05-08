using Unity.Entities;

[FactoryKey("UnitHasTargetSource")]
public class UnitHasTargetSource : ISource
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
        if (!_em.HasComponent<UnitPerceptionComponent>(_entity))
            return 0f;

        return _em.GetComponentData<UnitPerceptionComponent>(_entity).HasTarget ? 1f : 0f;
    }
}
