using Unity.Entities;
using Unity.Mathematics;

[FactoryKey("UnitHealthRatioSource")]
[EditorLabel("生命百分比")]
public class UnitHealthRatioSource : ISource
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
            _context.EntityManager.HasComponent<UnitVitalityComponent>(_context.Entity);
    }

    public float GetValue()
    {
        if (!CanUse())
            return 0f;

        UnitVitalityComponent vitality = _context.EntityManager.GetComponentData<UnitVitalityComponent>(_context.Entity);
        float maxHealth = math.max(0f, vitality.RealMaxHealth);
        if (maxHealth <= 0f)
            return 0f;

        return math.clamp(vitality.CurrentHealth / maxHealth, 0f, 1f);
    }
}
