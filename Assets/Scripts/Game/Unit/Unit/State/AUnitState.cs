using System.Collections.Generic;
using Unity.Entities;

public abstract class AUnitState
{
    protected Entity Entity;
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
