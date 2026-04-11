using Unity.Entities;

public interface ISource
{
    float GetValue();

    /// <summary>
    /// 注入 Entity / EntityManager，需要访问 ECS 数据的 Source 应重写此方法。
    /// 默认空实现，不需要 ECS 数据的 Source 无需处理。
    /// </summary>
    void Init(Entity entity, EntityManager em) { }
}