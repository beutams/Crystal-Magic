using Unity.Entities;

[FactoryKey("UnitWantToCastSource")]
[EditorLabel("想要施法")]
public class UnitWantToCastSource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        if (_context.HasRuntimeEntity)
            return _context.EntityManager.HasComponent<UnitIntentComponent>(_context.Entity);

        return _context.UnitPrefab != null && _context.UnitPrefab.GetComponent<UnitIntentAuthoring>() != null;
    }

    public float GetValue()
    {
        if (!_context.HasRuntimeEntity || !_context.EntityManager.HasComponent<UnitIntentComponent>(_context.Entity))
            return 0f;

        return _context.EntityManager.GetComponentData<UnitIntentComponent>(_context.Entity).WantToCast ? 1f : 0f;
    }
}
