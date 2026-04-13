using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitIntentAuthoring : MonoBehaviour
{
    class UnitIntentBaker : Baker<UnitIntentAuthoring>
    {
        public override void Bake(UnitIntentAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitIntentComponent());
        }
    }
}

/// <summary>
/// 单位意图组件——所有输入源（玩家 Input / AI 行为树）的统一写入目标。
/// 状态机读取此组件决定行为，不直接读取原始输入。
/// 后续扩展攻击、施法等意图也加在这里。
/// </summary>
public struct UnitIntentComponent : IComponentData
{
    /// <summary>移动方向</summary>
    public float2 MoveDirection;
    /// <summary>想要释放技能</summary>
    public bool WantToCast;
}
