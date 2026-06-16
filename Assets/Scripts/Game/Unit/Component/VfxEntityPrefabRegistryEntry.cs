using Unity.Collections;
using Unity.Entities;

namespace CrystalMagic.Game.Unit
{
    public struct VfxEntityPrefabRegistryEntry : IBufferElementData
    {
        public FixedString128Bytes Name;
        public Entity Prefab;
    }
}
