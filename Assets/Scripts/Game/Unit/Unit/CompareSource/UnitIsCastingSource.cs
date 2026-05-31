using Unity.Entities;

[FactoryKey("UnitIsCastingSource")]
[EditorLabel("是否正在施法")]
public class UnitIsCastingSource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        if (_context.HasRuntimeEntity)
            return _context.EntityManager.HasComponent<UnitCastComponent>(_context.Entity);

        return _context.UnitPrefab != null && _context.UnitPrefab.GetComponent<UnitCastAuthoring>() != null;
    }

    public float GetValue()
    {
        if (!_context.HasRuntimeEntity || !_context.EntityManager.HasComponent<UnitCastComponent>(_context.Entity))
            return 0f;

        UnitCastComponent cast = _context.EntityManager.GetComponentData<UnitCastComponent>(_context.Entity);
        return cast.IsCasting || cast.HasPreparedCast ? 1f : 0f;
    }
}
