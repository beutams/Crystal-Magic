using Unity.Entities;

namespace CrystalMagic.Core
{
    public readonly struct GameplayEventPayload
    {
        public GameplayEventPayload(Entity sourceEntity, UnitValue value)
        {
            SourceEntity = sourceEntity;
            Value = value;
        }

        public Entity SourceEntity { get; }
        public UnitValue Value { get; }
    }
}
