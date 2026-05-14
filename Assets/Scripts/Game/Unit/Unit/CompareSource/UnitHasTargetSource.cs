using Unity.Entities;

[FactoryKey("UnitHasTargetSource")]
[EditorLabel("是否有目标")]
public class UnitHasTargetSource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        if (_context.HasRuntimeEntity)
            return _context.EntityManager.HasComponent<UnitPerceptionComponent>(_context.Entity);

        return _context.UnitPrefab != null && _context.UnitPrefab.GetComponent<UnitPerceptionAuthoring>() != null;
    }

    public float GetValue()
    {
        if (!_context.HasRuntimeEntity || !_context.EntityManager.HasComponent<UnitPerceptionComponent>(_context.Entity))
            return 0f;

        return _context.EntityManager.GetComponentData<UnitPerceptionComponent>(_context.Entity).HasTarget ? 1f : 0f;
    }
}
