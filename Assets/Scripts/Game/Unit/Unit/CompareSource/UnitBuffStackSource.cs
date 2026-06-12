using Unity.Entities;

[FactoryKey("UnitBuffStackSource")]
[EditorLabel("Buff层数")]
public class UnitBuffStackSource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        if (_context.SourceParam < 0)
            return false;

        if (_context.HasRuntimeEntity)
            return _context.EntityManager.HasComponent<UnitBuffRuntimeComponent>(_context.Entity);

        return _context.UnitPrefab != null && _context.UnitPrefab.GetComponent<UnitBuffAuthoring>() != null;
    }

    public float GetValue()
    {
        if (_context.SourceParam < 0 ||
            !_context.HasRuntimeEntity ||
            !_context.EntityManager.HasComponent<UnitBuffRuntimeComponent>(_context.Entity))
        {
            return 0f;
        }

        UnitBuffRuntimeComponent runtimeComponent = _context.EntityManager.GetComponentObject<UnitBuffRuntimeComponent>(_context.Entity);
        if (runtimeComponent?.Buffs == null)
            return 0f;

        for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
            if (entry.BuffId == _context.SourceParam)
                return entry.StackCount;
        }

        return 0f;
    }
}
