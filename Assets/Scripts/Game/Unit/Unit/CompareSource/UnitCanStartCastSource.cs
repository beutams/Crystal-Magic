using Unity.Entities;

[FactoryKey("UnitCanStartCastSource")]
[EditorLabel("可以开始施法")]
public class UnitCanStartCastSource : ISource
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
            _context.EntityManager.HasComponent<UnitCastAvailabilityComponent>(_context.Entity);
    }

    public float GetValue()
    {
        if (!CanUse())
            return 0f;

        UnitCastAvailabilityComponent availability = _context.EntityManager.GetComponentData<UnitCastAvailabilityComponent>(_context.Entity);
        return availability.CanStartCast != 0 ? 1f : 0f;
    }
}
