using Unity.Entities;

[FactoryKey("UnitTargetCastRangeMarginSource")]
public class UnitTargetCastRangeMarginSource : ISource
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
        if (!_em.HasComponent<UnitPerceptionComponent>(_entity) || !_em.HasComponent<UnitAttackComponent>(_entity))
            return float.MinValue;

        UnitPerceptionComponent perception = _em.GetComponentData<UnitPerceptionComponent>(_entity);
        if (!perception.HasTarget)
            return float.MinValue;

        float castRange = _em.GetComponentData<UnitAttackComponent>(_entity).RealSkillRange;
        return castRange - perception.TargetDistance;
    }
}
