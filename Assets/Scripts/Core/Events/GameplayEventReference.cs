using Unity.Entities;

namespace CrystalMagic.Core
{
    public readonly struct GameplayEventReference
    {
        public GameplayEventReference(Entity sourceEntity, UnitValue value)
        {
            SourceEntity = sourceEntity;
            Value = value;
        }

        public Entity SourceEntity { get; }
        public UnitValue Value { get; }
    }
}
