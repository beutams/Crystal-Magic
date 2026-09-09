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

[FactoryKey("UnitIsEnemySource")]
[EditorLabel("是否敌对")]
public sealed class UnitIsEnemySource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public float GetValue()
    {
        EntityManager entityManager = _context.EntityManager;
        if (!_context.HasOriginEntity ||
            !entityManager.Exists(_context.OriginEntity) ||
            !entityManager.Exists(_context.Entity) ||
            !entityManager.HasComponent<UnitFactionComponent>(_context.OriginEntity) ||
            !entityManager.HasComponent<UnitFactionComponent>(_context.Entity))
        {
            return 0f;
        }

        UnitFactionType originFaction = entityManager.GetComponentData<UnitFactionComponent>(_context.OriginEntity).Value;
        UnitFactionType targetFaction = entityManager.GetComponentData<UnitFactionComponent>(_context.Entity).Value;
        return UnitFactionUtility.IsEnemy(originFaction, targetFaction) ? 1f : 0f;
    }
}
