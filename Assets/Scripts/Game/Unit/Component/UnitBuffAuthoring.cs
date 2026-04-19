using Unity.Entities;
using UnityEngine;

public class UnitBuffAuthoring : MonoBehaviour
{
    class UnitBuffBaker : Baker<UnitBuffAuthoring>
    {
        public override void Bake(UnitBuffAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            // Buffer 瀛樺偍褰撳墠鍗曚綅韬笂鎵€鏈?Buff 鐨?Id锛堟潵鑷?BuffDataTable锛?
            AddBuffer<UnitBuffElement>(entity);
        }
    }
}

public struct UnitBuffElement : IBufferElementData
{
    public int BuffId;
    public float RemainingTime;
    public int StackCount;
}

/// <summary>
/// 鍗曚綅褰撳墠韬笂鐨?Buff 鍒楄〃鍏冪礌
/// DynamicBuffer&lt;UnitBuffElement&gt; 瀛樺偍璇ュ崟浣嶆墍鏈夋縺娲?Buff 鐨?Id
/// </summary>
