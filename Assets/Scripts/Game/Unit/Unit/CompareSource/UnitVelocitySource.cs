using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// 读取单位当前速度大小（Velocity 向量的模长）。
/// 需要通过 Init 注入 Entity/EntityManager 后才能使用。
/// </summary>
[FactoryKey("UnitVelocitySource")]
public class UnitVelocitySource : ISource
{
    private Entity _entity;
    private EntityManager _em;

    public void Init(Entity entity, EntityManager em)
    {
        _entity = entity;
        _em     = em;
    }

    public float GetValue()
    {
        if (!_em.HasComponent<UnitIntentComponent>(_entity)) return 0f;
        return math.length(_em.GetComponentData<UnitIntentComponent>(_entity).MoveDirection);
    }
}

[FactoryKey("UnitIsEnemySource")]
public class UnitIsEnemySource : ISource
{
    private Entity _targetEntity;
    private Entity _originEntity;
    private EntityManager _em;
    private bool _hasOriginEntity;

    public void Init(SourceContext context)
    {
        _targetEntity = context.Entity;
        _originEntity = context.OriginEntity;
        _em = context.EntityManager;
        _hasOriginEntity = context.HasOriginEntity;
    }

    public float GetValue()
    {
        if (!_hasOriginEntity ||
            !_em.Exists(_originEntity) ||
            !_em.Exists(_targetEntity) ||
            !_em.HasComponent<UnitFactionComponent>(_originEntity) ||
            !_em.HasComponent<UnitFactionComponent>(_targetEntity))
        {
            return 0f;
        }

        UnitFactionType originFaction = _em.GetComponentData<UnitFactionComponent>(_originEntity).Value;
        UnitFactionType targetFaction = _em.GetComponentData<UnitFactionComponent>(_targetEntity).Value;
        return UnitFactionUtility.IsEnemy(originFaction, targetFaction) ? 1f : 0f;
    }
}
