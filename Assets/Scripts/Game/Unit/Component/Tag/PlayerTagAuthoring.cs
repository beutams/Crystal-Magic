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
            AddComponent<PlayerSkillComponent>(entity);
            AddBuffer<UnitBuffElement>(entity);
            AddComponentObject(entity, new UnitBuffPayloadComponent());
        }
    }
}

public struct PlayerTag : IComponentData
{
}

/// <summary>
/// 鐜╁鏍囪缁勪欢
/// </summary>
