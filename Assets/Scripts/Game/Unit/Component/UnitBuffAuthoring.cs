using Unity.Entities;
using UnityEngine;

public class UnitBuffAuthoring : MonoBehaviour
{
    class UnitBuffBaker : Baker<UnitBuffAuthoring>
    {
        public override void Bake(UnitBuffAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            // Buffer 存储当前单位身上所有 Buff 的 Id（来自 BuffDataTable）
            AddBuffer<UnitBuffElement>(entity);
        }
    }
}

/// <summary>
/// 单位当前身上的 Buff 列表元素
/// DynamicBuffer&lt;UnitBuffElement&gt; 存储该单位所有激活 Buff 的 Id
/// </summary>
public struct UnitBuffElement : IBufferElementData
{
    /// <summary>对应 BuffDataTable 中的 Id</summary>
    public int BuffId;

    /// <summary>剩余持续时间（秒），≤0 时由 UnitBuffSystem 移除</summary>
    public float RemainingTime;

    /// <summary>当前叠层数</summary>
    public int StackCount;
}
