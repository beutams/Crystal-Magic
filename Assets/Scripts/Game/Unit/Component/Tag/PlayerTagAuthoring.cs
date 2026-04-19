using Unity.Entities;
using UnityEngine;

public class PlayerTagAuthoring : MonoBehaviour
{
    class PlayerTagBaker : Baker<PlayerTagAuthoring>
    {
        public override void Bake(PlayerTagAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
        }
    }
}

public struct PlayerTag : IComponentData
{
}

/// <summary>
/// 鐜╁鏍囪缁勪欢
/// </summary>
