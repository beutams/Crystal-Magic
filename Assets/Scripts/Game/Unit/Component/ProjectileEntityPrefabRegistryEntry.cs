using Unity.Collections;
using Unity.Entities;

namespace CrystalMagic.Game.Unit
{
    public struct ProjectileEntityPrefabRegistryEntry : IBufferElementData
    {
        public FixedString128Bytes Name;
        public Entity Prefab;
    }
}
