using Unity.Entities;

public interface ISource
{
    float GetValue();

    void Init(Entity entity, EntityManager em) { }
}