using Unity.Entities;

[FactoryKey("UnitIsControlledSource")]
[EditorLabel("是否受控")]
public class UnitIsControlledSource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        return _context.HasRuntimeEntity &&
            _context.EntityManager.Exists(_context.Entity) &&
            _context.EntityManager.HasComponent<UnitControlRuntimeComponent>(_context.Entity);
    }

    public float GetValue()
    {
        if (!CanUse())
            return 0f;

        UnitControlRuntimeComponent control = _context.EntityManager.GetComponentData<UnitControlRuntimeComponent>(_context.Entity);
        return control.HasControl != 0 ? 1f : 0f;
    }
}
