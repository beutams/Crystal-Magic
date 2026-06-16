using Unity.Collections;
using Unity.Entities;

namespace CrystalMagic.Game.Unit
{
    public struct DropEntityPrefabRegistryEntry : IBufferElementData
    {
        public FixedString128Bytes Name;
        public Entity Prefab;
    }
}
