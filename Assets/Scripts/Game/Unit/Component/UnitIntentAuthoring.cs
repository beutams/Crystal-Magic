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

public struct UnitIntentComponent : IComponentData
{
    public float2 MoveDirection;
    public bool WantToCast;
    public bool HasCastTarget;
    public float2 CastTargetPosition;
}

/// <summary>
/// 鍗曚綅鎰忓浘缁勪欢鈥斺€旀墍鏈夎緭鍏ユ簮锛堢帺瀹?Input / AI 琛屼负鏍戯級鐨勭粺涓€鍐欏叆鐩爣銆?
/// 鐘舵€佹満璇诲彇姝ょ粍浠跺喅瀹氳涓猴紝涓嶇洿鎺ヨ鍙栧師濮嬭緭鍏ャ€?
/// 鍚庣画鎵╁睍鏀诲嚮銆佹柦娉曠瓑鎰忓浘涔熷姞鍦ㄨ繖閲屻€?
/// </summary>
