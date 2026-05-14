using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// 读取单位当前速度大小（Velocity 向量的模长）。
/// 需要通过 Init 注入 Entity/EntityManager 后才能使用。
/// </summary>
[FactoryKey("UnitVelocitySource")]
[EditorLabel("移动输入强度")]
public class UnitVelocitySource : ISource
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

        return _context.UnitPrefab != null && _context.UnitPrefab.GetComponent<UnitMoveAuthoring>() != null;
    }

    public float GetValue()
    {
        if (!_context.HasRuntimeEntity || !_context.EntityManager.HasComponent<UnitIntentComponent>(_context.Entity))
            return 0f;

        return math.length(_context.EntityManager.GetComponentData<UnitIntentComponent>(_context.Entity).MoveDirection);
    }
}

[FactoryKey("UnitIsEnemySource")]
[EditorLabel("是否敌对")]
public class UnitIsEnemySource : ISource
{
    private SourceContext _context;

    public void Init(SourceContext context)
    {
        _context = context;
    }

    public bool CanUse()
    {
        return _context.HasRuntimeEntity &&
            _context.HasOriginEntity &&
            _context.EntityManager.Exists(_context.OriginEntity) &&
            _context.EntityManager.Exists(_context.Entity) &&
            _context.EntityManager.HasComponent<UnitFactionComponent>(_context.OriginEntity) &&
            _context.EntityManager.HasComponent<UnitFactionComponent>(_context.Entity);
    }

    public float GetValue()
    {
        if (!CanUse())
        {
            return 0f;
        }

        UnitFactionType originFaction = _context.EntityManager.GetComponentData<UnitFactionComponent>(_context.OriginEntity).Value;
        UnitFactionType targetFaction = _context.EntityManager.GetComponentData<UnitFactionComponent>(_context.Entity).Value;
        return UnitFactionUtility.IsEnemy(originFaction, targetFaction) ? 1f : 0f;
    }
}
