using CrystalMagic.Game.Data;
using Unity.Entities;

namespace CrystalMagic.Editor.Unit
{
    public sealed class UnitRuntimeDrawerContext
    {
        public UnitRuntimeDrawerContext(EntityManager entityManager, Entity entity, string unitName, UnitData unitData)
        {
            EntityManager = entityManager;
            Entity = entity;
            UnitName = unitName;
            UnitData = unitData;
        }

        public EntityManager EntityManager { get; }
        public Entity Entity { get; }
        public string UnitName { get; }
        public UnitData UnitData { get; }

        public bool HasComponent<T>()
        {
            return Entity != Entity.Null &&
                   EntityManager.Exists(Entity) &&
                   EntityManager.HasComponent<T>(Entity);
        }

        public T GetComponent<T>() where T : unmanaged, IComponentData
        {
            return EntityManager.GetComponentData<T>(Entity);
        }
    }

    public interface IUnitRuntimeAttributeDrawer
    {
        bool CanDraw(UnitRuntimeDrawerContext context);
        void Draw(UnitRuntimeDrawerContext context);
    }
}
