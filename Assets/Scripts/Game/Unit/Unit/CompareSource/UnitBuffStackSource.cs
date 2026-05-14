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
            return _context.EntityManager.HasBuffer<UnitBuffElement>(_context.Entity);

        return _context.UnitPrefab != null && _context.UnitPrefab.GetComponent<UnitBuffAuthoring>() != null;
    }

    public float GetValue()
    {
        if (_context.SourceParam < 0 ||
            !_context.HasRuntimeEntity ||
            !_context.EntityManager.HasBuffer<UnitBuffElement>(_context.Entity))
        {
            return 0f;
        }

        DynamicBuffer<UnitBuffElement> buffer = _context.EntityManager.GetBuffer<UnitBuffElement>(_context.Entity, true);
        for (int i = 0; i < buffer.Length; i++)
        {
            UnitBuffElement element = buffer[i];
            if (element.BuffId == _context.SourceParam)
                return element.StackCount;
        }

        return 0f;
    }
}
