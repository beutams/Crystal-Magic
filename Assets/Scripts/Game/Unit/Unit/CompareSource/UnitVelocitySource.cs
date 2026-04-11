using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// 读取单位当前速度大小（Velocity 向量的模长）。
/// 需要通过 Init 注入 Entity/EntityManager 后才能使用。
/// </summary>
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
