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
            return _context.EntityManager.HasComponent<UnitSkillReleaseComponent>(_context.Entity);

        return _context.UnitPrefab != null && _context.UnitPrefab.GetComponent<UnitSkillReleaseAuthoring>() != null;
    }

    public float GetValue()
    {
        if (!_context.HasRuntimeEntity || !_context.EntityManager.HasComponent<UnitSkillReleaseComponent>(_context.Entity))
            return 0f;

        UnitSkillReleaseComponent release = _context.EntityManager.GetComponentObject<UnitSkillReleaseComponent>(_context.Entity);
        return release?.PendingRequests?.Count > 0 ? 1f : 0f;
    }
}
