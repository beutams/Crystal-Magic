using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<PlayerFeatureAuthoring>
    {
        public override void Bake(PlayerFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddPlayerComponents(entity);
        }
    }
}
