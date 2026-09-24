using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// 本地玩家移动的纯表现状态。该组件不进入预测快照，也不会被 SS 回滚。
/// </summary>
public struct ClientPlayerMovePresentationComponent : IComponentData
{
    public float3 CurrentPosition;
    public float3 CorrectionOffset;
    public byte ReconciliationPending;
    public byte Initialized;
}
