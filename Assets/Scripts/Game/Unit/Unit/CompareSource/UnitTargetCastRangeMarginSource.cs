using Unity.Entities;

[FactoryKey("UnitTargetCastRangeMarginSource")]
public class UnitTargetCastRangeMarginSource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        if (_context.HasRuntimeEntity)
        {
            return _context.EntityManager.HasComponent<UnitPerceptionComponent>(_context.Entity) &&
                _context.EntityManager.HasComponent<UnitAttackComponent>(_context.Entity);
        }

        return _context.UnitPrefab != null &&
            _context.UnitPrefab.GetComponent<UnitPerceptionAuthoring>() != null &&
            _context.UnitPrefab.GetComponent<UnitAttackAuthoring>() != null;
    }

    public float GetValue()
    {
        if (!_context.HasRuntimeEntity ||
            !_context.EntityManager.HasComponent<UnitPerceptionComponent>(_context.Entity) ||
            !_context.EntityManager.HasComponent<UnitAttackComponent>(_context.Entity))
        {
            return 0f;
        }

        UnitPerceptionComponent perception = _context.EntityManager.GetComponentData<UnitPerceptionComponent>(_context.Entity);
        if (!perception.HasTarget)
            return 0f;

        float castRange = _context.EntityManager.GetComponentData<UnitAttackComponent>(_context.Entity).RealSkillRange;
        return castRange - perception.TargetDistance;
    }
}
