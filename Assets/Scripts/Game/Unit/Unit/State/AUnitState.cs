using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// 单位行为状态基类
/// OnInitialize 由 UnitStateMachineBuilder 在构建时调用，将 Entity/EntityManager 注入，
/// 子类可通过这两个字段在 OnEnter/OnUpdate/OnExit 中读写 ECS 组件数据。
/// </summary>
public abstract class AUnitState
{
    /// <summary>所属 Entity，Builder 初始化后有效</summary>
    protected Entity Entity;
    /// <summary>所属 World 的 EntityManager，Builder 初始化后有效</summary>
    protected EntityManager EntityManager;

    [System.NonSerialized] public Dictionary<AUnitState, Comparator> transitions;

    public virtual void OnInitialize(Entity entity, EntityManager em)
    {
        Entity        = entity;
        EntityManager = em;
    }

    public abstract void OnEnter();
    public abstract void OnUpdate(float deltaTime);
    public abstract void OnExit();
}
